using System.Numerics;

namespace Sea.Server;

public enum EncounterStateCode : byte
{
    Open = 1,
    Settled = 2,
}

public readonly record struct RewardContribution(
    ulong EntityId,
    ulong Damage,
    ulong Boarding,
    ulong Support)
{
    public BigInteger Score =>
        (BigInteger)Damage + Boarding + Support;
}

public readonly record struct RewardShare(ulong EntityId, ulong Amount);
public readonly record struct EncounterRewardGrant(
    ulong EntityId,
    uint Gold,
    ulong Experience);

public static class EncounterSettlementRules
{
    public static IReadOnlyList<EncounterRewardGrant> Settle(
        uint goldPool,
        ulong experiencePool,
        IReadOnlyCollection<RewardContribution> contributions)
    {
        var gold = SharedRewardRules.Distribute(goldPool, contributions);
        var experience = SharedRewardRules.Distribute(experiencePool, contributions);
        if (gold.Count != experience.Count)
        {
            throw new InvalidOperationException("Reward distributions selected different contributors.");
        }

        var grants = new EncounterRewardGrant[gold.Count];
        for (var index = 0; index < grants.Length; index++)
        {
            if (gold[index].EntityId != experience[index].EntityId)
            {
                throw new InvalidOperationException("Reward distributions used different contributor order.");
            }

            grants[index] = new EncounterRewardGrant(
                gold[index].EntityId,
                checked((uint)gold[index].Amount),
                experience[index].Amount);
        }

        return grants;
    }
}

public static class SharedRewardRules
{
    private const ulong EqualPoolPercent = 30;
    private const ulong EligibilityPercent = 5;
    private const ulong PercentScale = 100;

    public static IReadOnlyList<RewardShare> Distribute(
        ulong pool,
        IReadOnlyCollection<RewardContribution> contributions)
    {
        EnsureUniqueContributors(contributions);
        if (contributions.Count == 0)
        {
            return [];
        }

        var scored = contributions
            .Where(contribution => contribution.Score > BigInteger.Zero)
            .ToArray();
        if (scored.Length == 0)
        {
            return [];
        }

        var totalScore = scored.Aggregate(
            BigInteger.Zero,
            (total, contribution) => total + contribution.Score);
        var eligible = scored
            .Where(contribution =>
                contribution.Score * PercentScale >= totalScore * EligibilityPercent)
            .OrderByDescending(contribution => contribution.Score)
            .ThenBy(contribution => contribution.EntityId)
            .ToArray();
        if (eligible.Length == 0)
        {
            return [];
        }

        var eligibleScore = eligible.Aggregate(
            BigInteger.Zero,
            (total, contribution) => total + contribution.Score);
        var equalPool = (ulong)((BigInteger)pool * EqualPoolPercent / PercentScale);
        var proportionalPool = pool - equalPool;
        var shares = new ulong[eligible.Length];

        DistributeEqual(equalPool, shares);
        DistributeProportional(proportionalPool, eligible, eligibleScore, shares);

        return eligible
            .Select((contribution, index) =>
                new RewardShare(contribution.EntityId, shares[index]))
            .ToArray();
    }

    private static void EnsureUniqueContributors(
        IReadOnlyCollection<RewardContribution> contributions)
    {
        var entityIds = new HashSet<ulong>();
        foreach (var contribution in contributions)
        {
            if (contribution.EntityId == 0 || !entityIds.Add(contribution.EntityId))
            {
                throw new InvalidOperationException(
                    $"Encounter contribution state has an invalid or duplicate entity {contribution.EntityId}.");
            }
        }
    }

    private static void DistributeEqual(ulong pool, ulong[] shares)
    {
        var count = (ulong)shares.Length;
        var share = pool / count;
        var remainder = pool % count;
        for (var index = 0; index < shares.Length; index++)
        {
            shares[index] = share + ((ulong)index < remainder ? 1ul : 0ul);
        }
    }

    private static void DistributeProportional(
        ulong pool,
        IReadOnlyList<RewardContribution> contributions,
        BigInteger totalScore,
        ulong[] shares)
    {
        var distributed = 0ul;
        for (var index = 0; index < contributions.Count; index++)
        {
            var share = (ulong)(pool * contributions[index].Score / totalScore);
            shares[index] += share;
            distributed += share;
        }

        var remainder = pool - distributed;
        for (var index = 0ul; index < remainder; index++)
        {
            shares[index % (ulong)shares.Length]++;
        }
    }
}
