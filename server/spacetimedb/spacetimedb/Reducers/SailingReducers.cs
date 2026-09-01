using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void SetCourse(ReducerContext ctx, float x, float y)
    {
        if (!WorldRules.IsValidMove(x, y))
        {
            throw new Exception("The requested position is outside the map.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        var blockers = NavigationBlockers(ctx);
        if (NavigationRules.IsDestinationBlocked(x, y, blockers))
        {
            AppendEvent(ctx, ship.EntityId, "course_ignored", "destination_is_land");
            return;
        }

        ship.DestinationX = x;
        ship.DestinationY = y;
        ConfigureNavigationWaypoint(ref ship, blockers);
        ship.HasCourse = ship.PositionX != x || ship.PositionY != y;
        ship.IsStopping = false;
        ship.IsMoving = ship.PositionX != x || ship.PositionY != y;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "set_course", $"x={x:0.###},y={y:0.###}");
    }

    [SpacetimeDB.Reducer]
    public static void StopCourse(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.WaypointX = ship.PositionX;
        ship.WaypointY = ship.PositionY;
        ship.HasWaypoint = false;
        ship.HasCourse = false;
        ship.IsStopping = ship.Speed > 0f;
        ship.IsMoving = ship.Speed > 0f;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "stop_course", "");
    }

    [SpacetimeDB.Reducer]
    public static void SelectTarget(ReducerContext ctx, ulong entityId)
    {
        var target = FindShip(ctx, entityId);
        if (!target.IsActive || !target.IsAlive || target.Faction == "player")
        {
            throw new Exception("The selected ship cannot be targeted.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var distance = CombatRules.Distance(
            ship.PositionX,
            ship.PositionY,
            target.PositionX,
            target.PositionY);
        if (!TacticalRules.CanAcquireTarget(
                HasActiveStatus(ctx, target.EntityId, "smoke_screen", world.Tick),
                distance))
        {
            throw new Exception("Smoke conceals that ship at long range.");
        }

        ship.TargetEntityId = entityId;
        ship.IsEngaged = false;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "select_target", $"entity_id={entityId}");
    }

    [SpacetimeDB.Reducer]
    public static void ClearTarget(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.TargetEntityId = 0;
        ship.IsEngaged = false;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "clear_target", "");
    }

    [SpacetimeDB.Reducer]
    public static void SetAmmo(ReducerContext ctx, string ammoId)
    {
        if (ctx.Db.AmmoDefinition.AmmoId.Find(ammoId) is null)
        {
            throw new Exception("The selected ammunition does not exist.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        if (FindInventory(ctx, ship.EntityId, ammoId) is null)
        {
            throw new Exception("The selected ammunition is not in this ship's inventory.");
        }

        ship.SelectedAmmoId = ammoId;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "set_ammo", $"ammo={ammoId}");
    }

}
