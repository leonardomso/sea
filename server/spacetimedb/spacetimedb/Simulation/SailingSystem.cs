using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void AdvanceMovingShips(
        ReducerContext ctx,
        ShipTickBuffer ships,
        SpatialTickCache spatial,
        ulong tick,
        byte shardId)
    {
        var deltaSeconds = 1f / WorldRules.TickRateHz;
        var environment = ctx.Db.EnvironmentState.Id.Find(1);
        foreach (var indexedShip in ctx.Db.Ship.ByMovingShard.Filter((true, shardId)))
        {
            var ship = ships.TryGetStaged(indexedShip.EntityId, out var staged)
                ? staged
                : indexedShip;

            if (!ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var routedShip = ship;
            const int currentRefreshBuckets = 8;
            var chunkKey = routedShip.ChunkY * SpatialRules.ChunkCountPerAxis + routedShip.ChunkX;
            if (chunkKey % currentRefreshBuckets == (int)(tick % currentRefreshBuckets))
            {
                var current = CurrentVelocityAt(
                    ctx,
                    spatial,
                    routedShip.PositionX,
                    routedShip.PositionY);
                routedShip.CurrentVelocityX = current.X;
                routedShip.CurrentVelocityY = current.Y;
            }
            if (routedShip.HasWaypoint && NavigationRules.Distance(
                    routedShip.PositionX,
                    routedShip.PositionY,
                    routedShip.WaypointX,
                    routedShip.WaypointY) <= NavigationRules.WaypointArrivalRadius)
            {
                routedShip.HasWaypoint = false;
                ConfigureNavigationWaypoint(
                    ref routedShip,
                    NavigationBlockersForCourse(
                        ctx,
                        routedShip.PositionX,
                        routedShip.PositionY,
                        routedShip.DestinationX,
                        routedShip.DestinationY));
            }

            var navigationX = routedShip.HasWaypoint
                ? routedShip.WaypointX
                : routedShip.DestinationX;
            var navigationY = routedShip.HasWaypoint
                ? routedShip.WaypointY
                : routedShip.DestinationY;
            var windMultiplier = environment is EnvironmentState wind
                ? EnvironmentRules.WindSpeedMultiplier(
                    routedShip.HeadingDegrees,
                    wind.WindDirectionDegrees,
                    wind.WindStrength)
                : 1f;
            var exposure = routedShip.EnvironmentExposureCode;
            var movementModifiers = TacticalRules.MovementModifiers(
                (routedShip.MovementStatusMask & HotPathCodes.FullSailMovementMask) != 0,
                (routedShip.MovementStatusMask & HotPathCodes.SlowedMovementMask) != 0 ? 1u : 0u,
                routedShip.Sails == 0,
                routedShip.MaxSails == 0
                    ? 0f
                    : (float)routedShip.Sails / routedShip.MaxSails,
                (exposure & 2) != 0,
                (exposure & 1) != 0,
                routedShip.ModeCode == (byte)ShipMode.Repairing);
            var step = SailingRules.Step(
                new SailingState(
                    routedShip.PositionX,
                    routedShip.PositionY,
                    routedShip.HeadingDegrees,
                    routedShip.Speed),
                navigationX,
                navigationY,
                routedShip.IsStopping,
                new SailingParameters(
                    routedShip.MaximumSpeed * windMultiplier * movementModifiers.MaximumSpeed,
                    routedShip.Acceleration * movementModifiers.Acceleration,
                    routedShip.Deceleration,
                    routedShip.TurnRateDegrees * movementModifiers.TurnRate),
                deltaSeconds);
            var nextX = step.PositionX + routedShip.CurrentVelocityX * deltaSeconds;
            var nextY = step.PositionY + routedShip.CurrentVelocityY * deltaSeconds;
            var moved = routedShip;
            moved.HeadingDegrees = step.HeadingDegrees;
            moved.Speed = step.Speed;
            moved.IsMoving = step.IsMoving;
            moved.HasCourse = routedShip.HasCourse && (!step.Arrived || routedShip.HasWaypoint);
            moved.IsStopping = routedShip.IsStopping && step.Speed > 0f;
            moved.PositionX = Math.Clamp(nextX, WorldRules.MapMin, WorldRules.MapMax);
            moved.PositionY = Math.Clamp(nextY, WorldRules.MapMin, WorldRules.MapMax);

            moved.ChunkX = SpatialRules.ChunkCoordinate(moved.PositionX);
            moved.ChunkY = SpatialRules.ChunkCoordinate(moved.PositionY);
            ships.Stage(moved);
        }
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
        ReducerContext ctx,
        SpatialTickCache spatial,
        float x,
        float y)
    {
        var velocityX = 0f;
        var velocityY = 0f;
        foreach (var zone in spatial.CurrentZonesNear(ctx, x, y))
        {
            if (!zone.IsActive ||
                !WorldRules.IsInRange(x, y, zone.PositionX, zone.PositionY, zone.Radius))
            {
                continue;
            }

            var velocity = EnvironmentRules.DirectionalVelocity(
                zone.DirectionDegrees,
                zone.Strength);
            velocityX += velocity.X;
            velocityY += velocity.Y;
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
        if (tick % SimulationWorkRules.PeriodicEffectIntervalTicks != 0)
        {
            return;
        }

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
