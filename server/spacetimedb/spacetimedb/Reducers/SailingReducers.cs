using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static CommandRejectionCode ApplySetCourse(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        SetCourseCommand command)
    {
        var rejection = SetCourse(ctx, world, ref ship, command.X, command.Y);
        if (rejection != CommandRejectionCode.None)
        {
            return rejection;
        }

        StartCastOff(ctx, world, ref ship, command);
        AppendEvent(
            ctx,
            world.Tick,
            ship.EntityId,
            "set_course",
            $"x={command.X:0.###},y={command.Y:0.###}");
        return CommandRejectionCode.None;
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
        ClearRoute(ctx, world, ref ship);

        // A cast-off is only worth holding while there is a course left to leave on.
        if (ship.ModeCode == (byte)ShipMode.CastingOff)
        {
            CancelActiveChannel(ctx, ref ship, world.Tick);
        }

        AppendEvent(ctx, world.Tick, ship.EntityId, "stop_course", "");
    }

    /// <summary>
    /// Answering the crossing prompt from SEA_5 §10.2. Her course is dropped: it was plotted
    /// against a land mask that does not apply on the new chart, and sailing it there would
    /// take her through an island she cannot see. Her target and whatever was stuck to her go
    /// with it, for the same reason. Her heading is kept, so she puts out of the new chart
    /// pointing the way she came into it.
    /// </summary>
    private static void ApplyChangeMap(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        if (ctx.Db.MapCrossingOffer.EntityId.Find(ship.EntityId) is not MapCrossingOffer offer)
        {
            throw new InvalidOperationException("Accepted crossing has no offer standing.");
        }

        var arrival = MapCrossingRules.Arrive(offer.ToMapId, offer.SpawnX, offer.SpawnY);
        ClearRoute(ctx, world, ref ship);
        ClearEffects(ctx, ship.EntityId);
        ship.MapId = arrival.MapId;
        ship.PositionX = arrival.PositionX;
        ship.PositionY = arrival.PositionY;
        ship.DestinationX = arrival.PositionX;
        ship.DestinationY = arrival.PositionY;
        ship.ChunkX = arrival.ChunkX;
        ship.ChunkY = arrival.ChunkY;
        ship.HasRoute = arrival.HasRoute;
        ship.MovementStatusMask = arrival.MovementStatusMask;
        ship.MovementSlowMagnitude = arrival.MovementSlowMagnitude;
        ship.EnvironmentExposureCode = arrival.EnvironmentExposureCode;
        ship.TargetEntityId = arrival.TargetEntityId;
        ship.IsEngaged = arrival.IsEngaged;
        ctx.Db.MapCrossingOffer.EntityId.Delete(ship.EntityId);
        AppendEvent(ctx, world.Tick, ship.EntityId, "change_map", $"map_id={offer.ToMapId}");
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
