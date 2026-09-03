using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void PersistCommandShip(ReducerContext ctx, Ship ship)
    {
        ctx.Db.Ship.EntityId.Update(ship);
        UpdateShipMovement(ctx, ship, CurrentSimulationTick(ctx));
        QueueMovementUpdate(ctx, ship, replaceKinematics: true);
    }

    private static void PersistShip(ReducerContext ctx, Ship ship)
    {
        ctx.Db.Ship.EntityId.Update(ship);
        UpdateShipMovement(ctx, ship, CurrentSimulationTick(ctx));
        QueueMovementUpdate(ctx, ship, replaceKinematics: false);
    }

    // Tracked ships are always active and alive, so the published row is rebuilt from
    // the kinematics instead of being read back first. The fat Ship row only follows
    // on a chunk change or a stop; clients read live kinematics from ShipMovement.
    private static void PublishMovement(
        ReducerContext ctx,
        ShipKinematics tracked,
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

    private static void InsertShipMovement(ReducerContext ctx, Ship ship) =>
        ctx.Db.ShipMovement.Insert(ToShipMovement(ship, CurrentSimulationTick(ctx)));

    private static void UpdateShipMovement(ReducerContext ctx, Ship ship, ulong tick)
    {
        var movement = ctx.Db.ShipMovement.EntityId.Find(ship.EntityId);
        if (movement is null)
        {
            ctx.Db.ShipMovement.Insert(ToShipMovement(ship, tick));
            return;
        }

        var updated = movement.Value;
        CopyKinematics(ship, ref updated);
        updated.SnapshotTick = tick;
        ctx.Db.ShipMovement.EntityId.Update(updated);
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

    private static void HydrateTrackedKinematics(ReducerContext ctx, ref Ship ship)
    {
        var shard = FindMovementShard(ctx, ship.MovementShard);
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
        var pending = ctx.Db.MovementUpdate.ShipEntityId.Find(ship.EntityId);
        if (pending is MovementUpdate existing)
        {
            if (!replaceKinematics && existing.ReplaceKinematics)
            {
                existing.Ship = ship;
            }
            else
            {
                existing.Ship = ship;
                existing.ReplaceKinematics = replaceKinematics;
            }

            existing.ShardId = ship.MovementShard;
            ctx.Db.MovementUpdate.ShipEntityId.Update(existing);
            return;
        }

        ctx.Db.MovementUpdate.Insert(new MovementUpdate
        {
            ShipEntityId = ship.EntityId,
            ShardId = ship.MovementShard,
            ReplaceKinematics = replaceKinematics,
            Ship = ship,
        });
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
        if (!ShouldTrackMovement(update.Ship))
        {
            if (index >= 0)
            {
                ships.RemoveAt(index);
            }
            return;
        }

        if (index < 0)
        {
            ships.Add(ToKinematics(update.Ship));
            return;
        }

        if (update.ReplaceKinematics)
        {
            ships[index] = ToKinematics(update.Ship);
            return;
        }

        var tracked = ships[index];
        CopyGameplayState(update.Ship, ref tracked);
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

    private static ShipKinematics ToKinematics(Ship ship)
    {
        var tactical = TacticalMovementParameters(ship);
        return new ShipKinematics
        {
            EntityId = ship.EntityId,
            FactionCode = ship.FactionCode,
            PositionX = ship.PositionX,
            PositionY = ship.PositionY,
            DestinationX = ship.DestinationX,
            DestinationY = ship.DestinationY,
            WaypointX = ship.WaypointX,
            WaypointY = ship.WaypointY,
            HasWaypoint = ship.HasWaypoint,
            DesiredHeadingDegrees = SailingRules.DesiredHeading(
                ship.PositionX,
                ship.PositionY,
                ship.HasWaypoint ? ship.WaypointX : ship.DestinationX,
                ship.HasWaypoint ? ship.WaypointY : ship.DestinationY),
            HeadingDegrees = ship.HeadingDegrees,
            Speed = ship.Speed,
            TacticalMaximumSpeed = tactical.MaximumSpeed,
            TacticalAcceleration = tactical.Acceleration,
            Deceleration = ship.Deceleration,
            TacticalTurnRateDegrees = tactical.TurnRateDegrees,
            EffectiveMaximumSpeed = -1f,
            HasCourse = ship.HasCourse,
            IsStopping = ship.IsStopping,
            IsMoving = ship.IsMoving,
            CurrentVelocityX = ship.CurrentVelocityX,
            CurrentVelocityY = ship.CurrentVelocityY,
            ChunkX = ship.ChunkX,
            ChunkY = ship.ChunkY,
        };
    }

    private static void CopyKinematics(ShipKinematics source, ref Ship target)
    {
        target.PositionX = source.PositionX;
        target.PositionY = source.PositionY;
        target.DestinationX = source.DestinationX;
        target.DestinationY = source.DestinationY;
        target.WaypointX = source.WaypointX;
        target.WaypointY = source.WaypointY;
        target.HasWaypoint = source.HasWaypoint;
        target.HeadingDegrees = source.HeadingDegrees;
        target.Speed = source.Speed;
        target.HasCourse = source.HasCourse;
        target.IsStopping = source.IsStopping;
        target.IsMoving = source.IsMoving;
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

    private static void CopyGameplayState(Ship source, ref ShipKinematics target)
    {
        var tactical = TacticalMovementParameters(source);
        target.TacticalMaximumSpeed = tactical.MaximumSpeed;
        target.TacticalAcceleration = tactical.Acceleration;
        target.Deceleration = source.Deceleration;
        target.TacticalTurnRateDegrees = tactical.TurnRateDegrees;
        target.EffectiveMaximumSpeed = -1f;
    }

    private static SailingParameters TacticalMovementParameters(Ship ship)
    {
        var modifiers = TacticalRules.MovementModifiers(
            (ship.MovementStatusMask & HotPathCodes.FullSailMovementMask) != 0,
            (ship.MovementStatusMask & HotPathCodes.SlowedMovementMask) != 0 ? 1u : 0u,
            ship.Sails == 0,
            ship.MaxSails == 0 ? 0f : (float)ship.Sails / ship.MaxSails,
            (ship.EnvironmentExposureCode & 2) != 0,
            (ship.EnvironmentExposureCode & 1) != 0,
            ship.ModeCode == (byte)ShipMode.Repairing);
        return new SailingParameters(
            ship.MaximumSpeed * modifiers.MaximumSpeed,
            ship.Acceleration * modifiers.Acceleration,
            ship.Deceleration,
            ship.TurnRateDegrees * modifiers.TurnRate);
    }
}
