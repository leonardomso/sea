using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private readonly record struct DecodedCommand(
        ShipCommandKind Kind,
        SetCourseCommand SetCourse = default,
        SelectTargetCommand SelectTarget = default,
        SetAmmoCommand SetAmmo = default,
        FireBroadsideCommand FireBroadside = default,
        ActivateAbilityCommand ActivateAbility = default);

    private static DecodedCommand DecodeCommand(ShipCommand command) => command switch
    {
        ShipCommand.SetCourse(var value) => new(ShipCommandKind.SetCourse, SetCourse: value),
        ShipCommand.StopCourse => new(ShipCommandKind.StopCourse),
        ShipCommand.SelectTarget(var value) => new(
            ShipCommandKind.SelectTarget,
            SelectTarget: value),
        ShipCommand.ClearTarget => new(ShipCommandKind.ClearTarget),
        ShipCommand.SetAmmo(var value) => new(ShipCommandKind.SetAmmo, SetAmmo: value),
        ShipCommand.FireBroadside(var value) => new(
            ShipCommandKind.FireBroadside,
            FireBroadside: value),
        ShipCommand.ActivateAbility(var value) => new(
            ShipCommandKind.ActivateAbility,
            ActivateAbility: value),
        ShipCommand.StartRepair => new(ShipCommandKind.StartRepair),
        ShipCommand.StartBoarding => new(ShipCommandKind.StartBoarding),
        ShipCommand.CancelChannel => new(ShipCommandKind.CancelChannel),
        _ => throw new InvalidOperationException("Unknown ship command variant."),
    };

    private static CommandSnapshot BuildCommandSnapshot(
        ReducerContext ctx,
        Ship ship,
        DecodedCommand command)
    {
        var mode = ResolveMode(ship);
        var snapshot = new CommandSnapshot
        {
            Mode = mode,
            CourseValid = true,
            TargetValid = true,
            AmmoKnown = true,
            AmmoOwned = true,
            FireRejection = FireRejection.None,
            AbilityRejection = AbilityRejection.None,
            RepairRejection = RepairRejection.None,
            BoardingRejection = BoardingRejection.None,
            HasActiveChannel = FindActiveChannel(ctx, ship.EntityId) is not null,
        };

        return command.Kind switch
        {
            ShipCommandKind.SetCourse => CourseSnapshot(ctx, snapshot, command.SetCourse),
            ShipCommandKind.SelectTarget => TargetSnapshot(ctx, ship, snapshot, command.SelectTarget),
            ShipCommandKind.SetAmmo => AmmoSnapshot(ctx, ship, snapshot, command.SetAmmo),
            ShipCommandKind.FireBroadside => FireSnapshot(ctx, ship, snapshot, command.FireBroadside),
            ShipCommandKind.ActivateAbility => AbilitySnapshot(
                ctx,
                ship,
                snapshot,
                command.ActivateAbility),
            ShipCommandKind.StartRepair => RepairSnapshot(ctx, ship, snapshot),
            ShipCommandKind.StartBoarding => BoardingSnapshot(ctx, ship, snapshot),
            _ => snapshot,
        };
    }

    private static void ApplyAcceptedCommand(
        ReducerContext ctx,
        ref Ship ship,
        DecodedCommand command,
        CommandDecision decision)
    {
        switch (command.Kind)
        {
            case ShipCommandKind.SetCourse:
                ApplySetCourse(ctx, ref ship, command.SetCourse);
                break;
            case ShipCommandKind.StopCourse:
                ApplyStopCourse(ctx, ref ship);
                break;
            case ShipCommandKind.SelectTarget:
                ApplySelectTarget(ctx, ref ship, command.SelectTarget);
                break;
            case ShipCommandKind.ClearTarget:
                ApplyClearTarget(ctx, ref ship);
                break;
            case ShipCommandKind.SetAmmo:
                ApplySetAmmo(ctx, ref ship, command.SetAmmo);
                break;
            case ShipCommandKind.FireBroadside:
                if (!CombatRules.TryParseWeakPoint(
                        command.FireBroadside.WeakPoint,
                        out var weakPoint) ||
                    !Enum.TryParse<BroadsideSide>(
                    command.FireBroadside.Side,
                    ignoreCase: true,
                    out var side))
                {
                    throw new InvalidOperationException("Accepted broadside arguments are invalid.");
                }
                ApplyFireBroadside(ctx, ref ship, command.FireBroadside, side, weakPoint);
                break;
            case ShipCommandKind.ActivateAbility:
                ApplyActivateAbility(ctx, ref ship, command.ActivateAbility);
                break;
            case ShipCommandKind.StartRepair:
                ApplyStartRepair(ctx, ref ship);
                break;
            case ShipCommandKind.StartBoarding:
                ApplyStartBoarding(ctx, ref ship);
                break;
            case ShipCommandKind.CancelChannel:
                ApplyCancelChannel(ctx, ship);
                break;
            default:
                throw new InvalidOperationException("Accepted command has no executor.");
        }

        ship.ModeCode = (byte)decision.NextMode;
        ctx.Db.Ship.EntityId.Update(ship);
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
