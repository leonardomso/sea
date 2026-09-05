using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ProgressionRulesTests
{
    [Theory]
    [InlineData(0u, 100u, 100u)]
    [InlineData(uint.MaxValue - 5, 10u, uint.MaxValue)]
    [InlineData(uint.MaxValue, 1u, uint.MaxValue)]
    [InlineData(uint.MaxValue - 10, 10u, uint.MaxValue)]
    [InlineData(uint.MaxValue, 0u, uint.MaxValue)]
    public void Gold_addition_saturates(uint current, uint amount, uint expected)
    {
        Assert.Equal(expected, ProgressionRules.AddGoldSaturating(current, amount));
    }

    [Fact]
    public void Both_kinds_of_crate_pay_their_quantity_into_the_purse()
    {
        // The kill itself is paid by encounter settlement; this is the sail-over bonus on
        // top of it, and salvage is the only thing a Havenmere wreck actually leaves.
        Assert.Equal(12u, LootRules.GoldFromClaim("gold", 12));
        Assert.Equal(7u, LootRules.GoldFromClaim("salvage", 7));

        // A type the hold will carry one day is not gold, and quietly minting it would be.
        Assert.Equal(0u, LootRules.GoldFromClaim("powder", 9));
        Assert.Equal(0u, LootRules.GoldFromClaim("Gold", 9));
    }

    [Fact]
    public void Contribution_addition_saturates()
    {
        Assert.Equal(30ul, ProgressionRules.AddSaturating(10, 20));
        Assert.Equal(ulong.MaxValue, ProgressionRules.AddSaturating(ulong.MaxValue - 1, 5));
        Assert.Equal(ulong.MaxValue, ProgressionRules.AddSaturating(ulong.MaxValue - 5, 5));
        Assert.Equal(10ul, ProgressionRules.AddSaturating(10, 0));
    }

    [Fact]
    public void Boarding_contribution_is_a_fixed_constant()
    {
        // Balance guard pinning the tuned value, not a behavior test.
        Assert.Equal(25ul, ProgressionRules.BoardingContribution);
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
    [InlineData(true, 100u, 100ul)]
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


    [Fact]
    public void Fresh_spawns_carry_the_same_ten_second_shield_as_respawns()
    {
        Assert.Equal(10 * WorldRules.TickRateHz, RespawnRules.PlayerProtectionTicks);
        Assert.Equal(
            RespawnRules.Restore(player: true, maximumHull: 100, currentTick: 40).InvulnerableUntilTick,
            RespawnRules.PlayerProtectionUntil(40));
    }
    [Fact]
    public void AChargeLargerThanThePurseEmptiesItRatherThanWrappingRound()
    {
        Assert.Equal(400u, ProgressionRules.TakeGoldSaturating(1_000u, 600u));
        Assert.Equal(0u, ProgressionRules.TakeGoldSaturating(1_000u, 1_000u));
        Assert.Equal(0u, ProgressionRules.TakeGoldSaturating(1_000u, uint.MaxValue));
    }

}
