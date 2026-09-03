using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (uint Processed, uint Dormant) AdvanceMovingShips(
        ReducerContext ctx,
        ulong tick,
        bool hasActiveLoot)
    {
        var spatial = new SpatialTickCache();
        var environment = ctx.Db.EnvironmentState.Id.Find(1);
        var currentField = ctx.Db.CurrentFieldState.Id.Find(1);
        var processed = 0u;
        for (byte shardId = 0; shardId < SimulationWorkRules.MovementShardCount; shardId++)
        {
            processed += AdvanceMovementShard(
                ctx,
                spatial,
                environment,
                currentField,
                tick,
                shardId,
                hasActiveLoot);
        }

        return (processed, 0);
    }

    private static uint AdvanceMovementShard(
        ReducerContext ctx,
        SpatialTickCache spatial,
        EnvironmentState? environment,
        CurrentFieldState? currentField,
        ulong tick,
        byte shardId,
        bool hasActiveLoot)
    {
        var shard = ctx.Db.MovementShardState.ShardId.Find(shardId) ??
            throw new InvalidOperationException("Movement shard state is missing.");
        ApplyPendingMovementUpdates(ctx, ref shard);
        var processed = ProcessMovementBatch(
            ctx,
            spatial,
            environment,
            currentField,
            shard.Ships,
            SimulationWorkRules.FirstMovementTick(shard.LastSimulatedTick, tick),
            tick,
            hasActiveLoot);

        // Ships that stopped were published while compacting; the rest publish here so
        // every client sees the tick's kinematics in the same transaction they were made.
        foreach (var ship in shard.Ships)
        {
            WriteMovementSnapshot(ctx, ship, tick);
        }

        shard.LastSimulatedTick = tick;
        ctx.Db.MovementShardState.ShardId.Update(shard);
        return processed;
    }

    private static uint ProcessMovementBatch(
        ReducerContext ctx,
        SpatialTickCache spatial,
        EnvironmentState? environment,
        CurrentFieldState? currentField,
        List<ShipKinematics> ships,
        ulong firstTick,
        ulong lastTick,
        bool hasActiveLoot)
    {
        var processed = 0u;
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < ships.Count; readIndex++)
        {
            var ship = ships[readIndex];
            for (var tick = firstTick; tick <= lastTick; tick++)
            {
                processed++;
                if (ship.IsMoving)
                {
                    ProcessMovingShip(
                        ctx,
                        spatial,
                        environment,
                        currentField,
                        ref ship,
                        tick,
                        1f / WorldRules.TickRateHz,
                        hasActiveLoot);
                }

            }

            if (ship.IsMoving)
            {
                ships[writeIndex++] = ship;
            }
            if (!ship.IsMoving)
            {
                WriteMovementSnapshot(ctx, ship, lastTick);
            }
        }

        if (writeIndex < ships.Count)
        {
            ships.RemoveRange(writeIndex, ships.Count - writeIndex);
        }

        return processed;
    }

    private static void ProcessMovingShip(
        ReducerContext ctx,
        SpatialTickCache spatial,
        EnvironmentState? environment,
        CurrentFieldState? currentField,
        ref ShipKinematics ship,
        ulong tick,
        float deltaSeconds,
        bool hasActiveLoot)
    {
        RefreshEnvironment(currentField, environment, ref ship, tick);
        AdvanceNavigationWaypoint(ctx, ref ship);
        var destination = NavigationDestination(ship);
        var parameters = MovementParameters(ship);
        var step = SailingRules.StepTowardHeading(
            new SailingState(ship.PositionX, ship.PositionY, ship.HeadingDegrees, ship.Speed),
            destination.X,
            destination.Y,
            ship.DesiredHeadingDegrees,
            ship.IsStopping,
            parameters,
            deltaSeconds);
        ApplySailingStep(ref ship, step, deltaSeconds);
        if (hasActiveLoot &&
            SimulationWorkRules.ShouldProcessLootPickup(ship.EntityId, tick))
        {
            ProcessLootClaimsForMovingShip(ctx, ship);
        }
    }

    private static void RefreshEnvironment(
        CurrentFieldState? currentField,
        EnvironmentState? environment,
        ref ShipKinematics ship,
        ulong tick)
    {
        var refreshCurrent = SimulationWorkRules.ShouldRefreshCurrent(
            ship.EntityId,
            tick);
        if (!refreshCurrent && ship.EffectiveMaximumSpeed >= 0f)
        {
            return;
        }

        if (refreshCurrent)
        {
            var current = CurrentVelocityAt(currentField, ship.PositionX, ship.PositionY);
            ship.CurrentVelocityX = current.X;
            ship.CurrentVelocityY = current.Y;
            var destination = NavigationDestination(ship);
            ship.DesiredHeadingDegrees = SailingRules.DesiredHeading(
                ship.PositionX,
                ship.PositionY,
                destination.X,
                destination.Y);
        }

        var windMultiplier = environment is EnvironmentState wind
            ? EnvironmentRules.WindSpeedMultiplier(
                ship.HeadingDegrees,
                wind.WindDirectionDegrees,
                wind.WindStrength)
            : 1f;
        ship.EffectiveMaximumSpeed = ship.TacticalMaximumSpeed * windMultiplier;
    }

    private static void AdvanceNavigationWaypoint(
        ReducerContext ctx,
        ref ShipKinematics ship)
    {
        if (!ship.HasWaypoint || NavigationRules.Distance(
                ship.PositionX,
                ship.PositionY,
                ship.WaypointX,
                ship.WaypointY) > NavigationRules.WaypointArrivalRadius)
        {
            return;
        }

        ship.HasWaypoint = false;
        ConfigureNavigationWaypoint(
            ref ship,
            NavigationBlockers(ctx));
    }

    private static (float X, float Y) NavigationDestination(ShipKinematics ship) =>
        ship.HasWaypoint
            ? (ship.WaypointX, ship.WaypointY)
            : (ship.DestinationX, ship.DestinationY);

    private static SailingParameters MovementParameters(ShipKinematics ship) =>
        new(
            ship.EffectiveMaximumSpeed,
            ship.TacticalAcceleration,
            ship.Deceleration,
            ship.TacticalTurnRateDegrees);

    private static void ApplySailingStep(
        ref ShipKinematics ship,
        AuthoritativeSailingStep step,
        float deltaSeconds)
    {
        var hasCourse = ship.HasCourse;
        var hasWaypoint = ship.HasWaypoint;
        var wasStopping = ship.IsStopping;
        ship.HeadingDegrees = step.HeadingDegrees;
        ship.Speed = step.Speed;
        ship.IsMoving = step.IsMoving;
        ship.HasCourse = hasCourse && (!step.Arrived || hasWaypoint);
        ship.IsStopping = wasStopping && step.Speed > 0f;
        ship.PositionX = Math.Clamp(
            step.PositionX + ship.CurrentVelocityX * deltaSeconds,
            WorldRules.MapMin,
            WorldRules.MapMax);
        ship.PositionY = Math.Clamp(
            step.PositionY + ship.CurrentVelocityY * deltaSeconds,
            WorldRules.MapMin,
            WorldRules.MapMax);
        ship.ChunkX = SpatialRules.ChunkCoordinate(ship.PositionX);
        ship.ChunkY = SpatialRules.ChunkCoordinate(ship.PositionY);
    }

    private static void UpdateWind(ReducerContext ctx, ulong tick)
    {
        if (ctx.Db.EnvironmentState.Id.Find(1) is not EnvironmentState environment ||
            tick < environment.NextWindChangeTick)
        {
            return;
        }

        environment.WindEpoch++;
        var wind = EnvironmentRules.WindForEpoch(environment.Seed, environment.WindEpoch);
        environment.WindDirectionDegrees = wind.DirectionDegrees;
        environment.WindStrength = wind.Strength;
        environment.NextWindChangeTick = tick + EnvironmentRules.WindEpochTicks;
        ctx.Db.EnvironmentState.Id.Update(environment);
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


    private static bool IsNavigablePosition(
        ReducerContext ctx,
        SpatialTickCache spatial,
        float x,
        float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            return false;
        }

        var bounds = SpatialRules.BoundsAround(
            x,
            y,
            SpatialRules.MaximumWorldInfluenceRadius);
        foreach (var worldObject in spatial.WorldObjectsIn(ctx, bounds))
        {
            if (worldObject.IsActive && worldObject.BlocksMovement &&
                WorldRules.IsBlocked(
                    (WorldObjectCode)worldObject.KindCode,
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius,
                    x,
                    y))
            {
                return false;
            }
        }

        return true;
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
