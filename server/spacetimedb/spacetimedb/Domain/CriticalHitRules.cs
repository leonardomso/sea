namespace Sea.Server;

/// <summary>
/// The one roll in the whole of combat. Every other part of a volley is settled
/// by the numbers (SEA_5 section 8.2 — no hit chance, no dodge), so this is the
/// only place a fight is not a pure function of the board.
/// </summary>
/// <remarks>
/// SEA_5 section 8.7: one volley in ten lands for half again the damage, the
/// same for players and NPCs, applied after armour.
/// <para>
/// The roll is a hash of the world seed, the tick and both hulls rather than a
/// running generator, because a running generator is state a replay has to
/// reproduce exactly and a hash is not. The four inputs are absorbed one at a
/// time, each through a full mixing round, so no two of them can be swapped for
/// one another: folding them together with a bare xor would make the tick and
/// the defender the same axis and every ship would crit on the same ticks.
/// </para>
/// <para>
/// Nothing here reads the clock and nothing here is floating point at run time,
/// so the same shot rolls the same way on any host, on any run.
/// </para>
/// </remarks>
public static class CriticalHitRules
{
    /// <summary>One volley in ten. SEA_5 section 8.7.</summary>
    public const float Chance = 0.10f;

    /// <summary>Half again the damage. SEA_5 section 8.7.</summary>
    public const float Multiplier = 1.5f;

    /// <summary><see cref="Multiplier" /> as an exact ratio, so the damage never
    /// goes near a float.</summary>
    private const uint MultiplierNumerator = 3;

    private const uint MultiplierDenominator = 2;

    /// <summary>How many of the hash's top bits become the roll. Twenty-four is
    /// finer than a per-cent-of-a-per-cent, with room to spare at 10 Hz.</summary>
    private const int RollBits = 24;

    private const float RollScale = 1 << RollBits;

    /// <summary>The roll lands below this on <see cref="Chance" /> of shots. Cut
    /// once here at compile time so the comparison at run time is integers.</summary>
    private const ulong CriticalThreshold = (ulong)(Chance * RollScale);

    /// <summary>Golden ratio in 64 bits — the splitmix64 increment, which also
    /// keeps zero from mixing to zero.</summary>
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Decides whether one volley is a critical. Pure in its four arguments, so
    /// a replay of the same command log crits on the same volleys.
    /// </summary>
    public static bool IsCritical(ulong seed, ulong tick, ulong attackerId, ulong defenderId)
    {
        var state = Mix(seed);
        state = Mix(state ^ tick);
        state = Mix(state ^ attackerId);
        state = Mix(state ^ defenderId);

        return (state >> (64 - RollBits)) < CriticalThreshold;
    }

    /// <summary>
    /// Scales damage that has already been through armour. Rounds down, so a
    /// shot that was doing one point still does one point, and saturates rather
    /// than wrapping round on damage no hull will ever take.
    /// </summary>
    public static uint Apply(uint damage, bool isCritical)
    {
        if (!isCritical)
        {
            return damage;
        }

        var scaled = ((ulong)damage * MultiplierNumerator) / MultiplierDenominator;
        return scaled > uint.MaxValue ? uint.MaxValue : (uint)scaled;
    }

    /// <summary>
    /// The splitmix64 finalizer, with the increment kept so that mixing zero
    /// does not give zero back. Every input bit reaches every output bit.
    /// </summary>
    private static ulong Mix(ulong value)
    {
        value += GoldenGamma;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
