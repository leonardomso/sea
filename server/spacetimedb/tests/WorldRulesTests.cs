using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class WorldRulesTests
{
    [Fact]
    public void TheMapIsFourHundredSquaresOnASide()
    {
        // A tripwire, not a behaviour test: 400 is a number SEA_5 §3.1 picked and nothing
        // reads MapSizeSquares yet. MapMin and MapMax are not asserted here because
        // InsideTheMapIsZeroToFourHundredOnBothAxes below fails the moment either moves.
        Assert.Equal(400f, WorldRules.MapSizeSquares);
    }

    [Fact]
    public void ATickIsATenthOfASecond()
    {
        Assert.Equal(0.1f, WorldRules.SecondsPerTick);
    }

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(400f, 400f, true)]
    [InlineData(-0.01f, 200f, false)]
    [InlineData(400.01f, 200f, false)]
    [InlineData(200f, -0.01f, false)]
    [InlineData(200f, 400.01f, false)]
    public void InsideTheMapIsZeroToFourHundredOnBothAxes(float x, float y, bool expected)
    {
        Assert.Equal(expected, WorldRules.IsInsideMap(x, y));
    }

    [Fact]
    public void ClampToMapPullsAPointBackInside()
    {
        var (x, y) = WorldRules.ClampToMap(-5f, 900f);

        // Math.Clamp hands back the bound itself, so these are exact. Replay hashes
        // floats bit for bit, and a tolerance is the one thing that would hide drift.
        Assert.Equal(0f, x);
        Assert.Equal(400f, y);
    }

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    public void IsInsideMap_rejects_non_finite_positions(float x, float y)
    {
        Assert.False(WorldRules.IsInsideMap(x, y));
    }

    [Fact]
    public void Initial_player_values_are_deterministic()
    {
        Assert.Equal(100u, WorldRules.InitialHealth);
        Assert.Equal(0u, WorldRules.InitialGold);
        Assert.Equal(10u, WorldRules.TickRateHz);
        Assert.Equal(25u, WorldRules.InitialCannonDamage);
        Assert.Equal(100u, WorldRules.EnemyInitialHealth);
        Assert.Equal(20u, WorldRules.InitialCannonCooldownTicks);
        Assert.Equal(100u, WorldRules.EnemyGoldReward);
    }

    [Fact]
    public void AdvanceTowards_moves_over_time_without_teleporting()
    {
        var step = WorldRules.AdvanceTowards(0f, 0f, 3f, 4f, 2f);

        Assert.Equal(1.2f, step.X, 3);
        Assert.Equal(1.6f, step.Y, 3);
        Assert.False(step.Arrived);
    }

    [Fact]
    public void AdvanceTowards_stops_exactly_at_the_destination()
    {
        var step = WorldRules.AdvanceTowards(0f, 0f, 3f, 4f, 5f);

        Assert.Equal(3f, step.X);
        Assert.Equal(4f, step.Y);
        Assert.True(step.Arrived);
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
    [InlineData(PlayerLoadSource.ClientLifecycle, false)]
    [InlineData(PlayerLoadSource.ExplicitLoad, true)]
    public void Player_creation_is_reserved_for_explicit_loads(
        PlayerLoadSource source,
        bool expected)
    {
        Assert.Equal(expected, PlayerConnectionRules.MayCreatePlayer(source));
    }

    [Theory]
    [InlineData(100ul, 100ul, false)]
    [InlineData(100ul, 101ul, true)]
    public void Transient_events_expire_after_their_expiry_tick(
        ulong expiresAtTick,
        ulong currentTick,
        bool expected)
    {
        Assert.Equal(expected, EventRetentionRules.IsExpired(expiresAtTick, currentTick));
    }

    [Fact]
    public void Default_content_is_complete_unique_and_validated()
    {
        var content = ContentCatalog.CreateDefault();

        Assert.Equal(4, content.Ammunition.Count);
        Assert.Equal(4, content.Npcs.Count);
        Assert.Empty(ContentCatalog.Validate(content));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void AdvanceTowardsRejectsInvalidMaximumDistance(float maximumDistance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldRules.AdvanceTowards(0, 0, 1, 1, maximumDistance));
    }
}
