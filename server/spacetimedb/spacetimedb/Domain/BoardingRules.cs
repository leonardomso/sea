namespace Sea.Server;

/// <summary>
/// One side of a boarding action. <see cref="MoraleFraction"/> carries SEA_2_MATH §5.7's hull
/// factor and, once an Arms Locker exists, the weapon and guard that equip the hands; until then
/// it is the fighting condition of the crew, clamped to the unit interval.
/// </summary>
public readonly record struct BoardingParty(uint Hands, float MoraleFraction, byte Tier);

/// <summary>
/// What a boarding did. <see cref="HullDamage"/> is dealt to the defender;
/// <see cref="AttackerHullFractionLost"/> is the attacker's own price for failing, as a share of
/// her maximum hull, because <see cref="BoardingRules.Resolve(BoardingParty, BoardingParty, uint)"/>
/// is never told what that hull is.
/// </summary>
public readonly record struct BoardingOutcome(
    bool AttackerWon,
    uint HullDamage,
    uint AttackerHandsLost,
    uint DefenderHandsLost,
    ulong SilenceTicks,
    float AttackerHullFractionLost,
    float LootMultiplier);

/// <summary>
/// Grappling and taking a ship. SEA_5_PHYSICS §9 sets the reach and the cooldowns,
/// SEA_2_MATH §5.7 the scores, the chance and the price of failing, SEA_3_MECHANICS §4.3 the
/// outcome of a success.
/// </summary>
/// <remarks>
/// Boarding is deliberately hard to start: four squares is almost touching, the target has to be
/// at half health, and the attacker needs half her hands. It is a finisher, not an opener. It is
/// also never certain -- the chance is clamped away from both 0 and 1 -- so the only ship that
/// cannot be taken is one nobody is close enough to grapple.
/// </remarks>
public static class BoardingRules
{
    /// <summary>Grappling reach, in squares (SEA_5 §9.1).</summary>
    public const float ReachSquares = 4f;

    /// <summary>A hull is boardable at or below half her health (SEA_5 §9.1).</summary>
    public const float BoardableHullFraction = 0.50f;

    /// <summary>An attacker needs at least half her hands (SEA_2 §5.7).</summary>
    public const float RequiredHandsFraction = 0.50f;

    /// <summary>Sixty seconds after boarding a player (SEA_5 §9.3).</summary>
    public const ulong PlayerCooldownTicks = 60 * WorldRules.TickRateHz;

    /// <summary>Fifteen seconds after boarding an NPC (SEA_5 §9.3).</summary>
    public const ulong NpcCooldownTicks = 15 * WorldRules.TickRateHz;

    /// <summary>One victim cannot be boarded again for five minutes (SEA_5 §9, SEA_3 §4.3).</summary>
    public const ulong VictimImmunityTicks = 300 * WorldRules.TickRateHz;

    /// <summary>A win takes a tenth of the loser's maximum hull (SEA_3 §4.3).</summary>
    public const float WinHullDamageFraction = 0.10f;

    /// <summary>A win takes a tenth of the loser's hands (SEA_3 §4.3).</summary>
    public const float WinDefenderHandsFraction = 0.10f;

    /// <summary>A win still costs the winner a twentieth of hers: a fight always costs sailors.</summary>
    public const float WinAttackerHandsFraction = 0.05f;

    /// <summary>A failed boarding costs the attacker a tenth of her own maximum hull (SEA_2 §5.7).</summary>
    public const float FailHullFraction = 0.10f;

    /// <summary>
    /// A failed boarding kills <c>0.30 x (1 - P)</c> of the attacker's hands, so a long shot that
    /// fails costs more sailors than a fair fight that does (SEA_2 §5.7).
    /// </summary>
    public const float FailHandsFraction = 0.30f;

    /// <summary>The defender's guns are silent for three seconds afterwards (SEA_3 §4.3).</summary>
    public const ulong SilenceTicks = 3 * WorldRules.TickRateHz;

    /// <summary>How much a bigger hull is worth in a melee, per tier above the first.</summary>
    public const float TierWeight = 0.15f;

    /// <summary>No boarding is hopeless and none is certain (SEA_2 §5.7).</summary>
    public const float MinWinChance = 0.05f;

    /// <inheritdoc cref="MinWinChance"/>
    public const float MaxWinChance = 0.90f;

    /// <summary>The haul is worth between half and twice the base, by how one-sided it was.</summary>
    public const float MinLootMultiplier = 0.5f;

    /// <inheritdoc cref="MinLootMultiplier"/>
    public const float MaxLootMultiplier = 2.0f;

    /// <summary>
    /// The median draw. <see cref="Resolve(BoardingParty, BoardingParty, uint)"/> uses it so that
    /// a party with better than even odds takes the ship and callers that do not carry a
    /// deterministic roll still get a repeatable answer.
    /// </summary>
    public const float EvenRoll = 0.5f;

    public static bool IsInReach(float distanceSquares) => distanceSquares <= ReachSquares;

    public static bool CanBoard(uint defenderHull, uint defenderMaxHull) =>
        defenderMaxHull > 0 && (float)defenderHull / defenderMaxHull <= BoardableHullFraction;

    public static bool HasHandsToBoard(uint hands, uint maxHands) =>
        maxHands > 0 && (float)hands / maxHands >= RequiredHandsFraction;

    /// <summary>
    /// Hands, weighted by the crew's condition and by the hull they fight from. SEA_2 §5.7 scores
    /// attack and defence with different hull factors; until an Arms Locker gives the two sides
    /// different gear, one symmetric score is the whole of it.
    /// </summary>
    public static float Score(BoardingParty party) =>
        party.Hands *
        Math.Clamp(party.MoraleFraction, 0f, 1f) *
        (1f + (TierWeight * (party.Tier - 1)));

    /// <summary>
    /// <c>P = clamp(A / (A + D), 0.05, 0.90)</c> (SEA_2 §5.7).
    /// </summary>
    public static float WinChance(BoardingParty attacker, BoardingParty defender)
    {
        var attackerScore = Score(attacker);
        var total = attackerScore + Score(defender);
        return total <= 0f
            ? MinWinChance
            : Math.Clamp(attackerScore / total, MinWinChance, MaxWinChance);
    }

    /// <summary>
    /// <c>clamp(A / D, 0.5, 2.0)</c> (SEA_2 §5.7). The reward path multiplies the haul by this.
    /// </summary>
    public static float LootMultiplier(BoardingParty attacker, BoardingParty defender)
    {
        var defenderScore = Score(defender);
        return defenderScore <= 0f
            ? MaxLootMultiplier
            : Math.Clamp(Score(attacker) / defenderScore, MinLootMultiplier, MaxLootMultiplier);
    }

    /// <inheritdoc cref="Resolve(BoardingParty, BoardingParty, uint, float)"/>
    public static BoardingOutcome Resolve(
        BoardingParty attacker,
        BoardingParty defender,
        uint defenderMaxHull) =>
        Resolve(attacker, defender, defenderMaxHull, EvenRoll);

    /// <summary>
    /// One instant check, decided by <paramref name="roll"/> against
    /// <see cref="WinChance(BoardingParty, BoardingParty)"/>. Neither ship is stopped or slowed
    /// (SEA_5 §9.2); the fight goes on either way.
    /// </summary>
    public static BoardingOutcome Resolve(
        BoardingParty attacker,
        BoardingParty defender,
        uint defenderMaxHull,
        float roll)
    {
        if (Score(attacker) + Score(defender) <= 0f)
        {
            // Nobody left standing on either deck: no boarding happened, so nothing is owed.
            return new BoardingOutcome(false, 0u, 0u, 0u, 0UL, 0f, MinLootMultiplier);
        }

        var chance = WinChance(attacker, defender);
        if (roll >= chance)
        {
            return new BoardingOutcome(
                false,
                0u,
                Hands(attacker.Hands, FailHandsFraction * (1f - chance)),
                0u,
                0UL,
                FailHullFraction,
                MinLootMultiplier);
        }

        return new BoardingOutcome(
            true,
            Hull(defenderMaxHull, WinHullDamageFraction),
            Hands(attacker.Hands, WinAttackerHandsFraction),
            Hands(defender.Hands, WinDefenderHandsFraction),
            SilenceTicks,
            0f,
            LootMultiplier(attacker, defender));
    }

    private static uint Hull(uint maximumHull, float fraction) =>
        Round(maximumHull * fraction);

    private static uint Hands(uint hands, float fraction) =>
        Round(hands * fraction);

    private static uint Round(float value) =>
        value <= 0f ? 0u : (uint)MathF.Round(value, MidpointRounding.AwayFromZero);
}
