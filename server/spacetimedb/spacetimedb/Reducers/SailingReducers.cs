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
        StartCastOff(ctx, world, ref ship, command);
        AppendEvent(
            ctx,
            world.Tick,
            ship.EntityId,
            "set_course",
            $"x={command.X:0.###},y={command.Y:0.###}");
    }

    /// <summary>
    /// Leaving Port Lowell. The course is taken up now but sailed only once the channel completes,
    /// so the harbour is somewhere a ship withdraws to rather than cover it can dip in and out of
    /// between volleys.
    /// </summary>
    private static void StartCastOff(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        SetCourseCommand command)
    {
        if (world.Harbor(ctx) is not WorldObject harbor ||
            !PortRules.RequiresCastOff(
                ship.IsInPort,
                command.X,
                command.Y,
                harbor.PositionX,
                harbor.PositionY,
                harbor.Radius))
        {
            return;
        }

        ship.IsMoving = false;
        if (ship.ModeCode == (byte)ShipMode.CastingOff)
        {
            // The channel already running is the one paying for this course too; re-plotting the
            // destination inside it must not buy the ship a second, shorter wait.
            return;
        }

        CancelActiveChannel(ctx, ref ship, world.Tick);
        ship.ModeCode = (byte)ShipMode.CastingOff;
        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = ship.EntityId,
            ChannelType = HotPathCodes.ChannelId(ChannelCode.CastOff),
            ChannelTypeCode = (byte)ChannelCode.CastOff,
            TargetEntityId = ship.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + PortRules.CastOffTicks,
            NextProcessTick = world.Tick + PortRules.CastOffTicks,
            InitialHull = ship.Hull,
            DamageTaken = 0,
            IsActive = true,
        });
        AppendEvent(ctx, world.Tick, ship.EntityId, "cast_off_started", "");
    }

    private static void ApplyStopCourse(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        ClearCourse(ref ship);

        // A cast-off is only worth holding while there is a course left to leave on.
        if (ship.ModeCode == (byte)ShipMode.CastingOff)
        {
            CancelActiveChannel(ctx, ref ship, world.Tick);
        }

        AppendEvent(ctx, world.Tick, ship.EntityId, "stop_course", "");
    }

    private static void ClearCourse(ref Ship ship)
    {
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.WaypointX = ship.PositionX;
        ship.WaypointY = ship.PositionY;
        ship.HasWaypoint = false;
        ship.HasCourse = false;
        ship.IsStopping = ship.Speed > 0f;
        ship.IsMoving = ship.Speed > 0f;
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
