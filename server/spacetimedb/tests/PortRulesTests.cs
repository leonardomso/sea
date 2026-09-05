using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class PortRulesTests
{
    private const float PortX = -80f;
    private const float PortY = -80f;
    private const float PortRadius = 10f;

    [Theory]
    [InlineData(-80f, -80f, true)]
    [InlineData(-71f, -80f, true)]
    [InlineData(-69f, -80f, false)]
    [InlineData(0f, 0f, false)]
    public void TheHarbourMouthIsACircle(float x, float y, bool expected)
    {
        Assert.Equal(expected, PortRules.IsInside(x, y, PortX, PortY, PortRadius));
    }

    [Fact]
    public void OnlyACourseOutOfThePortIsCastOffFor()
    {
        Assert.True(PortRules.RequiresCastOff(true, 0f, 0f, PortX, PortY, PortRadius));
    }

    [Fact]
    public void SailingBetweenBerthsInsideThePortIsJustSailing()
    {
        Assert.False(PortRules.RequiresCastOff(true, -75f, -78f, PortX, PortY, PortRadius));
    }

    [Fact]
    public void AShipAtSeaNeverOwesACastOff()
    {
        Assert.False(PortRules.RequiresCastOff(false, 0f, 0f, PortX, PortY, PortRadius));
        Assert.False(PortRules.RequiresCastOff(false, PortX, PortY, PortX, PortY, PortRadius));
    }

    [Fact]
    public void CastingOffTakesThreeSeconds()
    {
        Assert.Equal(3UL * WorldRules.TickRateHz, PortRules.CastOffTicks);
    }

    [Fact]
    public void SafeWaterReachesThirtySquaresFromTheHarbour()
    {
        Assert.True(PortRules.IsSafeWater(70f, 300f, harborX: 70f, harborY: 330f));
        Assert.False(PortRules.IsSafeWater(70f, 299f, 70f, 330f));
    }

    [Fact]
    public void AShoalSlowsATierThreeHullAndStopsATierFive()
    {
        Assert.True(PortRules.CanCrossShoal(tier: 1));
        Assert.True(PortRules.CanCrossShoal(3));
        Assert.False(PortRules.CanCrossShoal(4));
        Assert.False(PortRules.CanCrossShoal(5));
    }
}
