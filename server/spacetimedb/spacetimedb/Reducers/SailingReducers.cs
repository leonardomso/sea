using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplySetCourse(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        SetCourseCommand command)
    {
        var blockers = world.Blockers(ctx);
        ship.DestinationX = command.X;
        ship.DestinationY = command.Y;
        ConfigureNavigationWaypoint(ref ship, blockers);
        ship.HasCourse = ship.PositionX != command.X || ship.PositionY != command.Y;
        ship.IsStopping = false;
        ship.IsMoving = ship.HasCourse;
        AppendEvent(
            ctx,
            world.Tick,
            ship.EntityId,
            "set_course",
            $"x={command.X:0.###},y={command.Y:0.###}");
    }

    private static void ApplyStopCourse(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.WaypointX = ship.PositionX;
        ship.WaypointY = ship.PositionY;
        ship.HasWaypoint = false;
        ship.HasCourse = false;
        ship.IsStopping = ship.Speed > 0f;
        ship.IsMoving = ship.Speed > 0f;
        AppendEvent(ctx, world.Tick, ship.EntityId, "stop_course", "");
    }

    private static void ApplySelectTarget(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        SelectTargetCommand command)
    {
        ship.TargetEntityId = command.EntityId;
        ship.IsEngaged = false;
        AppendEvent(ctx, world.Tick, ship.EntityId, "select_target", $"entity_id={command.EntityId}");
    }

    private static void ApplyClearTarget(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        ship.TargetEntityId = 0;
        ship.IsEngaged = false;
        AppendEvent(ctx, world.Tick, ship.EntityId, "clear_target", "");
    }

    private static void ApplySetAmmo(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        SetAmmoCommand command)
    {
        if (!HotPathCodes.TryParseAmmunition(command.AmmoId, out var ammunitionCode))
        {
            throw new InvalidOperationException("Accepted ammunition id is invalid.");
        }

        ship.SelectedAmmoCode = (byte)ammunitionCode;
        AppendEvent(ctx, world.Tick, ship.EntityId, "set_ammo", $"ammo={command.AmmoId}");
    }
}
