using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Boarding stopped answering "not available yet" in Phase 11. What admission does with the
/// answer <see cref="BoardingRules.Validate"/> gives it is pinned here; the answer itself is
/// pinned in <see cref="BoardingGateTests"/>.
/// </summary>
public sealed class CommandPolicyBoardingTests
{
    private static CommandSnapshot Ready(
        BoardingRejection rejection = BoardingRejection.None,
        ShipMode mode = ShipMode.Operational) => new()
    {
        Mode = mode,
        CourseValid = true,
        TargetValid = true,
        AmmoKnown = true,
        BoardingRejection = rejection,
    };

    [Fact]
    public void ABoardingThatPassedTheGateIsAccepted()
    {
        var decision = CommandPolicy.Evaluate(Ready(), ShipCommandKind.StartBoarding);

        Assert.True(decision.Accepted);
        Assert.Equal(CommandRejectionCode.None, decision.Rejection);
        Assert.Equal(CommandEffect.StartBoarding, decision.Effects);
        Assert.Equal(ShipMode.Operational, decision.NextMode);
    }

    [Theory]
    [InlineData(BoardingRejection.SourceSunk, CommandRejectionCode.Sunk)]
    [InlineData(BoardingRejection.NoTarget, CommandRejectionCode.NoTarget)]
    [InlineData(BoardingRejection.TargetSunk, CommandRejectionCode.TargetSunk)]
    [InlineData(BoardingRejection.InPort, CommandRejectionCode.InPort)]
    [InlineData(BoardingRejection.OutOfRange, CommandRejectionCode.OutOfRange)]
    [InlineData(BoardingRejection.TargetNotBoardable, CommandRejectionCode.TargetNotBoardable)]
    [InlineData(BoardingRejection.NotEnoughHands, CommandRejectionCode.NotEnoughHands)]
    [InlineData(BoardingRejection.OnCooldown, CommandRejectionCode.OnCooldown)]
    [InlineData(BoardingRejection.TargetRecentlyBoarded, CommandRejectionCode.TargetRecentlyBoarded)]
    public void EveryRefusalReachesTheCaptainAsItsOwnReason(
        BoardingRejection rejection,
        CommandRejectionCode expected)
    {
        var decision = CommandPolicy.Evaluate(Ready(rejection), ShipCommandKind.StartBoarding);

        Assert.False(decision.Accepted);
        Assert.Equal(expected, decision.Rejection);
        Assert.Equal(CommandEffect.None, decision.Effects);
    }

    /// <summary>
    /// The client reads these off the wire, so the numbers are the contract and not the names.
    /// </summary>
    [Fact]
    public void TheNewRefusalsKeepTheNumbersTheClientWasGiven()
    {
        Assert.Equal(28, (int)CommandRejectionCode.TargetNotBoardable);
        Assert.Equal(29, (int)CommandRejectionCode.TargetRecentlyBoarded);
        Assert.Equal(30, (int)CommandRejectionCode.NotEnoughHands);
    }

    [Fact]
    public void ACrewHoldingStationForARepairIsNotThrowingHooks()
    {
        foreach (var mode in new[] { ShipMode.Repairing, ShipMode.CastingOff })
        {
            var decision = CommandPolicy.Evaluate(
                Ready(mode: mode),
                ShipCommandKind.StartBoarding);

            Assert.False(decision.Accepted);
            Assert.Equal(CommandRejectionCode.ModeConflict, decision.Rejection);
        }
    }

    [Fact]
    public void AWreckDoesNotBoard()
    {
        var decision = CommandPolicy.Evaluate(
            Ready(mode: ShipMode.Sunk),
            ShipCommandKind.StartBoarding);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.Sunk, decision.Rejection);
    }
    /// <summary>
    /// You do not throw hooks at your own side. It is the same guard firing has, and it sits ahead
    /// of the boarding gate so a captain is told who she aimed at rather than how far away he was.
    /// </summary>
    [Fact]
    public void HooksAreNotThrownAtYourOwnSide()
    {
        var decision = CommandPolicy.Evaluate(
            Ready() with { TargetIsFriendly = true },
            ShipCommandKind.StartBoarding);

        Assert.False(decision.Accepted);
        Assert.Equal(CommandRejectionCode.PlayerTargetForbidden, decision.Rejection);
    }

}
