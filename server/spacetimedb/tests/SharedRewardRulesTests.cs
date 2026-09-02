using System.Numerics;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SharedRewardRulesTests
{
    [Fact]
    public void One_contributor_receives_the_complete_pool()
    {
        var grants = SharedRewardRules.Distribute(
            101,
            [new RewardContribution(7, Damage: 100, Boarding: 0, Support: 0)]);

        Assert.Equal([new RewardShare(7, 101)], grants);
    }

    [Fact]
    public void Two_contributors_receive_equal_and_proportional_shares()
    {
        var grants = SharedRewardRules.Distribute(
            101,
            [
                new RewardContribution(20, Damage: 20, Boarding: 0, Support: 0),
                new RewardContribution(10, Damage: 80, Boarding: 0, Support: 0),
            ]);

        Assert.Equal(
            [new RewardShare(10, 72), new RewardShare(20, 29)],
            grants);
    }

    [Fact]
    public void Four_equal_contributors_use_entity_id_for_every_remainder()
    {
        var grants = SharedRewardRules.Distribute(
            100,
            [
                new RewardContribution(4, Damage: 25, Boarding: 0, Support: 0),
                new RewardContribution(2, Damage: 25, Boarding: 0, Support: 0),
                new RewardContribution(3, Damage: 25, Boarding: 0, Support: 0),
                new RewardContribution(1, Damage: 25, Boarding: 0, Support: 0),
            ]);

        Assert.Equal(
            [
                new RewardShare(1, 26),
                new RewardShare(2, 26),
                new RewardShare(3, 24),
                new RewardShare(4, 24),
            ],
            grants);
    }

    [Fact]
    public void Exactly_five_percent_is_eligible()
    {
        var grants = SharedRewardRules.Distribute(
            100,
            [
                new RewardContribution(9, Damage: 5, Boarding: 0, Support: 0),
                new RewardContribution(4, Damage: 95, Boarding: 0, Support: 0),
            ]);

        Assert.Equal(
            [new RewardShare(4, 82), new RewardShare(9, 18)],
            grants);
    }

    [Fact]
    public void Contribution_below_five_percent_is_ineligible()
    {
        var grants = SharedRewardRules.Distribute(
            100,
            [
                new RewardContribution(9, Damage: 4, Boarding: 0, Support: 0),
                new RewardContribution(4, Damage: 96, Boarding: 0, Support: 0),
            ]);

        Assert.Equal([new RewardShare(4, 100)], grants);
    }

    [Fact]
    public void Damage_boarding_and_support_all_count()
    {
        var grants = SharedRewardRules.Distribute(
            120,
            [
                new RewardContribution(3, Damage: 20, Boarding: 10, Support: 10),
                new RewardContribution(8, Damage: 40, Boarding: 0, Support: 0),
                new RewardContribution(5, Damage: 0, Boarding: 20, Support: 20),
            ]);

        Assert.Equal(120ul, grants.Aggregate(0ul, (total, grant) => total + grant.Amount));
        Assert.Equal([3ul, 5ul, 8ul], grants.Select(grant => grant.EntityId).Order().ToArray());
    }

    [Fact]
    public void Many_contributors_never_gain_or_lose_pool_units()
    {
        var contributions = Enumerable.Range(1, 20)
            .Select(index => new RewardContribution(
                (ulong)index,
                Damage: 50,
                Boarding: 0,
                Support: 0))
            .ToArray();

        var grants = SharedRewardRules.Distribute(1_000_003, contributions);

        Assert.Equal(1_000_003ul, grants.Aggregate(0ul, (total, grant) => total + grant.Amount));
        Assert.Equal(Enumerable.Range(1, 20).Select(value => (ulong)value),
            grants.Select(grant => grant.EntityId));
    }

    [Fact]
    public void Empty_and_zero_contribution_sets_receive_nothing()
    {
        Assert.Empty(SharedRewardRules.Distribute(100, []));
        Assert.Empty(SharedRewardRules.Distribute(
            100,
            [new RewardContribution(1, Damage: 0, Boarding: 0, Support: 0)]));
    }

    [Fact]
    public void Maximum_pool_is_distributed_without_overflow()
    {
        var grants = SharedRewardRules.Distribute(
            ulong.MaxValue,
            [
                new RewardContribution(1, Damage: ulong.MaxValue, Boarding: 0, Support: 0),
                new RewardContribution(2, Damage: ulong.MaxValue, Boarding: 0, Support: 0),
            ]);

        var total = grants.Aggregate(BigInteger.Zero, (sum, grant) => sum + grant.Amount);
        Assert.Equal(new BigInteger(ulong.MaxValue), total);
    }

    [Fact]
    public void Zero_gold_pool_keeps_experience_eligibility_aligned()
    {
        var grants = EncounterSettlementRules.Settle(
            goldPool: 0,
            experiencePool: 100,
            [new RewardContribution(5, Damage: 50, Boarding: 0, Support: 0)]);

        Assert.Equal([new EncounterRewardGrant(5, Gold: 0, Experience: 100)], grants);
    }

    [Fact]
    public void Duplicate_contributors_are_corrupt_state()
    {
        var duplicate = new[]
        {
            new RewardContribution(1, Damage: 10, Boarding: 0, Support: 0),
            new RewardContribution(1, Damage: 20, Boarding: 0, Support: 0),
        };

        Assert.Throws<InvalidOperationException>(() =>
            SharedRewardRules.Distribute(100, duplicate));
    }

    [Fact]
    public void Encounter_settlement_conserves_both_reward_pools()
    {
        var grants = EncounterSettlementRules.Settle(
            goldPool: 103,
            experiencePool: 1_003,
            [
                new RewardContribution(30, Damage: 55, Boarding: 0, Support: 0),
                new RewardContribution(10, Damage: 35, Boarding: 10, Support: 0),
            ]);

        Assert.Equal(103u, grants.Aggregate(0u, (total, grant) => total + grant.Gold));
        Assert.Equal(1_003ul, grants.Aggregate(0ul, (total, grant) => total + grant.Experience));
        Assert.Equal([30ul, 10ul], grants.Select(grant => grant.EntityId));
    }

    [Fact]
    public void Disconnect_or_death_does_not_remove_recorded_contribution()
    {
        var recordedBeforeDisconnect = new[]
        {
            new RewardContribution(3, Damage: 60, Boarding: 0, Support: 0),
            new RewardContribution(8, Damage: 40, Boarding: 0, Support: 0),
        };

        var grants = EncounterSettlementRules.Settle(100, 200, recordedBeforeDisconnect);

        Assert.Equal([3ul, 8ul], grants.Select(grant => grant.EntityId));
        Assert.Equal(100u, grants.Aggregate(0u, (sum, grant) => sum + grant.Gold));
        Assert.Equal(200ul, grants.Aggregate(0ul, (sum, grant) => sum + grant.Experience));
    }

    [Fact]
    public void Late_join_without_contribution_receives_no_reward()
    {
        const ulong lateJoinEntityId = 99;
        var grants = EncounterSettlementRules.Settle(
            100,
            200,
            [new RewardContribution(4, Damage: 100, Boarding: 0, Support: 0)]);

        Assert.Equal([4ul], grants.Select(grant => grant.EntityId));
        Assert.DoesNotContain(grants, grant => grant.EntityId == lateJoinEntityId);
    }

    [Fact]
    public void Replaying_pure_settlement_is_deterministic()
    {
        var contributions = new[]
        {
            new RewardContribution(12, Damage: 70, Boarding: 0, Support: 0),
            new RewardContribution(9, Damage: 20, Boarding: 10, Support: 0),
        };

        var first = EncounterSettlementRules.Settle(101, 151, contributions);
        var replay = EncounterSettlementRules.Settle(101, 151, contributions);

        Assert.Equal(first, replay);
    }
}
