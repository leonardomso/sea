using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private readonly record struct DecodedCommand(
        ShipCommandKind Kind,
        SetCourseCommand SetCourse = default,
        SelectTargetCommand SelectTarget = default,
        SetAmmoCommand SetAmmo = default,
        ChooseRespawnCommand ChooseRespawn = default);

    private static DecodedCommand DecodeCommand(ShipCommand command) => command switch
    {
        ShipCommand.SetCourse(var value) => new(ShipCommandKind.SetCourse, SetCourse: value),
        ShipCommand.StopCourse => new(ShipCommandKind.StopCourse),
        ShipCommand.SelectTarget(var value) => new(
            ShipCommandKind.SelectTarget,
            SelectTarget: value),
        ShipCommand.ClearTarget => new(ShipCommandKind.ClearTarget),
        ShipCommand.SetAmmo(var value) => new(ShipCommandKind.SetAmmo, SetAmmo: value),
        ShipCommand.Fire => new(ShipCommandKind.Fire),
        ShipCommand.ActivateAbility => new(ShipCommandKind.ActivateAbility),
        ShipCommand.StartRepair => new(ShipCommandKind.StartRepair),
        ShipCommand.StartBoarding => new(ShipCommandKind.StartBoarding),
        ShipCommand.CancelChannel => new(ShipCommandKind.CancelChannel),
        ShipCommand.UseRepairKit => new(ShipCommandKind.UseRepairKit),
        ShipCommand.ChooseRespawn(var value) => new(
            ShipCommandKind.ChooseRespawn,
            ChooseRespawn: value),
        ShipCommand.ChangeMap => new(ShipCommandKind.ChangeMap),
        _ => throw new InvalidOperationException("Unknown ship command variant."),
    };

    private static CommandSnapshot BuildCommandSnapshot(
        ReducerContext ctx,
        TickWorld world,
        Ship ship,
        DecodedCommand command)
    {
        var snapshot = new CommandSnapshot
        {
            Mode = ResolveMode(ship),
            CourseValid = true,
            TargetValid = true,
            AmmoKnown = true,
            FireRejection = FireRejection.None,
            RepairRejection = RepairRejection.None,
            KitRejection = RepairRejection.None,
            HasActiveChannel = FindActiveChannel(ctx, ship.EntityId) is not null,
        };

        return command.Kind switch
        {
            ShipCommandKind.SetCourse => CourseSnapshot(ship, snapshot, command.SetCourse),
            ShipCommandKind.SelectTarget => TargetSnapshot(
                ctx,
                ship,
                snapshot,
                command.SelectTarget),
            ShipCommandKind.SetAmmo => AmmoSnapshot(snapshot, command.SetAmmo),
            ShipCommandKind.Fire => FireSnapshot(ctx, world, ship, snapshot),
            ShipCommandKind.StartRepair => RepairSnapshot(ctx, world, ship, snapshot),
            ShipCommandKind.UseRepairKit => KitSnapshot(ctx, world, ship, snapshot),
            ShipCommandKind.ChooseRespawn => RespawnSnapshot(
                ctx,
                ship,
                snapshot,
                command.ChooseRespawn),

            // The offer row is the whole question: it exists only while she is lying against a
            // border that leads somewhere, so finding it is the same as asking whether she was
            // ever asked.
            ShipCommandKind.ChangeMap => snapshot with
            {
                CrossingOffered = ctx.Db.MapCrossingOffer.EntityId.Find(ship.EntityId) is not null,
            },
            _ => snapshot,
        };
    }

    /// <summary>
    /// Runs an accepted command and hands back what actually happened. Most executors
    /// cannot fail, but plotting a course can: whether there is a way through the
    /// islands, and whether the captain has already had her eight clicks this second,
    /// are not questions the snapshot can answer without doing the search twice. Such a
    /// command is refused after the fact, and the refusal replaces the acceptance.
    /// </summary>
    private static CommandDecision ApplyAcceptedCommand(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        DecodedCommand command,
        CommandDecision decision)
    {
        // The mode is set before the executor runs, not after, so an executor that decides on a
        // mode of its own -- casting off, or standing down when a course stays inside the port --
        // is not overwritten by the decision that let it run.
        ship.ModeCode = (byte)decision.NextMode;
        var rejection = CommandRejectionCode.None;
        switch (command.Kind)
        {
            case ShipCommandKind.SetCourse:
                rejection = ApplySetCourse(ctx, world, ref ship, command.SetCourse);
                break;
            case ShipCommandKind.StopCourse:
                ApplyStopCourse(ctx, world, ref ship);
                break;
            case ShipCommandKind.SelectTarget:
                ApplySelectTarget(ctx, world, ref ship, command.SelectTarget);
                break;
            case ShipCommandKind.ClearTarget:
                ApplyClearTarget(ctx, world, ref ship);
                break;
            case ShipCommandKind.SetAmmo:
                ApplySetAmmo(ctx, world, ref ship, command.SetAmmo);
                break;
            case ShipCommandKind.Fire:
                ApplyFire(ctx, world, ref ship);
                break;
            case ShipCommandKind.StartRepair:
                ApplyStartRepair(ctx, world, ref ship);
                break;
            case ShipCommandKind.CancelChannel:
                ApplyCancelChannel(ctx, world, ref ship);
                break;
            case ShipCommandKind.UseRepairKit:
                ApplyUseRepairKit(ctx, world, ref ship);
                break;
            case ShipCommandKind.ChooseRespawn:
                ApplyChooseRespawn(ctx, world, ship, command.ChooseRespawn);
                break;
            case ShipCommandKind.ChangeMap:
                ApplyChangeMap(ctx, world, ref ship);
                break;
            default:
                throw new InvalidOperationException("Accepted command has no executor.");
        }

        // The ship is written back either way: a dropped course still spends the
        // captain's rate-limit budget and still counts against her trust score.
        PersistCommandShip(ctx, ship, world.Tick);
        return rejection == CommandRejectionCode.None
            ? decision
            : new CommandDecision(false, rejection, decision.NextMode, CommandEffect.None);
    }

    private static ShipMode ResolveMode(Ship ship)
    {
        if (!ship.IsActive || !ship.IsAlive)
        {
            return ShipMode.Sunk;
        }

        if (ship.ModeCode > (byte)ShipMode.CastingOff)
        {
            throw new InvalidOperationException("Ship mode code is corrupt.");
        }

        return (ShipMode)ship.ModeCode;
    }
}
