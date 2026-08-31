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

    [Theory]
    [InlineData("island", 35f, 20f, 12f, 35f, 20f)]
    [InlineData("reef", -30f, -25f, 10f, -39f, -25f)]
    public void IsBlocked_rejects_points_inside_blocking_geometry(string kind, float entityX, float entityY, float radius, float x, float y)
    {
        Assert.True(WorldRules.IsBlocked(kind, entityX, entityY, radius, x, y));
    }

    [Theory]
    [InlineData("harbor", 0f, 0f, 8f, 0f, 0f)]
    [InlineData("training_target", 45f, -10f, 15f, 45f, -10f)]
    [InlineData("island", 35f, 20f, 12f, 48f, 20f)]
    public void IsBlocked_allows_non_blocking_or_distant_points(string kind, float entityX, float entityY, float radius, float x, float y)
    {
        Assert.False(WorldRules.IsBlocked(kind, entityX, entityY, radius, x, y));
    }
}
