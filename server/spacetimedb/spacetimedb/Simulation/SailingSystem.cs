using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void AdvanceMovingShips(ReducerContext ctx)
    {
        var deltaSeconds = 1f / WorldRules.TickRateHz;
        var worldTick = ctx.Db.WorldState.Id.Find(1)?.Tick ?? 0;
        var environment = ctx.Db.EnvironmentState.Id.Find(1);
        var navigationBlockers = NavigationBlockers(ctx);
        foreach (var ship in ctx.Db.Ship.ByMoving.Filter(true))
        {
            if (!ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var routedShip = ship;
            if (routedShip.HasWaypoint && NavigationRules.Distance(
                    routedShip.PositionX,
                    routedShip.PositionY,
                    routedShip.WaypointX,
                    routedShip.WaypointY) <= NavigationRules.WaypointArrivalRadius)
            {
                routedShip.HasWaypoint = false;
                ConfigureNavigationWaypoint(ref routedShip, navigationBlockers);
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
            var hazards = HazardsAt(ctx, routedShip.PositionX, routedShip.PositionY);
            var movementModifiers = TacticalRules.MovementModifiers(
                HasActiveStatus(ctx, routedShip.EntityId, "full_sail", worldTick),
                ActiveStatusStacks(ctx, routedShip.EntityId, "slowed", worldTick),
                routedShip.Sails == 0,
                routedShip.MaxSails == 0
                    ? 0f
                    : (float)routedShip.Sails / routedShip.MaxSails,
                hazards.InShoal,
                hazards.InStorm,
                FindActiveChannel(ctx, routedShip.EntityId) is ShipChannel channel &&
                    channel.ChannelType == "repair");
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
            var current = CurrentVelocityAt(ctx, step.PositionX, step.PositionY);
            var nextX = step.PositionX + current.X * deltaSeconds;
            var nextY = step.PositionY + current.Y * deltaSeconds;
            var moved = routedShip;
            moved.HeadingDegrees = step.HeadingDegrees;
            moved.Speed = step.Speed;
            moved.IsMoving = step.IsMoving;
            moved.HasCourse = routedShip.HasCourse && (!step.Arrived || routedShip.HasWaypoint);
            moved.IsStopping = routedShip.IsStopping && step.Speed > 0f;
            if (IsNavigablePosition(ctx, routedShip.EntityId, nextX, nextY))
            {
                moved.PositionX = Math.Clamp(nextX, WorldRules.MapMin, WorldRules.MapMax);
                moved.PositionY = Math.Clamp(nextY, WorldRules.MapMin, WorldRules.MapMax);
            }
            else
            {
                moved.HasCourse = false;
                moved.IsStopping = true;
                moved.Speed = MathF.Max(0f, routedShip.Speed - routedShip.Deceleration * deltaSeconds);
                moved.IsMoving = moved.Speed > 0f;
                moved.DestinationX = routedShip.PositionX;
                moved.DestinationY = routedShip.PositionY;
                moved.HasWaypoint = false;
            }

            moved.ChunkX = SpatialRules.ChunkCoordinate(moved.PositionX);
            moved.ChunkY = SpatialRules.ChunkCoordinate(moved.PositionY);
            ctx.Db.Ship.EntityId.Update(moved);
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
        float x,
        float y)
    {
        var velocityX = 0f;
        var velocityY = 0f;
        foreach (var zone in ctx.Db.CurrentZone.Iter())
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
        ulong movingEntityId,
        float x,
        float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            return false;
        }

        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (worldObject.IsActive && worldObject.BlocksMovement &&
                WorldRules.IsBlocked(
                    worldObject.Kind,
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius,
                    x,
                    y))
            {
                return false;
            }
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (ship.EntityId != movingEntityId && ship.IsAlive &&
                WorldRules.IsInRange(x, y, ship.PositionX, ship.PositionY, 4f))
            {
                return false;
            }
        }

        return true;
    }

    private static void MoveStorms(ReducerContext ctx)
    {
        var deltaSeconds = 1f / WorldRules.TickRateHz;
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (!worldObject.IsActive || worldObject.Kind != "storm" ||
                worldObject.MovementSpeed <= 0f)
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
