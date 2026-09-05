namespace Sea.Server;

public static class ProgressionRules
{
    public const ulong BoardingContribution = 25;

    public static ulong AddSaturating(ulong current, ulong amount) =>
        ulong.MaxValue - current < amount ? ulong.MaxValue : current + amount;

    public static uint AddGoldSaturating(uint current, uint amount) =>
        uint.MaxValue - current < amount ? uint.MaxValue : current + amount;

    /// <summary>
    /// Money out of the purse. A charge larger than what she is carrying empties it rather than
    /// wrapping round and making her the richest captain afloat.
    /// </summary>
    public static uint TakeGoldSaturating(uint current, uint amount) =>
        amount >= current ? 0u : current - amount;
}

public readonly record struct LootCandidate(ulong EntityId, float Distance);
public readonly record struct LootClaimSelection(ulong EntityId, float Distance);

public static class LootRules
{
    public const float PickupRadius = 3.5f;
    public const ulong LifetimeTicks = 600;

    /// <summary>
    /// What a claimed crate is worth in the purse. Section 5's reward split pays a kill through
    /// encounter settlement; sail-over salvage is the separate small bonus on top of it, and it
    /// is paid the same way gold is because a captain who sails over a wreck and is given a log
    /// line has not been given anything. A type the hold will carry one day pays nothing yet.
    /// </summary>
    public static uint GoldFromClaim(string lootType, uint quantity) =>
        string.Equals(lootType, "gold", StringComparison.Ordinal) ||
        string.Equals(lootType, "salvage", StringComparison.Ordinal)
            ? quantity
            : 0;

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
    public const ulong PlayerDelayTicks = 8 * WorldRules.TickRateHz;
    public const ulong NpcDelayTicks = 300;
    public const ulong PlayerProtectionTicks = 10 * WorldRules.TickRateHz;

    // The spawn shield covers a player's first spawn as well as every respawn.
    public static ulong PlayerProtectionUntil(ulong currentTick) =>
        currentTick + PlayerProtectionTicks;

    // Every hull comes back whole. A wreck that respawned half-repaired only sent the player
    // straight back to the port to finish the job, which is a loading screen, not a decision.
    public static RespawnState Restore(bool player, uint maximumHull, ulong currentTick) =>
        new(maximumHull, player ? PlayerProtectionUntil(currentTick) : currentTick);
}
