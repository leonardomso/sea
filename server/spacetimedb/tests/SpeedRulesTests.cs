using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SpeedRulesTests
{
    // A clean brig on her rated 5.0, heading east across a wind that blows north:
    // the wind term is exactly 1 for this fixture, so every test below that does
    // not mean to talk about wind leaves the heading alone.
    private static SpeedInputs Brig() => new(
        BaseSquaresPerSecond: 5.0f,
        BonusFraction: 0f,
        Hull: 100,
        MaxHull: 100,
        HeadingDegrees: 90f,
        WindDirectionDegrees: 0f,
        InStorm: false,
        DebuffMultiplier: 1f,
        IsFrozen: false);

    [Fact]
    public void ACleanShipInACrosswindMakesHerRatedSpeed()
    {
        Assert.Equal(5.0f, SpeedRules.Effective(Brig()), 4);
    }

    [Theory]
    [InlineData(100u, 1.00f)]
    [InlineData(51u, 1.00f)]
    [InlineData(50u, 0.92f)]
    [InlineData(26u, 0.92f)]
    [InlineData(25u, 0.85f)]
    [InlineData(1u, 0.85f)]
    public void HpStateHasThreeSteps(uint hull, float expected)
    {
        Assert.Equal(expected, SpeedRules.HpStateMultiplier(hull, 100), 4);
    }

    [Fact]
    public void AShipWithNoRatedHullIsNeverCountedAsDamaged()
    {
        Assert.Equal(1.00f, SpeedRules.HpStateMultiplier(0, 0), 4);
    }

    [Fact]
    public void DownwindIsTenPerCentAndUpwindIsTenPerCentTheOtherWay()
    {
        Assert.Equal(1.10f, SpeedRules.WindMultiplier(90f, 90f), 4);
        Assert.Equal(0.90f, SpeedRules.WindMultiplier(270f, 90f), 4);
        Assert.Equal(1.00f, SpeedRules.WindMultiplier(0f, 90f), 4);
    }

    [Fact]
    public void BonusesAddThenCapAtTwentyFivePerCent()
    {
        // SEA_5 §13 test 10, with the cap Sea keeps from stat_caps.json.
        var inputs = Brig() with { BonusFraction = 0.35f };

        Assert.Equal(6.25f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void AStormAndAHeadWindMultiplyTogether()
    {
        // SEA_5 §13 test 8.
        var inputs = Brig() with { InStorm = true, HeadingDegrees = 180f };

        Assert.Equal(5.0f * 0.85f * 0.90f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void SlowsMultiplyButNeverBelowHalf()
    {
        var inputs = Brig() with { DebuffMultiplier = 0.2f };

        Assert.Equal(2.5f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void NeitherABonusNorADebuffMayMakeAShipFasterThanHerRating()
    {
        var inputs = Brig() with { BonusFraction = -0.5f, DebuffMultiplier = 2f };

        Assert.Equal(5.0f, SpeedRules.Effective(inputs), 4);
    }

    [Fact]
    public void AFrozenShipMakesNoWayAtAll()
    {
        Assert.Equal(0f, SpeedRules.Effective(Brig() with { IsFrozen = true }), 6);
    }

    [Fact]
    public void TheFastestPossibleShipIsSevenPointSeven()
    {
        // SEA_5 §5.3, amended for the 0.25 cap.
        var skiff = new SpeedInputs(5.6f, 0.25f, 100, 100, 90f, 90f, false, 1f, false);

        Assert.Equal(7.70f, SpeedRules.Effective(skiff), 3);
    }

    [Fact]
    public void TheSlowestPossibleShipIsTwoPointEightSix()
    {
        var galleon = new SpeedInputs(4.4f, 0f, 20, 100, 180f, 0f, true, 1f, false);

        Assert.Equal(2.86f, SpeedRules.Effective(galleon), 2);
    }
}
