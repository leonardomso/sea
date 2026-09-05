using System.Linq;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class TacticalRulesTests
{
    [Fact]
    public void TheStormIsNotAppliedHereBecauseSpeedRulesOwnsIt()
    {
        // SEA_5 §5.1 puts the storm outside the debuff floor, so it is SpeedRules'
        // term and this returns only what the floor applies to.
        var modifiers = TacticalRules.Resolve(
            slowed: false, slowMagnitude: 0f, inShoal: false, repairing: false);

        Assert.Equal(1f, modifiers.SpeedMultiplier, 4);
    }

    [Fact]
    public void AShoalAndASlowMultiplyTogether()
    {
        var modifiers = TacticalRules.Resolve(
            slowed: true, slowMagnitude: 0.2f, inShoal: true, repairing: false);

        Assert.Equal(0.8f * TacticalRules.ShoalMultiplier, modifiers.SpeedMultiplier, 4);
    }

    [Fact]
    public void AStormThatReachesTheBorderStopsThere()
    {
        var (x, y) = TacticalRules.MoveStorm(
            positionX: 398f, positionY: 200f, directionDegrees: 90f,
            speedSquaresPerSecond: 0.5f, deltaSeconds: 20f);

        Assert.Equal(WorldRules.MapMax, x, 4);
        Assert.Equal(200f, y, 4);
    }

    [Fact]
    public void ThereIsNoWeaponEffectivenessLeftToIgnore()
    {
        Assert.DoesNotContain(
            "WeaponEffectiveness",
            typeof(TacticalModifiers).GetProperties().Select(property => property.Name),
            StringComparer.Ordinal);
    }

    [Fact]
    public void ARepairingHullHoldsStationRatherThanRunning()
    {
        var modifiers = TacticalRules.Resolve(
            slowed: false, slowMagnitude: 0f, inShoal: false, repairing: true);

        Assert.Equal(TacticalRules.RepairingMultiplier, modifiers.SpeedMultiplier, 4);
    }

    [Fact]
    public void ASlowShoalAndARepairAllMultiplyTogether()
    {
        var modifiers = TacticalRules.Resolve(
            slowed: true, slowMagnitude: 0.5f, inShoal: true, repairing: true);

        // 0.5 slow, then the shoal, then holding station: each multiplies the one before it.
        Assert.Equal(
            0.5f * TacticalRules.ShoalMultiplier * TacticalRules.RepairingMultiplier,
            modifiers.SpeedMultiplier,
            4);
    }

    // Mid-chart on purpose. A fixture at (0,0) sits in the world's north-west corner, so
    // three of the four bearings would run off the edge and stop against the border
    // rather than testing the heading at all.
    [Theory]
    [InlineData(0f, 200f, 190f)]     // north is up the screen
    [InlineData(90f, 210f, 200f)]
    [InlineData(180f, 200f, 210f)]
    [InlineData(270f, 190f, 200f)]
    public void MoveStorm_SailsAlongItsHeading(
        float directionDegrees,
        float expectedX,
        float expectedY)
    {
        var (x, y) = TacticalRules.MoveStorm(200f, 200f, directionDegrees, 10f, 1f);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }

    [Fact]
    public void MoveStorm_StopsAtEveryBorderRatherThanWrapping()
    {
        var (x, y) = TacticalRules.MoveStorm(
            positionX: WorldRules.MapMin + 1f, positionY: WorldRules.MapMin + 1f,
            directionDegrees: 270f, speedSquaresPerSecond: 10f, deltaSeconds: 1f);

        Assert.Equal(WorldRules.MapMin, x, 4);
        Assert.Equal(WorldRules.MapMin + 1f, y, 4);
    }
}
