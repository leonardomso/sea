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
        Assert.Equal(10u, WorldRules.TickRateHz);
        Assert.Equal(25u, WorldRules.InitialCannonDamage);
        Assert.Equal(100u, WorldRules.EnemyInitialHealth);
        Assert.Equal(20u, WorldRules.InitialCannonCooldownTicks);
        Assert.Equal(60f, WorldRules.CannonRange);
        Assert.Equal(100u, WorldRules.EnemyGoldReward);
        Assert.Equal(12f, WorldRules.PlayerShipSpeed);
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
    [InlineData(PlayerLoadSource.ClientLifecycle, false)]
    [InlineData(PlayerLoadSource.ExplicitLoad, true)]
    public void Player_creation_is_reserved_for_explicit_loads(
        PlayerLoadSource source,
        bool expected)
    {
        Assert.Equal(expected, PlayerConnectionRules.MayCreatePlayer(source));
    }

    [Theory]
    [InlineData(-100f, 0)]
    [InlineData(-75.01f, 0)]
    [InlineData(-75f, 1)]
    [InlineData(0f, 4)]
    [InlineData(100f, 7)]
    public void ChunkCoordinate_partitions_the_map_into_bounded_cells(
        float position,
        int expected)
    {
        Assert.Equal(expected, SpatialRules.ChunkCoordinate(position));
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
        Assert.Equal(3, content.Npcs.Count);
        Assert.Empty(ContentCatalog.Validate(content));
    }

    [Theory]
    [InlineData(WorldObjectCode.Island, true)]
    [InlineData(WorldObjectCode.Reef, true)]
    [InlineData(WorldObjectCode.Harbor, false)]
    [InlineData(WorldObjectCode.Shoal, false)]
    [InlineData(WorldObjectCode.Storm, false)]
    public void TypedWorldObjectsOwnCollisionBehavior(WorldObjectCode kind, bool expected)
    {
        Assert.Equal(expected, WorldRules.IsBlocked(kind, 0, 0, 5, 0, 0));
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

    [Fact]
    public void SpatialBoundsClampAndRejectInvalidValues()
    {
        Assert.Equal(new ChunkBounds(0, 7, 0, 7),
            SpatialRules.BoundsAround(0, 0, 500));
        Assert.Equal(new ChunkBounds(0, 7, 0, 7),
            SpatialRules.BoundsForSegment(-100, 100, 100, -100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpatialRules.BoundsAround(0, 0, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpatialRules.ChunkCoordinate(float.PositiveInfinity));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(78)]
    public void CoordinateColumnsRejectOutOfRangeIndexes(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChartCoordinates.ColumnLabel(column));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChartCoordinates.CellCenter(column, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("AAA")]
    [InlineData("A!")]
    [InlineData("ZZ")]
    public void InvalidCoordinateColumnLabelsAreRejected(string? label)
    {
        Assert.False(ChartCoordinates.TryColumnIndex(label, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA")]
    [InlineData("AA nope")]
    [InlineData("AA -1")]
    [InlineData("AA 61")]
    public void InvalidCoordinateCellsAreRejected(string coordinate)
    {
        Assert.False(ChartCoordinates.TryCellCenter(coordinate, out _));
    }
}
