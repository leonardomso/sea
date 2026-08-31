using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class WorldRulesTests
{
    [Theory]
    [InlineData(-100, -100)]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    public void IsInsideMap_accepts_positions_within_bounds(float x, float y)
    {
        Assert.True(WorldRules.IsInsideMap(x, y));
    }

    [Theory]
    [InlineData(-100.01f, 0)]
    [InlineData(100.01f, 0)]
    [InlineData(0, -100.01f)]
    [InlineData(0, 100.01f)]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    public void IsInsideMap_rejects_invalid_positions(float x, float y)
    {
        Assert.False(WorldRules.IsInsideMap(x, y));
    }

    [Fact]
    public void Initial_player_values_are_deterministic()
    {
        Assert.Equal(100u, WorldRules.InitialHealth);
        Assert.Equal(0u, WorldRules.InitialGold);
        Assert.Equal(20u, WorldRules.TickRateHz);
    }
}
