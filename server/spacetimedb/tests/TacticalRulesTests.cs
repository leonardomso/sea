using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class TacticalRulesTests
{
    [Fact]
    public void MovementModifiers_LeaveACleanHullAtFullSpeed()
    {
        var modifiers = TacticalRules.MovementModifiers(
            slowed: false,
            slowMagnitude: 0f,
            inShoal: false,
            inStorm: false,
            repairing: false);

        Assert.Equal(1f, modifiers.MaximumSpeed);
        Assert.Equal(1f, modifiers.Acceleration);
        Assert.Equal(1f, modifiers.TurnRate);
        Assert.Equal(1f, modifiers.WeaponEffectiveness);
    }

    [Theory]
    [InlineData(0.3f, 0.7f)]
    [InlineData(0.9f, 0.1f)]
    [InlineData(1.5f, EffectRules.MinimumSpeedMultiplier)]
    public void ChainShotSlowsAShipButNeverStopsIt(float magnitude, float expected)
    {
        var modifiers = TacticalRules.MovementModifiers(
            slowed: true,
            slowMagnitude: magnitude,
            inShoal: false,
            inStorm: false,
            repairing: false);

        Assert.Equal(expected, modifiers.MaximumSpeed, 4);
    }

    [Fact]
    public void ShoalsStormsAndRepairsStackOntoTheSameHull()
    {
        var modifiers = TacticalRules.MovementModifiers(
            slowed: true,
            slowMagnitude: 0.5f,
            inShoal: true,
            inStorm: true,
            repairing: true);

        // 0.5 slow, then the shoal, then holding station: each multiplies the one before it.
        Assert.Equal(0.5f * 0.65f * 0.5f, modifiers.MaximumSpeed, 4);
        Assert.Equal(0.65f, modifiers.TurnRate);
        Assert.Equal(0.75f, modifiers.WeaponEffectiveness);
    }

    [Fact]
    public void AStormBitesHandlingAndGunneryWithoutTouchingTopSpeed()
    {
        var modifiers = TacticalRules.MovementModifiers(
            slowed: false,
            slowMagnitude: 0f,
            inShoal: false,
            inStorm: true,
            repairing: false);

        Assert.Equal(1f, modifiers.MaximumSpeed);
        Assert.Equal(0.65f, modifiers.TurnRate);
        Assert.Equal(0.75f, modifiers.WeaponEffectiveness);
    }

    [Theory]
    [InlineData(false, true, true, true, RepairRejection.SourceSunk)]
    [InlineData(true, false, true, true, RepairRejection.Busy)]
    [InlineData(true, true, false, true, RepairRejection.NoRepairKit)]
    [InlineData(true, true, true, false, RepairRejection.NothingToRepair)]
    [InlineData(true, true, true, true, RepairRejection.None)]
    public void RepairValidation_ChecksTheHullBeforeTheKit(
        bool alive,
        bool idle,
        bool hasKit,
        bool damaged,
        RepairRejection expected)
    {
        Assert.Equal(expected, TacticalRules.ValidateRepair(
            new RepairRequest(alive, idle, hasKit, damaged)));
    }

    [Theory]
    [InlineData(0ul, 40u)]
    [InlineData(15ul, 55u)]
    [InlineData(30ul, 70u)]
    [InlineData(90ul, 70u)]
    public void ProgressiveRestore_PaysOutInStepWithTheChannel(
        ulong elapsedTicks,
        uint expected)
    {
        Assert.Equal(
            expected,
            TacticalRules.ProgressiveRestore(
                initial: 40,
                maximum: 100,
                restoreAmount: 30,
                elapsedTicks: elapsedTicks,
                durationTicks: TacticalRules.RepairDurationTicks));
    }

    [Fact]
    public void ProgressiveRestore_NeverExceedsTheHullCeiling()
    {
        Assert.Equal(
            100u,
            TacticalRules.ProgressiveRestore(90, 100, 50, 30, TacticalRules.RepairDurationTicks));
    }

    [Fact]
    public void ProgressiveRestore_RejectsAChannelWithNoDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TacticalRules.ProgressiveRestore(10, 100, 10, 5, 0));
    }

    [Theory]
    [InlineData(0f, 0f, 10f)]
    [InlineData(90f, 10f, 0f)]
    [InlineData(180f, 0f, -10f)]
    [InlineData(270f, -10f, 0f)]
    public void MoveStorm_SailsAlongItsHeading(
        float directionDegrees,
        float expectedX,
        float expectedY)
    {
        var moved = TacticalRules.MoveStorm(0f, 0f, directionDegrees, 10f, 1f);

        Assert.Equal(expectedX, moved.X, 3);
        Assert.Equal(expectedY, moved.Y, 3);
    }

    [Fact]
    public void MoveStorm_WrapsAroundTheChartRatherThanLeavingIt()
    {
        var moved = TacticalRules.MoveStorm(WorldRules.MapMax - 1f, 0f, 90f, 10f, 1f);

        Assert.InRange(moved.X, WorldRules.MapMin, WorldRules.MapMax);
        Assert.Equal(WorldRules.MapMin + 9f, moved.X, 3);
    }
}
