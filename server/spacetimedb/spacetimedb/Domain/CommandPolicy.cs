namespace Sea.Server;

public enum ShipMode : byte
{
    Operational = 0,
    Repairing = 1,
    Boarding = 2,
    Sunk = 3,
}

public enum ShipCommandKind : byte
{
    SetCourse = 0,
    StopCourse = 1,
    SelectTarget = 2,
    ClearTarget = 3,
    SetAmmo = 4,
    FireBroadside = 5,
    ActivateAbility = 6,
    StartRepair = 7,
    StartBoarding = 8,
    CancelChannel = 9,
}

public enum CommandRejectionCode : byte
{
    None = 0,
    StaleCommand = 1,
    PlayerNotLoaded = 2,
    ModeConflict = 3,
    Sunk = 4,
    InvalidCourse = 5,
    DestinationBlocked = 6,
    InvalidTarget = 7,
    PlayerTargetForbidden = 8,
    TargetConcealed = 9,
    UnknownAmmunition = 10,
    AmmunitionNotOwned = 11,
    NoTarget = 12,
    TargetSunk = 13,
    CannonsDisabled = 14,
    NoAmmunition = 15,
    Reloading = 16,
    OutOfRange = 17,
    OutsideArc = 18,
    UnknownAbility = 19,
    Cooldown = 20,
    NoRepairKit = 21,
    NothingToRepair = 22,
    TargetTooStrong = 23,
    NotChanneling = 24,
    MissingResource = 25,
    InvalidBroadsideSide = 26,
    InvalidWeakPoint = 27,
}

[Flags]
public enum CommandEffect : ushort
{
    None = 0,
    SetCourse = 1 << 0,
    StopCourse = 1 << 1,
    SelectTarget = 1 << 2,
    ClearTarget = 1 << 3,
    SetAmmo = 1 << 4,
    FireBroadside = 1 << 5,
    ActivateAbility = 1 << 6,
    StartRepair = 1 << 7,
    StartBoarding = 1 << 8,
    CancelChannel = 1 << 9,
}

public readonly record struct CommandSnapshot
{
    public ShipMode Mode { get; init; }
    public bool CourseValid { get; init; }
    public bool DestinationBlocked { get; init; }
    public bool TargetValid { get; init; }
    public bool TargetIsFriendly { get; init; }
    public bool TargetConcealed { get; init; }
    public bool AmmoKnown { get; init; }
    public bool AmmoOwned { get; init; }
    public FireRejection FireRejection { get; init; }
    public AbilityRejection AbilityRejection { get; init; }
    public RepairRejection RepairRejection { get; init; }
    public BoardingRejection BoardingRejection { get; init; }
    public bool HasActiveChannel { get; init; }
    public CommandRejectionCode ArgumentRejection { get; init; }
}

public readonly record struct CommandDecision(
    bool Accepted,
    CommandRejectionCode Rejection,
    ShipMode NextMode,
    CommandEffect Effects);

public enum CommandSequenceDecision : byte
{
    Process = 0,
    Duplicate = 1,
    Stale = 2,
}

public static class CommandSequencePolicy
{
    public static CommandSequenceDecision Evaluate(ulong lastProcessed, ulong requested)
    {
        if (requested == 0 || requested < lastProcessed)
        {
            return CommandSequenceDecision.Stale;
        }

        return requested == lastProcessed && lastProcessed != 0
            ? CommandSequenceDecision.Duplicate
            : CommandSequenceDecision.Process;
    }
}

public static class CommandPolicy
{
    public static CommandDecision Evaluate(
        CommandSnapshot snapshot,
        ShipCommandKind command)
    {
        if (snapshot.Mode == ShipMode.Sunk)
        {
            return Reject(snapshot.Mode, CommandRejectionCode.Sunk);
        }

        if (!ModeAllows(snapshot.Mode, command))
        {
            var code = command == ShipCommandKind.CancelChannel
                ? CommandRejectionCode.NotChanneling
                : CommandRejectionCode.ModeConflict;
            return Reject(snapshot.Mode, code);
        }

        var rejection = snapshot.ArgumentRejection != CommandRejectionCode.None
            ? snapshot.ArgumentRejection
            : Validate(snapshot, command);
        if (rejection != CommandRejectionCode.None)
        {
            return Reject(snapshot.Mode, rejection);
        }

        return new CommandDecision(
            true,
            CommandRejectionCode.None,
            NextMode(snapshot.Mode, command),
            EffectFor(command));
    }

    private static bool ModeAllows(ShipMode mode, ShipCommandKind command) => mode switch
    {
        ShipMode.Operational => command != ShipCommandKind.CancelChannel,
        ShipMode.Repairing or ShipMode.Boarding => command is
            ShipCommandKind.SetCourse or
            ShipCommandKind.StopCourse or
            ShipCommandKind.SelectTarget or
            ShipCommandKind.ClearTarget or
            ShipCommandKind.CancelChannel,
        _ => false,
    };

    private static CommandRejectionCode Validate(
        CommandSnapshot snapshot,
        ShipCommandKind command) => command switch
        {
            ShipCommandKind.SetCourse => ValidateCourse(snapshot),
            ShipCommandKind.SelectTarget => ValidateTarget(snapshot),
            ShipCommandKind.SetAmmo => ValidateAmmo(snapshot),
            ShipCommandKind.FireBroadside when snapshot.TargetIsFriendly =>
                CommandRejectionCode.PlayerTargetForbidden,
            ShipCommandKind.FireBroadside => Map(snapshot.FireRejection),
            ShipCommandKind.ActivateAbility => Map(snapshot.AbilityRejection),
            ShipCommandKind.StartRepair => Map(snapshot.RepairRejection),
            ShipCommandKind.StartBoarding when snapshot.TargetIsFriendly =>
                CommandRejectionCode.PlayerTargetForbidden,
            ShipCommandKind.StartBoarding => Map(snapshot.BoardingRejection),
            ShipCommandKind.CancelChannel when !snapshot.HasActiveChannel =>
                CommandRejectionCode.NotChanneling,
            _ => CommandRejectionCode.None,
        };

    private static CommandRejectionCode ValidateCourse(CommandSnapshot snapshot)
    {
        if (!snapshot.CourseValid)
        {
            return CommandRejectionCode.InvalidCourse;
        }

        return snapshot.DestinationBlocked
            ? CommandRejectionCode.DestinationBlocked
            : CommandRejectionCode.None;
    }

    private static CommandRejectionCode ValidateTarget(CommandSnapshot snapshot)
    {
        if (!snapshot.TargetValid)
        {
            return CommandRejectionCode.InvalidTarget;
        }

        return snapshot.TargetConcealed
            ? CommandRejectionCode.TargetConcealed
            : CommandRejectionCode.None;
    }

    private static CommandRejectionCode ValidateAmmo(CommandSnapshot snapshot)
    {
        if (!snapshot.AmmoKnown)
        {
            return CommandRejectionCode.UnknownAmmunition;
        }

        return snapshot.AmmoOwned
            ? CommandRejectionCode.None
            : CommandRejectionCode.AmmunitionNotOwned;
    }

    private static ShipMode NextMode(ShipMode current, ShipCommandKind command) => command switch
    {
        ShipCommandKind.StartRepair => ShipMode.Repairing,
        ShipCommandKind.StartBoarding => ShipMode.Boarding,
        ShipCommandKind.CancelChannel => ShipMode.Operational,
        _ => current,
    };

    private static CommandEffect EffectFor(ShipCommandKind command) => command switch
    {
        ShipCommandKind.SetCourse => CommandEffect.SetCourse,
        ShipCommandKind.StopCourse => CommandEffect.StopCourse,
        ShipCommandKind.SelectTarget => CommandEffect.SelectTarget,
        ShipCommandKind.ClearTarget => CommandEffect.ClearTarget,
        ShipCommandKind.SetAmmo => CommandEffect.SetAmmo,
        ShipCommandKind.FireBroadside => CommandEffect.FireBroadside,
        ShipCommandKind.ActivateAbility => CommandEffect.ActivateAbility,
        ShipCommandKind.StartRepair => CommandEffect.StartRepair,
        ShipCommandKind.StartBoarding => CommandEffect.StartBoarding,
        ShipCommandKind.CancelChannel => CommandEffect.CancelChannel,
        _ => CommandEffect.None,
    };

    private static CommandDecision Reject(ShipMode mode, CommandRejectionCode code) =>
        new(false, code, mode, CommandEffect.None);

    private static CommandRejectionCode Map(FireRejection rejection) => rejection switch
    {
        FireRejection.None => CommandRejectionCode.None,
        FireRejection.SourceSunk => CommandRejectionCode.Sunk,
        FireRejection.NoTarget => CommandRejectionCode.NoTarget,
        FireRejection.TargetSunk => CommandRejectionCode.TargetSunk,
        FireRejection.CannonsDisabled => CommandRejectionCode.CannonsDisabled,
        FireRejection.NoAmmunition => CommandRejectionCode.NoAmmunition,
        FireRejection.Reloading => CommandRejectionCode.Reloading,
        FireRejection.OutOfRange => CommandRejectionCode.OutOfRange,
        FireRejection.OutsideArc => CommandRejectionCode.OutsideArc,
        FireRejection.Busy => CommandRejectionCode.ModeConflict,
        _ => CommandRejectionCode.MissingResource,
    };

    private static CommandRejectionCode Map(AbilityRejection rejection) => rejection switch
    {
        AbilityRejection.None => CommandRejectionCode.None,
        AbilityRejection.SourceSunk => CommandRejectionCode.Sunk,
        AbilityRejection.UnknownAbility => CommandRejectionCode.UnknownAbility,
        AbilityRejection.Cooldown => CommandRejectionCode.Cooldown,
        AbilityRejection.Busy => CommandRejectionCode.ModeConflict,
        _ => CommandRejectionCode.MissingResource,
    };

    private static CommandRejectionCode Map(RepairRejection rejection) => rejection switch
    {
        RepairRejection.None => CommandRejectionCode.None,
        RepairRejection.SourceSunk => CommandRejectionCode.Sunk,
        RepairRejection.Busy => CommandRejectionCode.ModeConflict,
        RepairRejection.NoRepairKit => CommandRejectionCode.NoRepairKit,
        RepairRejection.NothingToRepair => CommandRejectionCode.NothingToRepair,
        _ => CommandRejectionCode.MissingResource,
    };

    private static CommandRejectionCode Map(BoardingRejection rejection) => rejection switch
    {
        BoardingRejection.None => CommandRejectionCode.None,
        BoardingRejection.SourceSunk => CommandRejectionCode.Sunk,
        BoardingRejection.TargetSunk => CommandRejectionCode.TargetSunk,
        BoardingRejection.Busy => CommandRejectionCode.ModeConflict,
        BoardingRejection.TargetTooStrong => CommandRejectionCode.TargetTooStrong,
        BoardingRejection.OutOfRange => CommandRejectionCode.OutOfRange,
        BoardingRejection.Cooldown => CommandRejectionCode.Cooldown,
        _ => CommandRejectionCode.MissingResource,
    };
}
