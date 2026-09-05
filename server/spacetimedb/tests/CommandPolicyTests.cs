using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CommandPolicyTests
{
    /// <summary>Abilities and boarding left the model in 1b but keep their keys bound.</summary>
    private static readonly ShipCommandKind[] Retired =
    [
        ShipCommandKind.ActivateAbility,
        ShipCommandKind.StartBoarding,
    ];

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
        Assert.Equal(
            expected ? CommandRejectionCode.None : ExpectedRejection(mode, command),
            decision.Rejection);
    }

    [Theory]
    [InlineData(ShipCommandKind.ActivateAbility)]
    [InlineData(ShipCommandKind.StartBoarding)]
    public void RetiredCommandsAnswerNotAvailableInEveryMode(ShipCommandKind command)
    {
        foreach (var mode in Enum.GetValues<ShipMode>())
        {
            var decision = CommandPolicy.Evaluate(ValidSnapshot(mode), command);

            Assert.False(decision.Accepted);
            Assert.Equal(CommandRejectionCode.NotAvailable, decision.Rejection);
            Assert.Equal(mode, decision.NextMode);
            Assert.Equal(CommandEffect.None, decision.Effects);
        }
    }

    [Theory]
    [InlineData(ShipCommandKind.StartRepair, ShipMode.Repairing)]
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
    [InlineData(ShipCommandKind.Fire, CommandRejectionCode.NoTarget)]
    [InlineData(ShipCommandKind.StartRepair, CommandRejectionCode.NoRepairKit)]
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
            RepairRejection = RepairRejection.NoRepairKit,
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

    [Fact]
    public void PlayerShipCanBeSelectedForInspection()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            TargetIsFriendly = true,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.SelectTarget);

        Assert.True(decision.Accepted);
        Assert.Equal(CommandEffect.SelectTarget, decision.Effects);
    }

    [Fact]
    public void PlayerShipCannotBeFiredOn()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            TargetIsFriendly = true,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.Fire);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.PlayerTargetForbidden, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Theory]
    [InlineData(FireRejection.SourceSunk, CommandRejectionCode.Sunk)]
    [InlineData(FireRejection.NoTarget, CommandRejectionCode.NoTarget)]
    [InlineData(FireRejection.TargetSunk, CommandRejectionCode.TargetSunk)]
    [InlineData(FireRejection.Reloading, CommandRejectionCode.Reloading)]
    [InlineData(FireRejection.FiringTooFast, CommandRejectionCode.FiringTooFast)]
    [InlineData(FireRejection.OutOfRange, CommandRejectionCode.OutOfRange)]
    [InlineData(FireRejection.InPort, CommandRejectionCode.InPort)]
    public void VolleyFailuresHaveStableCommandCodes(
        FireRejection failure,
        CommandRejectionCode expected)
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = failure,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.Fire);

        Assert.False(decision.Accepted);
        Assert.Equal(expected, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Fact]
    public void AnUnmappedFireFailureIsReportedRatherThanAccepted()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = (FireRejection)byte.MaxValue,
        };

        Assert.Equal(
            CommandRejectionCode.MissingResource,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.Fire).Rejection);
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

    [Fact]
    public void ArgumentValidationTakesPrecedenceWithoutApplyingAnEffect()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            ArgumentRejection = CommandRejectionCode.UnknownAmmunition,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.Fire);

        Assert.Equal(CommandRejectionCode.UnknownAmmunition, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Fact]
    public void ConcealedTargetsHaveAStableCode()
    {
        var concealed = ValidSnapshot(ShipMode.Operational) with { TargetConcealed = true };

        Assert.Equal(
            CommandRejectionCode.TargetConcealed,
            CommandPolicy.Evaluate(concealed, ShipCommandKind.SelectTarget).Rejection);
    }

    [Theory]
    [InlineData(true, false, ShipCommandKind.Fire)]
    [InlineData(false, true, ShipCommandKind.StartRepair)]
    public void BusySubsystemsMapToModeConflict(bool fire, bool repair, ShipCommandKind command)
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = fire ? FireRejection.Busy : FireRejection.None,
            RepairRejection = repair ? RepairRejection.Busy : RepairRejection.None,
        };

        Assert.Equal(
            CommandRejectionCode.ModeConflict,
            CommandPolicy.Evaluate(snapshot, command).Rejection);
    }

    [Fact]
    public void CancellingWithoutAChannelIsRejectedEvenWhenTheModeAllowsIt()
    {
        var snapshot = ValidSnapshot(ShipMode.Repairing) with { HasActiveChannel = false };

        Assert.Equal(
            CommandRejectionCode.NotChanneling,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.CancelChannel).Rejection);
    }

    [Fact]
    public void UnknownCommandCodeIsRejectedAsCorruptInput()
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Operational),
            (ShipCommandKind)byte.MaxValue);

        Assert.Equal(CommandRejectionCode.MissingResource, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Fact]
    public void UnknownShipModeCannotAuthorizeACommand()
    {
        var snapshot = ValidSnapshot((ShipMode)byte.MaxValue);

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.SetCourse);

        Assert.Equal(CommandRejectionCode.ModeConflict, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    [Theory]
    [InlineData(ShipMode.Repairing)]
    [InlineData(ShipMode.CastingOff)]
    public void TheKitIsTheOneHealAShipAlreadyChannellingCanStillReachFor(ShipMode mode)
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(mode),
            ShipCommandKind.UseRepairKit);

        Assert.True(decision.Accepted);
        Assert.Equal(CommandEffect.UseRepairKit, decision.Effects);
        Assert.Equal(mode, decision.NextMode);
    }

    [Fact]
    public void TheKitAndTheChannelAnswerFromCooldownsOfTheirOwn()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            RepairRejection = RepairRejection.OnCooldown,
            KitRejection = RepairRejection.None,
        };

        Assert.Equal(
            CommandRejectionCode.OnCooldown,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.StartRepair).Rejection);
        Assert.True(CommandPolicy.Evaluate(snapshot, ShipCommandKind.UseRepairKit).Accepted);
    }

    [Fact]
    public void TheSpawnShieldIsSpentBySailingOutRatherThanByShooting()
    {
        var snapshot = ValidSnapshot(ShipMode.Operational) with
        {
            FireRejection = FireRejection.SpawnShielded,
        };

        Assert.Equal(
            CommandRejectionCode.SpawnShielded,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.Fire).Rejection);
    }

    [Fact]
    public void AWreckComesBackOnlyAfterItHasAskedForABerth()
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Sunk),
            ShipCommandKind.ChooseRespawn);

        Assert.True(decision.Accepted);
        Assert.Equal(CommandEffect.ChooseRespawn, decision.Effects);
        Assert.Equal(ShipMode.Sunk, decision.NextMode);
    }

    [Fact]
    public void AWreckThatHasAlreadyChosenIsNotAskedTwice()
    {
        var snapshot = ValidSnapshot(ShipMode.Sunk) with { RespawnPending = false };

        Assert.Equal(
            CommandRejectionCode.NotAvailable,
            CommandPolicy.Evaluate(snapshot, ShipCommandKind.ChooseRespawn).Rejection);
    }

    [Fact]
    public void ABerthThePortDoesNotOfferIsRefused()
    {
        var snapshot = ValidSnapshot(ShipMode.Sunk) with
        {
            ArgumentRejection = CommandRejectionCode.NotAvailable,
        };

        var decision = CommandPolicy.Evaluate(snapshot, ShipCommandKind.ChooseRespawn);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.NotAvailable, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    private static CommandSnapshot ValidSnapshot(ShipMode mode) => new()
    {
        Mode = mode,
        CourseValid = true,
        TargetValid = true,
        AmmoKnown = true,
        FireRejection = FireRejection.None,
        RepairRejection = RepairRejection.None,
        KitRejection = RepairRejection.None,
        HasActiveChannel = mode is ShipMode.Repairing or ShipMode.CastingOff,
        RespawnPending = mode == ShipMode.Sunk,
        CrossingOffered = true,
    };

    private static bool ExpectedAccepted(ShipMode mode, ShipCommandKind command)
    {
        if (Array.IndexOf(Retired, command) >= 0)
        {
            return false;
        }

        // Choosing a berth is answered before the mode gate, so it is the one order a wreck may
        // give and the one order every other mode may not.
        if (command == ShipCommandKind.ChooseRespawn)
        {
            return mode == ShipMode.Sunk;
        }

        return mode switch
        {
            ShipMode.Operational => command != ShipCommandKind.CancelChannel,
            ShipMode.Repairing or ShipMode.CastingOff => command is
                ShipCommandKind.SetCourse or
                ShipCommandKind.StopCourse or
                ShipCommandKind.SelectTarget or
                ShipCommandKind.ClearTarget or
                ShipCommandKind.UseRepairKit or
                ShipCommandKind.CancelChannel,
            ShipMode.Sunk => false,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    private static CommandRejectionCode ExpectedRejection(ShipMode mode, ShipCommandKind command)
    {
        if (Array.IndexOf(Retired, command) >= 0)
        {
            return CommandRejectionCode.NotAvailable;
        }

        if (command == ShipCommandKind.ChooseRespawn)
        {
            return CommandRejectionCode.NotSunk;
        }

        return mode switch
        {
            ShipMode.Operational => CommandRejectionCode.NotChanneling,
            ShipMode.Repairing or ShipMode.CastingOff => CommandRejectionCode.ModeConflict,
            ShipMode.Sunk => CommandRejectionCode.Sunk,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    /// <summary>
    /// The client reads these numbers off the wire, so their values are the contract,
    /// not their names. A course that cannot be plotted is refused whole (SEA_5 4.1.5)
    /// rather than half-obeyed, and the ninth click in a second is dropped (4.1.8).
    /// </summary>
    [Fact]
    public void ACourseIntoALandLockedLakeIsRejectedWithNoPath()
    {
        Assert.Equal(25, (int)CommandRejectionCode.NoPath);
    }

    [Fact]
    public void TooManyCoursesInOneSecondAreRejectedAsRateLimited()
    {
        Assert.Equal(26, (int)CommandRejectionCode.RateLimited);
    }

    /// <summary>
    /// SEA_5 §10.2: reaching a border raises a prompt, and the crossing happens when the captain
    /// confirms it. Confirming with no prompt standing is not a crossing that failed -- it is an
    /// order for something that was never offered.
    /// </summary>
    [Fact]
    public void ChangingMapNeedsAnOfferStanding()
    {
        var offered = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Operational),
            ShipCommandKind.ChangeMap);

        Assert.True(offered.Accepted);
        Assert.Equal(CommandEffect.ChangeMap, offered.Effects);
        Assert.Equal(ShipMode.Operational, offered.NextMode);
    }

    [Fact]
    public void ChangingMapWithNoOfferStandingIsRefused()
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Operational) with { CrossingOffered = false },
            ShipCommandKind.ChangeMap);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.NoCrossingOffered, decision.Rejection);
    }

    /// <summary>
    /// A hull under repair or still warping out of port is not sailing, so she is not at a border
    /// to be asked, and an order that says otherwise is a client that lost track of her.
    /// </summary>
    [Theory]
    [InlineData(ShipMode.Repairing)]
    [InlineData(ShipMode.CastingOff)]
    public void AShipBusyWithSomethingElseCannotCross(ShipMode mode)
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Operational) with { Mode = mode, CrossingOffered = true },
            ShipCommandKind.ChangeMap);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.ModeConflict, decision.Rejection);
    }

    [Fact]
    public void AWreckCannotCross()
    {
        var decision = CommandPolicy.Evaluate(
            ValidSnapshot(ShipMode.Operational) with { Mode = ShipMode.Sunk, CrossingOffered = true },
            ShipCommandKind.ChangeMap);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.Sunk, decision.Rejection);
    }
}
