namespace Sea.Server;

public enum NpcIntent : byte
{
    Wander = 0,
    Chase = 1,
    Hold = 2,
    Leash = 3,
}

/// <summary>How an enemy decides where to sail (SEA_5 §11).</summary>
/// <remarks>
/// The four numbers matter to each other more than they matter on their own.
/// Aggro is inside a gun's range so an enemy is shot at before she notices;
/// leash is well past sight so a chase is a chase and not a leash; hold at
/// eighty per cent of range keeps her shooting without drifting into ramming
/// distance every time the target turns.
/// </remarks>
public static class NpcMovementRules
{
    public const float WanderRadiusSquares = 25f;
    public const float AggroRadiusSquares = 20f;
    public const float LeashRadiusSquares = 60f;
    public const float HoldDistanceFraction = 0.8f;

    /// <summary>
    /// Half a second. A course is only replotted this often however fast the
    /// target moves, because A* on a four-hundred-square grid is the most
    /// expensive thing an NPC can ask for and twice a second is enough to
    /// follow anything on the map.
    /// </summary>
    public const ulong ReplanIntervalTicks = 5UL;

    /// <summary>The shortest and longest an idle enemy loiters (SEA_5 §11.2).</summary>
    public const ulong MinimumWanderWaitTicks = 80UL;
    public const ulong MaximumWanderWaitTicks = 200UL;

    public static float HoldDistanceSquares(float effectiveRangeSquares) =>
        effectiveRangeSquares * HoldDistanceFraction;

    /// <summary>
    /// How long an idle enemy sits before picking her next spot: eight to twenty
    /// seconds, derived from her id and how many times she has already moved.
    /// Derived rather than rolled, so a replay of the same log wanders the same
    /// way, and spread rather than fixed, so fifteen hostiles on one map do not
    /// all ask for a route on the same tick.
    /// </summary>
    public static ulong WanderWaitTicks(ulong entityId, ulong wanderIndex)
    {
        var span = MaximumWanderWaitTicks - MinimumWanderWaitTicks;
        return MinimumWanderWaitTicks + (Mix(entityId * 0x9E3779B97F4A7C15UL + wanderIndex) % (span + 1UL));
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public static NpcIntent Decide(
        float distanceToTargetSquares,
        float distanceFromHomeSquares,
        float holdDistanceSquares = 0f)
    {
        if (distanceFromHomeSquares > LeashRadiusSquares)
        {
            return NpcIntent.Leash;
        }

        if (distanceToTargetSquares > AggroRadiusSquares)
        {
            return NpcIntent.Wander;
        }

        return distanceToTargetSquares <= holdDistanceSquares ? NpcIntent.Hold : NpcIntent.Chase;
    }
}
