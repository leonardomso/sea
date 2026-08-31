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
        Assert.Equal(25u, WorldRules.InitialCannonDamage);
        Assert.Equal(100u, WorldRules.EnemyInitialHealth);
        Assert.Equal(20u, WorldRules.InitialCannonCooldownTicks);
        Assert.Equal(60f, WorldRules.CannonRange);
        Assert.Equal(100u, WorldRules.EnemyGoldReward);
        Assert.Equal(1u, WorldRules.InitialProgressionLevel);
        Assert.Equal(0u, WorldRules.InitialCannonUpgradeLevel);
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

    [Fact]
    public void IsInRange_uses_inclusive_distance()
    {
        Assert.True(WorldRules.IsInRange(0, 0, 3, 4, 5));
        Assert.False(WorldRules.IsInRange(0, 0, 3, 4, 4.99f));
    }

    [Theory]
    [InlineData(100u, 25u, 75u)]
    [InlineData(25u, 25u, 0u)]
    [InlineData(0u, 25u, 0u)]
    public void ApplyDamage_never_underflows(uint health, uint damage, uint expected)
    {
        Assert.Equal(expected, WorldRules.ApplyDamage(health, damage));
    }

    [Theory]
    [InlineData(0u, 100u)]
    [InlineData(1u, 200u)]
    [InlineData(2u, 300u)]
    public void CannonUpgradeCost_is_deterministic(uint level, uint expected)
    {
        Assert.Equal(expected, WorldRules.CannonUpgradeCost(level));
    }

    [Fact]
    public void CannonDamageAfterUpgrade_adds_the_fixed_upgrade_bonus()
    {
        Assert.Equal(35u, WorldRules.CannonDamageAfterUpgrade(25, 2));
    }
}
