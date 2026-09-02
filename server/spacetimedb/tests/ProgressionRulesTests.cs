using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ProgressionRulesTests
{
    [Theory]
    [InlineData(0ul, 1u)]
    [InlineData(499ul, 1u)]
    [InlineData(500ul, 2u)]
    [InlineData(1_500ul, 3u)]
    [InlineData(50_000ul, 3u)]
    public void Experience_uses_data_driven_level_boundaries(ulong experience, uint expected)
    {
        var thresholds = new[]
        {
            new LevelThreshold(1, 0),
            new LevelThreshold(2, 500),
            new LevelThreshold(3, 1_500),
        };

        Assert.Equal(expected, ProgressionRules.LevelFor(experience, thresholds));
    }

    [Fact]
    public void Grant_uses_the_same_level_rule_as_production()
    {
        var thresholds = new[]
        {
            new LevelThreshold(1, 0),
            new LevelThreshold(2, 500),
            new LevelThreshold(3, 1_500),
        };

        var result = ProgressionRules.ApplyGrant(
            new ProgressionState(Experience: 490, Gold: 90, Level: 1),
            new ProgressionGrant(Experience: 1_020, Gold: 25),
            thresholds);

        Assert.Equal(new ProgressionState(1_510, 115, 3), result);
    }

    [Fact]
    public void Grant_saturates_experience_and_gold()
    {
        var result = ProgressionRules.ApplyGrant(
            new ProgressionState(ulong.MaxValue - 2, uint.MaxValue - 1, 1),
            new ProgressionGrant(10, 10),
            [new LevelThreshold(1, 0)]);

        Assert.Equal(ulong.MaxValue, result.Experience);
        Assert.Equal(uint.MaxValue, result.Gold);
        Assert.Equal(1u, result.Level);
    }

    [Fact]
    public void Level_selection_uses_the_highest_eligible_level_regardless_of_row_order()
    {
        var thresholds = new[]
        {
            new LevelThreshold(10, 1_000),
            new LevelThreshold(2, 2_000),
            new LevelThreshold(7, 500),
        };

        Assert.Equal(10u, ProgressionRules.LevelFor(2_500, thresholds));
    }

    [Fact]
    public void Loot_winner_is_nearest_then_lowest_entity_id()
    {
        var candidates = new[]
        {
            new LootCandidate(9, 2f),
            new LootCandidate(4, 2f),
            new LootCandidate(2, 3f),
        };

        Assert.Equal(4ul, LootRules.SelectClaimant(candidates));
    }

    [Fact]
    public void Loot_outside_pickup_radius_cannot_be_claimed()
    {
        Assert.Equal(
            0ul,
            LootRules.SelectClaimant([new LootCandidate(7, LootRules.PickupRadius + 0.01f)]));
    }

    [Fact]
    public void Four_way_loot_contention_has_one_stable_winner()
    {
        var selection = new LootClaimSelection(0, float.PositiveInfinity);
        foreach (var candidate in new[]
                 {
                     new LootCandidate(12, 2.5f),
                     new LootCandidate(9, 1.5f),
                     new LootCandidate(4, 1.5f),
                     new LootCandidate(2, 3f),
                 })
        {
            selection = LootRules.Consider(selection, candidate);
        }

        Assert.Equal(4ul, selection.EntityId);
        Assert.Equal(selection, LootRules.Consider(
            selection,
            new LootCandidate(4, 1.5f)));
    }

    [Theory]
    [InlineData(true, 50u, 50ul)]
    [InlineData(false, 100u, 0ul)]
    public void Respawn_contract_restores_expected_hull_and_protection(
        bool player,
        uint expectedHull,
        ulong protectionTicks)
    {
        var state = RespawnRules.Restore(
            player,
            maximumHull: 100,
            currentTick: 1_000);

        Assert.Equal(expectedHull, state.Hull);
        Assert.Equal(1_000ul + protectionTicks, state.InvulnerableUntilTick);
    }
}
