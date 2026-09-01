namespace Sea.Server;

public readonly record struct LevelThreshold(uint Level, ulong RequiredExperience);

public static class ProgressionRules
{
    public const ulong DamageExperienceDivisor = 5;
    public const ulong KillExperience = 100;
    public const ulong BoardingExperience = 25;

    public static uint LevelFor(
        ulong experience,
        IReadOnlyCollection<LevelThreshold> thresholds)
    {
        if (thresholds.Count == 0)
        {
            throw new ArgumentException("At least one level threshold is required.", nameof(thresholds));
        }

        var level = 0u;
        var requirement = 0ul;
        foreach (var threshold in thresholds)
        {
            if (threshold.RequiredExperience <= experience &&
                (threshold.RequiredExperience > requirement || threshold.Level > level))
            {
                level = threshold.Level;
                requirement = threshold.RequiredExperience;
            }
        }

        return level;
    }

    public static ulong DamageExperience(ulong appliedDamage) =>
        appliedDamage == 0 ? 0 : Math.Max(1ul, appliedDamage / DamageExperienceDivisor);

    public static ulong AddSaturating(ulong current, ulong amount) =>
        ulong.MaxValue - current < amount ? ulong.MaxValue : current + amount;
}

public readonly record struct LootCandidate(ulong EntityId, float Distance);
public readonly record struct LootClaimSelection(ulong EntityId, float Distance);

public static class LootRules
{
    public const float PickupRadius = 3.5f;
    public const ulong LifetimeTicks = 600;

    public static ulong SelectClaimant(IEnumerable<LootCandidate> candidates)
    {
        var selection = new LootClaimSelection(0, float.PositiveInfinity);
        foreach (var candidate in candidates)
        {
            selection = Consider(selection, candidate);
        }

        return selection.EntityId;
    }

    public static LootClaimSelection Consider(
        LootClaimSelection selection,
        LootCandidate candidate) =>
        candidate.EntityId == 0 ||
        !float.IsFinite(candidate.Distance) ||
        candidate.Distance > PickupRadius ||
        candidate.Distance > selection.Distance ||
        candidate.Distance == selection.Distance && candidate.EntityId >= selection.EntityId
            ? selection
            : new LootClaimSelection(candidate.EntityId, candidate.Distance);
}

public readonly record struct RespawnState(uint Hull, ulong InvulnerableUntilTick);

public static class RespawnRules
{
    public const ulong PlayerDelayTicks = 50;
    public const ulong NpcDelayTicks = 300;
    public const ulong PlayerProtectionTicks = 50;

    public static RespawnState Restore(bool player, uint maximumHull, ulong currentTick) =>
        new(
            player ? Math.Max(1u, maximumHull / 2) : maximumHull,
            player ? currentTick + PlayerProtectionTicks : currentTick);
}
