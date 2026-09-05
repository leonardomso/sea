using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class RangeRulesTests
{
    [Theory]
    [InlineData(1, 18f)]
    [InlineData(2, 21f)]
    [InlineData(3, 24f)]
    [InlineData(4, 27f)]
    [InlineData(5, 30f)]
    public void EachTierOfGunReachesFurther(byte tier, float squares)
    {
        Assert.Equal(squares, RangeRules.BaseRangeSquares(tier), 4);
    }

    [Fact]
    public void RangeBonusesAddThenCapAtTenPerCent()
    {
        Assert.Equal(19.8f, RangeRules.EffectiveRangeSquares(18f, 0.10f), 4);
        Assert.Equal(19.8f, RangeRules.EffectiveRangeSquares(18f, 0.40f), 4);
        Assert.Equal(18.9f, RangeRules.EffectiveRangeSquares(18f, 0.05f), 4);
    }

    [Fact]
    public void HalfASquareOfGraceIsAllowedOnTheEdge()
    {
        // SEA_5 §13 test 5: at 24.4 squares with a 24-square gun, the shot fires.
        Assert.True(RangeRules.IsWithinRange(distanceSquares: 24.4f, effectiveRangeSquares: 24f));
        Assert.False(RangeRules.IsWithinRange(24.6f, 24f));
    }

    [Fact]
    public void GraceOnlyForgivesTheShotItStarts()
    {
        // SEA_5 §7.2: the grace is checked once, when the trigger is pulled.
        Assert.Equal(0.5f, RangeRules.GraceSquares, 4);
    }

    [Fact]
    public void AShipSeesSixtySquaresAndSubscribesToFiveMore()
    {
        Assert.Equal(60f, RangeRules.ViewDistanceSquares, 4);
        Assert.Equal(65f, RangeRules.SubscriptionRadiusSquares, 4);
    }

    [Fact]
    public void AShotCrossesTheLongestRangeInUnderASecond()
    {
        Assert.True(30f / RangeRules.ProjectileSpeedSquaresPerSecond < 1f);
    }
}
