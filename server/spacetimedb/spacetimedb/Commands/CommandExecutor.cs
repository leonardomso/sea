using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private readonly record struct DecodedCommand(
        ShipCommandKind Kind,
        SetCourseCommand SetCourse = default,
        SelectTargetCommand SelectTarget = default,
        SetAmmoCommand SetAmmo = default);

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
            HasActiveChannel = FindActiveChannel(ctx, ship.EntityId) is not null,
        };

        return command.Kind switch
        {
            ShipCommandKind.SetCourse => CourseSnapshot(ctx, world, snapshot, command.SetCourse),
            ShipCommandKind.SelectTarget => TargetSnapshot(
                ctx,
                ship,
                snapshot,
                command.SelectTarget),
            ShipCommandKind.SetAmmo => AmmoSnapshot(snapshot, command.SetAmmo),
            ShipCommandKind.Fire => FireSnapshot(ctx, world, ship, snapshot),
            ShipCommandKind.StartRepair => RepairSnapshot(ctx, ship, snapshot),
            _ => snapshot,
        };
    }

    private static void ApplyAcceptedCommand(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        DecodedCommand command,
        CommandDecision decision)
    {
        switch (command.Kind)
        {
            case ShipCommandKind.SetCourse:
                ApplySetCourse(ctx, world, ref ship, command.SetCourse);
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
                ApplyCancelChannel(ctx, world, ship);
                break;
            default:
                throw new InvalidOperationException("Accepted command has no executor.");
        }

        ship.ModeCode = (byte)decision.NextMode;
        PersistCommandShip(ctx, ship, world.Tick);
    }

    private static ShipMode ResolveMode(Ship ship)
    {
        if (!ship.IsActive || !ship.IsAlive)
        {
            return ShipMode.Sunk;
        }

        if (ship.ModeCode > (byte)ShipMode.Sunk)
        {
            throw new InvalidOperationException("Ship mode code is corrupt.");
        }

        return (ShipMode)ship.ModeCode;
    }
}
