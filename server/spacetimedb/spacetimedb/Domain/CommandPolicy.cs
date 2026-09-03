namespace Sea.Server;

public enum ShipMode : byte
{
    Operational = 0,
    Repairing = 1,
    Sunk = 2,
}

public enum ShipCommandKind : byte
{
    SetCourse = 0,
    StopCourse = 1,
    SelectTarget = 2,
    ClearTarget = 3,
    SetAmmo = 4,
    Fire = 5,
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
    NoTarget = 11,
    TargetSunk = 12,
    Reloading = 13,
    FiringTooFast = 14,
    OutOfRange = 15,
    InPort = 16,
    NoRepairKit = 17,
    NothingToRepair = 18,
    NotChanneling = 19,
    MissingResource = 20,
    NotAvailable = 21,
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
    Fire = 1 << 5,
    StartRepair = 1 << 6,
    CancelChannel = 1 << 7,
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
    public FireRejection FireRejection { get; init; }
    public RepairRejection RepairRejection { get; init; }
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
        if ((byte)command > (byte)ShipCommandKind.CancelChannel)
        {
            return Reject(snapshot.Mode, CommandRejectionCode.MissingResource);
        }

        // Abilities and boarding left the model with the four damage pools they scaled off. Their
        // keys stay bound on the client, so they answer "not available yet" rather than nothing.
        if (command is ShipCommandKind.ActivateAbility or ShipCommandKind.StartBoarding)
        {
            return Reject(snapshot.Mode, CommandRejectionCode.NotAvailable);
        }

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
        ShipMode.Repairing => command is
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
            ShipCommandKind.SetAmmo => snapshot.AmmoKnown
                ? CommandRejectionCode.None
                : CommandRejectionCode.UnknownAmmunition,
            ShipCommandKind.Fire when snapshot.TargetIsFriendly =>
                CommandRejectionCode.PlayerTargetForbidden,
            ShipCommandKind.Fire => Map(snapshot.FireRejection),
            ShipCommandKind.StartRepair => Map(snapshot.RepairRejection),
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

    private static ShipMode NextMode(ShipMode current, ShipCommandKind command) => command switch
    {
        ShipCommandKind.StartRepair => ShipMode.Repairing,
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
        ShipCommandKind.Fire => CommandEffect.Fire,
        ShipCommandKind.StartRepair => CommandEffect.StartRepair,
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
        FireRejection.Reloading => CommandRejectionCode.Reloading,
        FireRejection.FiringTooFast => CommandRejectionCode.FiringTooFast,
        FireRejection.OutOfRange => CommandRejectionCode.OutOfRange,
        FireRejection.InPort => CommandRejectionCode.InPort,
        FireRejection.Busy => CommandRejectionCode.ModeConflict,
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
}
