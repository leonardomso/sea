using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (uint Processed, uint Dormant) AdvanceMovingShips(
        ReducerContext ctx,
        TickWorld world)
    {
        var processed = 0u;
        for (byte shardId = 0; shardId < SimulationWorkRules.MovementShardCount; shardId++)
        {
            if (!SimulationWorkRules.ShouldAdvanceMovementShard(shardId, world.Tick))
            {
                continue;
            }

            processed += AdvanceMovementShard(ctx, world, shardId);
        }

        return (processed, 0);
    }

    private static uint AdvanceMovementShard(ReducerContext ctx, TickWorld world, byte shardId)
    {
        var shard = world.MovementShard(ctx, shardId);
        var wasIdle = shard.Ships.Count == 0;
        ApplyPendingMovementUpdates(ctx, ref shard);
        if (wasIdle && shard.Ships.Count == 0)
        {
            // Nothing sailed and nothing started, so the row stays untouched and the
            // tick pays nothing for an empty shard.
            return 0;
        }

        var processed = ProcessMovementBatch(
            ctx,
            world,
            shard.Ships,
            SimulationWorkRules.FirstMovementTick(shard.LastSimulatedTick, world.Tick, wasIdle),
            world.Tick);
        shard.LastSimulatedTick = world.Tick;
        world.StoreMovementShard(shard);
        ctx.Db.MovementShardState.ShardId.Update(shard);
        return processed;
    }

    private static uint ProcessMovementBatch(
        ReducerContext ctx,
        TickWorld world,
        List<ShipKinematics> ships,
        ulong firstTick,
        ulong lastTick)
    {
        var processed = 0u;
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < ships.Count; readIndex++)
        {
            var ship = ships[readIndex];
            var chunkX = ship.ChunkX;
            var chunkY = ship.ChunkY;
            var wasMoving = ship.IsMoving;
            for (var tick = firstTick; tick <= lastTick; tick++)
            {
                processed++;
                ProcessMovingShip(ctx, world, ref ship, tick, 1f / WorldRules.TickRateHz);
            }

            HoldAtBorder(world, ref ship);
            UpdatePortState(ctx, world, ref ship);
            if (!ship.IsMoving && world.HasActiveLoot(ctx))
            {
                // She sails out of the shard this tick, so this is her last chance to pick
                // up what she has come to rest on: a captain who steers onto a floating
                // crate and stops there has collected it, not parked beside it.
                ProcessLootClaims(ctx, ship, lastTick);
            }

            // A ship holding her course is already drawn where she is, so only the ones whose
            // reckoning has drifted, crossed into another chunk or come to rest cost a row.
            // Coming to rest is the tick she arrives, not every tick she lies still: a hull
            // set adrift on a current is published by the reckoning test like any other.
            var changedChunk = ship.ChunkX != chunkX || ship.ChunkY != chunkY;
            if (changedChunk || (wasMoving && !ship.IsMoving) || ReplicationRules.ShouldPublish(
                    PublishedMotionOf(ship),
                    ship.PositionX,
                    ship.PositionY,
                    ship.HeadingDegrees,
                    lastTick))
            {
                PublishMovement(ctx, ref ship, lastTick, changedChunk);
            }
            // A ship at rest in a current is still being carried, so she stays in the shard
            // until the water lets go of her (SEA_5 5.2).
            if (ship.IsMoving || ship.CurrentVelocityX != 0f || ship.CurrentVelocityY != 0f)
            {
                ships[writeIndex++] = ship;
            }
        }

        if (writeIndex < ships.Count)
        {
            ships.RemoveRange(writeIndex, ships.Count - writeIndex);
        }

        return processed;
    }

    /// <summary>
    /// The wall at the edge of the chart. A hull that has sailed into a crossing band is put
    /// back on its inner line and stopped there, the way she stops when a course runs out: the
    /// crossing itself is a question she has to answer (SEA_5 §10.2), not something the sea
    /// does to her. She is only recorded here; the prompt is raised outside the loop.
    /// </summary>
    /// <remarks>
    /// This runs once for the batch rather than once per simulated tick. A hull catching up on
    /// several ticks sails the whole run first and is held at the end of it, which is the same
    /// place she would have been held on the tick she reached the band: the band is six squares
    /// and a hull makes at most a square a tick, so nothing is skipped over.
    /// </remarks>
    private static void HoldAtBorder(TickWorld world, ref ShipKinematics ship)
    {
        var edge = MapEdgeRules.EdgeAt(ship.PositionX, ship.PositionY);
        if (edge == MapEdge.None)
        {
            return;
        }

        var (heldX, heldY) = MapEdgeRules.HoldInside(ship.PositionX, ship.PositionY);
        ship.PositionX = heldX;
        ship.PositionY = heldY;
        ship.ChunkX = SpatialRules.ChunkCoordinate(heldX);
        ship.ChunkY = SpatialRules.ChunkCoordinate(heldY);
        ship.HasRoute = false;
        ship.IsMoving = false;
        ship.Speed = 0f;
        world.RecordBorderBand(
            new BorderBand(ship.EntityId, ship.FactionCode, ship.MapId, edge, heldX, heldY));
    }

    /// <summary>
    /// Crossing the harbour mouth. Position only ever changes here, so this is the whole of the
    /// port boundary: entering puts out whatever a ship carried in with it, leaving is only
    /// bookkeeping because the course out was already paid for with a cast-off.
    /// </summary>
    private static void UpdatePortState(
        ReducerContext ctx,
        TickWorld world,
        ref ShipKinematics ship)
    {
        if (world.Harbor(ctx) is not WorldObject harbor)
        {
            return;
        }

        var inPort = PortRules.IsInside(
            ship.PositionX,
            ship.PositionY,
            harbor.PositionX,
            harbor.PositionY,
            harbor.Radius);
        if (inPort == ship.IsInPort ||
            ctx.Db.Ship.EntityId.Find(ship.EntityId) is not Ship stored)
        {
            ship.IsInPort = inPort;
            return;
        }

        ship.IsInPort = inPort;
        stored.IsInPort = inPort;
        if (inPort)
        {
            ClearEffects(ctx, stored.EntityId);
            stored.MovementStatusMask = 0;
            stored.MovementSlowMagnitude = 0f;
            CopyTacticalParameters(ToKinematics(stored), ref ship);
        }

        ctx.Db.Ship.EntityId.Update(stored);
    }

    private static void ProcessMovingShip(
        ReducerContext ctx,
        TickWorld world,
        ref ShipKinematics ship,
        ulong tick,
        float deltaSeconds)
    {
        RefreshEnvironment(world.CurrentField(ctx), world.Environment(ctx), ref ship, tick);
        if (ship.IsMoving)
        {
            FollowRoute(ctx, world, ref ship, deltaSeconds);
        }

        ApplyCurrentDrift(ref ship, deltaSeconds);
        ship.ChunkX = SpatialRules.ChunkCoordinate(ship.PositionX);
        ship.ChunkY = SpatialRules.ChunkCoordinate(ship.PositionY);
        if (SimulationWorkRules.ShouldProcessLootPickup(ship.EntityId, tick) &&
            world.HasActiveLoot(ctx))
        {
            ProcessLootClaims(ctx, ship, tick);
        }
    }

    /// <summary>
    /// One tick down the course. She turns instantly onto each leg and holds her rating
    /// along it (SEA_5 4.2); the only thing that moves her off the line is the current.
    /// </summary>
    private static void FollowRoute(
        ReducerContext ctx,
        TickWorld world,
        ref ShipKinematics ship,
        float deltaSeconds)
    {
        var step = RouteRules.Advance(
            world.RouteFor(ctx, ship.EntityId),
            ship.RouteIndex,
            ship.PositionX,
            ship.PositionY,
            ship.HeadingDegrees,
            ship.EffectiveSpeedSquaresPerSecond * deltaSeconds);
        ship.PositionX = step.PositionX;
        ship.PositionY = step.PositionY;
        ship.HeadingDegrees = step.HeadingDegrees;
        ship.RouteIndex = step.WaypointIndex;
        ship.Speed = ship.EffectiveSpeedSquaresPerSecond;
        if (!step.Arrived)
        {
            return;
        }

        ship.HasRoute = false;
        ship.IsMoving = false;
        ship.Speed = 0f;
    }

    /// <summary>
    /// The set of the current, which does not ask whether she has a course: a ship lying
    /// at anchor is carried too (SEA_5 5.2). Drift stops at land and at the map edge, so a
    /// hull left alone in a stream fetches up on a shore instead of being pushed through it.
    /// </summary>
    private static void ApplyCurrentDrift(ref ShipKinematics ship, float deltaSeconds)
    {
        if (ship.CurrentVelocityX == 0f && ship.CurrentVelocityY == 0f)
        {
            return;
        }

        var driftedX = Math.Clamp(
            ship.PositionX + ship.CurrentVelocityX * deltaSeconds,
            WorldRules.MapMin,
            WorldRules.MapMax);
        var driftedY = Math.Clamp(
            ship.PositionY + ship.CurrentVelocityY * deltaSeconds,
            WorldRules.MapMin,
            WorldRules.MapMax);
        if (ContentCatalog.LandMaskFor(ship.MapId).IsLand(driftedX, driftedY))
        {
            return;
        }

        ship.PositionX = driftedX;
        ship.PositionY = driftedY;
    }

    private static void RefreshEnvironment(
        CurrentFieldState? currentField,
        EnvironmentState? environment,
        ref ShipKinematics ship,
        ulong tick)
    {
        if (SimulationWorkRules.ShouldRefreshCurrent(ship.EntityId, tick))
        {
            var current = CurrentVelocityAt(currentField, ship.PositionX, ship.PositionY);
            ship.CurrentVelocityX = current.X;
            ship.CurrentVelocityY = current.Y;
        }

        var debuffs = TacticalRules.Resolve(
            (ship.MovementStatusMask & HotPathCodes.SlowedMovementMask) != 0,
            ship.MovementSlowMagnitude,
            HazardRules.HasExposure(ship.EnvironmentExposureCode, WorldObjectCode.Shoal),
            ship.IsRepairing);

        ship.EffectiveSpeedSquaresPerSecond = SpeedRules.Effective(new SpeedInputs(
            ship.BaseSpeedSquaresPerSecond,

            // Her fit's speed bonus is already in the rating, capped there by the same
            // 0.25 SpeedRules publishes. Handing it over a second time would pay it twice.
            BonusFraction: 0f,
            ship.Hull,
            ship.MaxHull,
            ship.HeadingDegrees,
            environment is EnvironmentState wind ? wind.WindDirectionDegrees : 0f,
            HazardRules.HasExposure(ship.EnvironmentExposureCode, WorldObjectCode.Storm),
            debuffs.SpeedMultiplier,

            // Nothing freezes a hull yet. The rule is written because SEA_5 5.2 has it;
            // the day something sets it, this is the line that carries it.
            IsFrozen: false));
    }

    /// <summary>
    /// The eight-hour boundary. Wind turns and storms are laid out again, both
    /// from the same band number, so a replay of the same log gets the same
    /// weather (SEA_5 12.5). This is one comparison a tick, and it does nothing
    /// on 287,999 ticks out of 288,000.
    /// </summary>
    private static void UpdateTimeBand(ReducerContext ctx, ulong tick)
    {
        if (ctx.Db.EnvironmentState.Id.Find(1) is not EnvironmentState environment)
        {
            return;
        }

        var band = EnvironmentRules.WindBand(tick);
        if (band == environment.WindBand)
        {
            return;
        }

        environment.WindBand = band;
        environment.WindDirectionDegrees = EnvironmentRules.WindForBand(environment.Seed, band);
        ctx.Db.EnvironmentState.Id.Update(environment);
        RespawnStorms(ctx, environment.Seed, band);
    }

    /// <summary>
    /// Clears the storms the last band left and lays out the ones this band
    /// calls for. Storms are replaced wholesale rather than aged out, so a hull
    /// caught in one when the band turns is simply no longer in it.
    /// </summary>
    private static void RespawnStorms(ReducerContext ctx, ulong seed, ulong band)
    {
        foreach (var storm in ctx.Db.WorldObject.ByActiveKind.Filter(
                     (true, (byte)WorldObjectCode.Storm)))
        {
            ctx.Db.WorldObject.EntityId.Delete(storm.EntityId);
        }

        var mapId = Catalog.Content.Maps[0].MapId;
        foreach (var layout in EnvironmentRules.StormsForBand(seed, band, mapId))
        {
            InsertWorldObject(
                ctx,
                AllocateEntityId(ctx),
                nameof(WorldObjectCode.Storm),
                layout.CentreX,
                layout.CentreY,
                EnvironmentRules.StormRadiusSquares,
                blocksMovement: false,
                directionDegrees: layout.DriftDirectionDegrees,
                movementSpeed: EnvironmentRules.StormDriftSquaresPerSecond);
        }
    }

    private static (float X, float Y) CurrentVelocityAt(
        CurrentFieldState? currentField,
        float x,
        float y)
    {
        if (currentField is not CurrentFieldState field)
        {
            return (0f, 0f);
        }

        var velocityX = 0f;
        var velocityY = 0f;
        var chunkX = SpatialRules.ChunkCoordinate(x);
        var chunkY = SpatialRules.ChunkCoordinate(y);
        var cell = chunkY * SpatialRules.ChunkCountPerAxis + chunkX;
        var mask = field.CellMasks[cell];
        for (var index = 0; index < field.Zones.Count && mask != 0; index++, mask >>= 1)
        {
            if ((mask & 1UL) == 0)
            {
                continue;
            }

            var zone = field.Zones[index];
            if (WorldRules.IsInRange(x, y, zone.PositionX, zone.PositionY, zone.Radius))
            {
                velocityX += zone.VelocityX;
                velocityY += zone.VelocityY;
            }
        }

        return (velocityX, velocityY);
    }


    private static void MoveStorms(ReducerContext ctx, ulong tick)
    {
        var deltaSeconds = (float)SimulationWorkRules.PeriodicEffectIntervalTicks /
            WorldRules.TickRateHz;
        foreach (var worldObject in ctx.Db.WorldObject.ByActiveKind.Filter(
                     (true, (byte)WorldObjectCode.Storm)))
        {
            if (worldObject.MovementSpeed <= 0f)
            {
                continue;
            }

            var position = TacticalRules.MoveStorm(
                worldObject.PositionX,
                worldObject.PositionY,
                worldObject.DirectionDegrees,
                worldObject.MovementSpeed,
                deltaSeconds);
            var moved = worldObject;
            moved.PositionX = position.X;
            moved.PositionY = position.Y;
            moved.ChunkX = SpatialRules.ChunkCoordinate(position.X);
            moved.ChunkY = SpatialRules.ChunkCoordinate(position.Y);
            ctx.Db.WorldObject.EntityId.Update(moved);
        }
    }
}
