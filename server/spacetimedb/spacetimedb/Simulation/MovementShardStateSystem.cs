using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    // A command rewrites the ship's course, so its kinematics replace what the shard
    // is integrating; every other write only retunes how the shard sails it.
    private static void PersistCommandShip(ReducerContext ctx, Ship ship, ulong tick) =>
        PersistShip(ctx, ship, tick, replaceKinematics: true);

    private static void PersistShip(ReducerContext ctx, Ship ship, ulong tick) =>
        PersistShip(ctx, ship, tick, replaceKinematics: false);

    private static void PersistShip(
        ReducerContext ctx,
        Ship ship,
        ulong tick,
        bool replaceKinematics)
    {
        ctx.Db.Ship.EntityId.Update(ship);
        UpdateShipMovement(ctx, ship, tick);
        QueueMovementUpdate(ctx, ship, replaceKinematics);
    }

    // Tracked ships are always active and alive, so the published row is rebuilt from
    // the kinematics instead of being read back first. The fat Ship row only follows
    // on a chunk change or a stop; clients read live kinematics from ShipMovement.
    // What the client was last told, so the next tick can tell how far the reckoning has
    // drifted. Recorded whenever a hull is republished, whichever row carried her.
    private static void RecordPublication(ref ShipKinematics tracked, ulong tick)
    {
        var published = ReplicationRules.Publish(
            PublishedMotionOf(tracked),
            tracked.PositionX,
            tracked.PositionY,
            tracked.HeadingDegrees,
            tick);
        tracked.PublishedTick = published.Tick;
        tracked.PublishedPositionX = published.PositionX;
        tracked.PublishedPositionY = published.PositionY;
        tracked.PublishedHeadingDegrees = published.HeadingDegrees;
        tracked.PublishedVelocityX = published.VelocityX;
        tracked.PublishedVelocityY = published.VelocityY;
    }

    private static void PublishMovement(
        ReducerContext ctx,
        ref ShipKinematics tracked,
        ulong tick,
        bool changedChunk)
    {
        ctx.Db.ShipMovement.EntityId.Update(ToShipMovement(tracked, tick));
        if (!changedChunk && tracked.IsMoving)
        {
            return;
        }

        if (ctx.Db.Ship.EntityId.Find(tracked.EntityId) is Ship stored)
        {
            CopyKinematics(tracked, ref stored);
            ctx.Db.Ship.EntityId.Update(stored);
        }
    }

    private static PublishedMotion PublishedMotionOf(ShipKinematics tracked) => new()
    {
        Tick = tracked.PublishedTick,
        PositionX = tracked.PublishedPositionX,
        PositionY = tracked.PublishedPositionY,
        HeadingDegrees = tracked.PublishedHeadingDegrees,
        VelocityX = tracked.PublishedVelocityX,
        VelocityY = tracked.PublishedVelocityY,
    };

    private static void InsertShipMovement(ReducerContext ctx, Ship ship, ulong tick)
    {
        ctx.Db.ShipMovement.Insert(ToShipMovement(ship, tick));
        SyncChunkMembership(ctx, ship, previous: null, tick);
    }

    private static void UpdateShipMovement(ReducerContext ctx, Ship ship, ulong tick)
    {
        var movement = ctx.Db.ShipMovement.EntityId.Find(ship.EntityId);
        if (movement is null)
        {
            InsertShipMovement(ctx, ship, tick);
            return;
        }

        var updated = movement.Value;
        CopyKinematics(ship, ref updated);
        updated.SnapshotTick = tick;
        ctx.Db.ShipMovement.EntityId.Update(updated);

        // The movement row is the only place the chunk she was in is still written down, so the
        // chunk rows are squared up against the row as it was, before it is overwritten above.
        SyncChunkMembership(ctx, ship, movement, tick);
    }

    private static ulong CurrentSimulationTick(ReducerContext ctx) =>
        ctx.Db.SimulationClock.Id.Find(1)?.Tick ?? 0;

    private static ShipMovement ToShipMovement(Ship ship, ulong tick)
    {
        var movement = new ShipMovement { EntityId = ship.EntityId, SnapshotTick = tick };
        CopyKinematics(ship, ref movement);
        return movement;
    }

    private static ShipMovement ToShipMovement(ShipKinematics tracked, ulong tick) => new()
    {
        EntityId = tracked.EntityId,
        FactionCode = tracked.FactionCode,
        MapId = tracked.MapId,
        PositionX = tracked.PositionX,
        PositionY = tracked.PositionY,
        HeadingDegrees = tracked.HeadingDegrees,
        Speed = tracked.Speed,
        IsMoving = tracked.IsMoving,
        IsActive = true,
        IsAlive = true,
        MovementShard = SimulationWorkRules.MovementShard(tracked.EntityId),
        ChunkX = tracked.ChunkX,
        ChunkY = tracked.ChunkY,
        SnapshotTick = tick,
    };

    private static void CopyKinematics(Ship source, ref ShipMovement target)
    {
        target.FactionCode = source.FactionCode;
        target.MapId = source.MapId;
        target.PositionX = source.PositionX;
        target.PositionY = source.PositionY;
        target.HeadingDegrees = source.HeadingDegrees;
        target.Speed = source.Speed;
        target.IsMoving = source.IsMoving;
        target.IsActive = source.IsActive;
        target.IsAlive = source.IsAlive;
        target.MovementShard = source.MovementShard;
        target.ChunkX = source.ChunkX;
        target.ChunkY = source.ChunkY;
    }

    private static void HydrateTrackedKinematics(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship)
    {
        var shard = world.MovementShard(ctx, ship.MovementShard);
        var index = FindTrackedShip(shard.Ships, ship.EntityId);
        if (index >= 0)
        {
            CopyKinematics(shard.Ships[index], ref ship);
        }
    }

    private static void QueueMovementUpdate(
        ReducerContext ctx,
        Ship ship,
        bool replaceKinematics)
    {
        var track = ShouldTrackMovement(ship);
        var update = new MovementUpdate
        {
            ShipEntityId = ship.EntityId,
            ShardId = ship.MovementShard,
            ReplaceKinematics = replaceKinematics,
            Track = track,
            Kinematics = track ? ToKinematics(ship) : default,
        };
        if (ctx.Db.MovementUpdate.ShipEntityId.Find(ship.EntityId) is MovementUpdate pending)
        {
            // A command already waiting keeps its right to replace the tracked course.
            update.ReplaceKinematics |= pending.ReplaceKinematics;
            ctx.Db.MovementUpdate.ShipEntityId.Update(update);
            return;
        }

        ctx.Db.MovementUpdate.Insert(update);
    }

    private static void ApplyPendingMovementUpdates(
        ReducerContext ctx,
        ref MovementShardState shard)
    {
        var updates = ctx.Db.MovementUpdate.ByShard.Filter(shard.ShardId).ToArray();
        foreach (var update in updates)
        {
            ApplyMovementUpdate(shard.Ships, update);
            ctx.Db.MovementUpdate.ShipEntityId.Delete(update.ShipEntityId);
        }
    }

    private static void ApplyMovementUpdate(
        List<ShipKinematics> ships,
        MovementUpdate update)
    {
        var index = FindTrackedShip(ships, update.ShipEntityId);
        if (!update.Track)
        {
            if (index >= 0)
            {
                ships.RemoveAt(index);
            }
            return;
        }

        if (index < 0)
        {
            ships.Add(update.Kinematics);
            return;
        }

        if (update.ReplaceKinematics)
        {
            ships[index] = update.Kinematics;
            return;
        }

        // Status and damage changes only retune the sailing parameters; the shard keeps
        // the position and course it has been integrating.
        var tracked = ships[index];
        CopyTacticalParameters(update.Kinematics, ref tracked);
        ships[index] = tracked;
    }

    private static MovementShardState FindMovementShard(ReducerContext ctx, byte shardId) =>
        ctx.Db.MovementShardState.ShardId.Find(shardId) ??
        throw new InvalidOperationException("Movement shard state is missing.");

    private static int FindTrackedShip(List<ShipKinematics> ships, ulong entityId)
    {
        for (var index = 0; index < ships.Count; index++)
        {
            if (ships[index].EntityId == entityId)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ShouldTrackMovement(Ship ship) =>
        ship.IsActive && ship.IsAlive && ship.IsMoving;

    private static ShipKinematics ToKinematics(Ship ship) => new()
    {
        EntityId = ship.EntityId,
        FactionCode = ship.FactionCode,
        MapId = ship.MapId,
        PositionX = ship.PositionX,
        PositionY = ship.PositionY,
        DestinationX = ship.DestinationX,
        DestinationY = ship.DestinationY,
        RouteIndex = ship.RouteIndex,
        HasRoute = ship.HasRoute,
        HeadingDegrees = ship.HeadingDegrees,
        Speed = ship.Speed,
        BaseSpeedSquaresPerSecond = ship.BaseSpeedSquaresPerSecond,
        Hull = ship.Hull,
        MaxHull = ship.MaxHull,
        MovementStatusMask = ship.MovementStatusMask,
        MovementSlowMagnitude = ship.MovementSlowMagnitude,
        EnvironmentExposureCode = ship.EnvironmentExposureCode,
        IsRepairing = ship.ModeCode == (byte)ShipMode.Repairing,
        EffectiveSpeedSquaresPerSecond = ship.EffectiveSpeedSquaresPerSecond,
        IsMoving = ship.IsMoving,
        IsInPort = ship.IsInPort,
        CurrentVelocityX = ship.CurrentVelocityX,
        CurrentVelocityY = ship.CurrentVelocityY,
        ChunkX = ship.ChunkX,
        ChunkY = ship.ChunkY,
    };

    private static void CopyKinematics(ShipKinematics source, ref Ship target)
    {
        target.PositionX = source.PositionX;
        target.PositionY = source.PositionY;
        target.DestinationX = source.DestinationX;
        target.DestinationY = source.DestinationY;
        target.RouteIndex = source.RouteIndex;
        target.HasRoute = source.HasRoute;
        target.HeadingDegrees = source.HeadingDegrees;
        target.Speed = source.Speed;
        target.EffectiveSpeedSquaresPerSecond = source.EffectiveSpeedSquaresPerSecond;
        target.IsMoving = source.IsMoving;
        target.IsInPort = source.IsInPort;
        target.CurrentVelocityX = source.CurrentVelocityX;
        target.CurrentVelocityY = source.CurrentVelocityY;
        target.ChunkX = source.ChunkX;
        target.ChunkY = source.ChunkY;
    }

    private static void CopyKinematics(ShipMovement source, ref Ship target)
    {
        target.PositionX = source.PositionX;
        target.PositionY = source.PositionY;
        target.HeadingDegrees = source.HeadingDegrees;
        target.Speed = source.Speed;
        target.IsMoving = source.IsMoving;
        target.IsActive = source.IsActive;
        target.IsAlive = source.IsAlive;
        target.MovementShard = source.MovementShard;
        target.ChunkX = source.ChunkX;
        target.ChunkY = source.ChunkY;
    }

    /// <summary>
    /// The half of a kinematics row the shard must not overwrite from the fat row: her
    /// rating and everything the water and her wounds do to it. Position and course are
    /// left alone, because the shard is the one sailing them.
    /// </summary>
    private static void CopyTacticalParameters(ShipKinematics source, ref ShipKinematics target)
    {
        target.BaseSpeedSquaresPerSecond = source.BaseSpeedSquaresPerSecond;
        target.Hull = source.Hull;
        target.MaxHull = source.MaxHull;
        target.MovementStatusMask = source.MovementStatusMask;
        target.MovementSlowMagnitude = source.MovementSlowMagnitude;
        target.EnvironmentExposureCode = source.EnvironmentExposureCode;
        target.IsRepairing = source.IsRepairing;
    }
}
