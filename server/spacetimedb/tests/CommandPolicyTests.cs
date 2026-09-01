using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CommandPolicyTests
{
    public static IEnumerable<object[]> ModeCommandCases()
    {
        foreach (var mode in Enum.GetValues<ShipMode>())
        {
            foreach (var command in Enum.GetValues<ShipCommandKind>())
            {
                yield return [mode, command];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModeCommandCases))]
    public void EveryCommandHasOneDecisionInEveryMode(ShipMode mode, ShipCommandKind command)
    {
        var decision = CommandPolicy.Evaluate(ValidSnapshot(mode), command);
        var expected = ExpectedAccepted(mode, command);

        Assert.Equal(expected, decision.Accepted);
        Assert.Equal(expected ? CommandRejectionCode.None : ExpectedModeRejection(mode),
            decision.Rejection);
    }

    [Theory]
    [InlineData(ShipCommandKind.StartRepair, ShipMode.Repairing)]
    [InlineData(ShipCommandKind.StartBoarding, ShipMode.Boarding)]
    [InlineData(ShipCommandKind.CancelChannel, ShipMode.Operational)]
    public void ChannelCommandsReturnTheNextMode(
        ShipCommandKind command,
        ShipMode expectedMode)
    {
        var initialMode = command == ShipCommandKind.CancelChannel
            ? ShipMode.Repairing
            : ShipMode.Operational;

        var decision = CommandPolicy.Evaluate(ValidSnapshot(initialMode), command);

        Assert.True(decision.Accepted);
        Assert.Equal(expectedMode, decision.NextMode);
    }

    [Theory]
    [InlineData(ShipCommandKind.SetCourse, CommandRejectionCode.InvalidCourse)]
    [InlineData(ShipCommandKind.SelectTarget, CommandRejectionCode.InvalidTarget)]
    [InlineData(ShipCommandKind.SetAmmo, CommandRejectionCode.UnknownAmmunition)]
    [InlineData(ShipCommandKind.FireBroadside, CommandRejectionCode.NoTarget)]
    [InlineData(ShipCommandKind.ActivateAbility, CommandRejectionCode.UnknownAbility)]
    [InlineData(ShipCommandKind.StartRepair, CommandRejectionCode.NoRepairKit)]
    [InlineData(ShipCommandKind.StartBoarding, CommandRejectionCode.TargetTooStrong)]
    public void InvalidGameplayStateReturnsAStableCode(
        ShipCommandKind command,
        CommandRejectionCode expected)
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            CourseValid = false,
            TargetValid = false,
            AmmoKnown = false,
            FireRejection = FireRejection.NoTarget,
            AbilityRejection = AbilityRejection.UnknownAbility,
            RepairRejection = RepairRejection.NoRepairKit,
            BoardingRejection = BoardingRejection.TargetTooStrong,
        };

        var decision = CommandPolicy.Evaluate(snapshot, command);

        Assert.False(decision.Accepted);
        Assert.Equal(expected, decision.Rejection);
        Assert.Equal(snapshot.Mode, decision.NextMode);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Fact]
    public void BlockedCourseIsRejectedWithoutAnEffect()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            DestinationBlocked = true,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.SetCourse);

        Assert.Equal(CommandRejectionCode.DestinationBlocked, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Theory]
    [InlineData(FireRejection.SourceSunk, CommandRejectionCode.Sunk)]
    [InlineData(FireRejection.NoTarget, CommandRejectionCode.NoTarget)]
    [InlineData(FireRejection.TargetSunk, CommandRejectionCode.TargetSunk)]
    [InlineData(FireRejection.CannonsDisabled, CommandRejectionCode.CannonsDisabled)]
    [InlineData(FireRejection.NoAmmunition, CommandRejectionCode.NoAmmunition)]
    [InlineData(FireRejection.Reloading, CommandRejectionCode.Reloading)]
    [InlineData(FireRejection.OutOfRange, CommandRejectionCode.OutOfRange)]
    [InlineData(FireRejection.OutsideArc, CommandRejectionCode.OutsideArc)]
    public void BroadsideFailuresHaveStableCommandCodes(
        FireRejection failure,
        CommandRejectionCode expected)
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = failure,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.FireBroadside);

        Assert.False(decision.Accepted);
        Assert.Equal(expected, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Theory]
    [InlineData(AbilityRejection.Cooldown, CommandRejectionCode.Cooldown)]
    [InlineData(AbilityRejection.UnknownAbility, CommandRejectionCode.UnknownAbility)]
    public void AbilityFailuresHaveStableCommandCodes(
        AbilityRejection failure,
        CommandRejectionCode expected)
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            AbilityRejection = failure,
        };

        Assert.Equal(
            expected,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.ActivateAbility).Rejection);
    }

    [Fact]
    public void PlayerAndNpcActorsReceiveTheSameDecisionFromTheSameSnapshot()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = FireRejection.Reloading,
        };

        var playerDecision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.FireBroadside);
        var npcDecision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.FireBroadside);

        Assert.Equal(playerDecision, npcDecision);
    }

    [Theory]
    [InlineData(0ul, 1ul, CommandSequenceDecision.Process)]
    [InlineData(4ul, 5ul, CommandSequenceDecision.Process)]
    [InlineData(4ul, 4ul, CommandSequenceDecision.Duplicate)]
    [InlineData(4ul, 3ul, CommandSequenceDecision.Stale)]
    [InlineData(0ul, 0ul, CommandSequenceDecision.Stale)]
    public void CommandIdsAreMonotonicAndRetrySafe(
        ulong lastProcessed,
        ulong requested,
        CommandSequenceDecision expected)
    {
        Assert.Equal(expected, CommandSequencePolicy.Evaluate(lastProcessed, requested));
    }

    private static CommandSnapshot ValidSnapshot(ShipMode mode) => new()
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
        HasActiveChannel = mode is ShipMode.Repairing or ShipMode.Boarding,
    };

    private static bool ExpectedAccepted(ShipMode mode, ShipCommandKind command) => mode switch
    {
        ShipMode.Operational => command != ShipCommandKind.CancelChannel,
        ShipMode.Repairing or ShipMode.Boarding => command is
            ShipCommandKind.SetCourse or
            ShipCommandKind.StopCourse or
            ShipCommandKind.SelectTarget or
            ShipCommandKind.ClearTarget or
            ShipCommandKind.CancelChannel,
        ShipMode.Sunk => false,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static CommandRejectionCode ExpectedModeRejection(ShipMode mode) => mode switch
    {
        ShipMode.Operational => CommandRejectionCode.NotChanneling,
        ShipMode.Repairing or ShipMode.Boarding => CommandRejectionCode.ModeConflict,
        ShipMode.Sunk => CommandRejectionCode.Sunk,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}
