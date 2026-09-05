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
/// Why an order to grapple was refused. The order matters as much as the list: a captain is told
/// the nearest reason she cannot board, not the last one, so "she is too healthy" beats "you are
/// on cooldown" when both are true.
/// </summary>
public enum BoardingRejection
{
    None,
    SourceSunk,
    NoTarget,
    TargetSunk,
    InPort,
    OutOfRange,
    TargetNotBoardable,
    NotEnoughHands,
    OnCooldown,
    TargetRecentlyBoarded,
}

/// <summary>
/// Everything admission knows about a captain asking to throw her hooks. It carries the two
/// timers rather than the ticks they were set on, because a cooldown is a fact about the row and
/// working it out twice is how the two ends of it come to disagree.
/// </summary>
public readonly record struct BoardingRequest
{
    public bool SourceAlive { get; init; }
    public bool TargetSelected { get; init; }
    public bool TargetAlive { get; init; }

    /// <summary>Either ship inside safe water. The harbour is a truce for hooks as well as guns.</summary>
    public bool InPort { get; init; }

    public float DistanceSquares { get; init; }
    public uint DefenderHull { get; init; }
    public uint DefenderMaxHull { get; init; }
    public uint AttackerHands { get; init; }
    public uint AttackerMaxHands { get; init; }
    public ulong CurrentTick { get; init; }
    public ulong AttackerCooldownUntilTick { get; init; }
    public ulong DefenderImmuneUntilTick { get; init; }
}

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

    /// <summary>Ten hands to a rate (SEA_2 §5.7): 10/20/30/40/50 by hull, and 10 x tier hostile.</summary>
    public const uint HandsPerTier = 10;

    /// <summary>A sailor comes back to his post every minute at sea (SEA_3 §4.3).</summary>
    public const ulong HandsRecoveryTicks = 60 * WorldRules.TickRateHz;

    /// <summary>A won boarding is paid by the game at fifteen map drops (SEA_2 §5.7).</summary>
    public const float HaulBaseMultiplier = 15f;

    /// <summary>A lost one costs twenty-five (SEA_2 §5.7), capped by <see cref="FailPurseFraction"/>.</summary>
    public const float FailGoldMultiplier = 25f;

    /// <inheritdoc cref="FailGoldMultiplier"/>
    public const float FailPurseFraction = 0.05f;

    /// <summary>
    /// The crew a rate carries. Tier nought is not a rate anything afloat has, but a row read
    /// before its stats are written would say so, and a complement of nought is a ship that can
    /// never board again -- so the first rate's crew is the floor.
    /// </summary>
    public static uint Complement(byte tier) => HandsPerTier * Math.Max((uint)tier, 1u);

    /// <summary>
    /// Hands, brought forward by however long she has been at sea. Worked out when it is asked
    /// for rather than added every tick: a sailor an hour is not worth a write per hull per tick,
    /// and the answer is the same either way.
    /// </summary>
    public static uint Recover(uint hands, uint maxHands, ulong elapsedTicks)
    {
        if (hands >= maxHands)
        {
            return maxHands;
        }

        var recovered = elapsedTicks / HandsRecoveryTicks;
        return recovered >= maxHands - hands ? maxHands : hands + (uint)recovered;
    }

    /// <summary>
    /// The hull factor on the attack, <c>0.6 + 0.4 x HP</c> (SEA_2 §5.7). A hull with no maximum
    /// is not a mauled ship, it is a ship whose stats have not been written; she counts as whole.
    /// </summary>
    public static float AttackerMorale(uint hull, uint maximumHull) =>
        0.6f + (0.4f * HullFraction(hull, maximumHull));

    /// <summary>
    /// The same factor on the defence, <c>0.4 + 0.6 x HP</c>: a mauled ship loses more of her
    /// defence than of her attack, which is what makes half health the moment to grapple.
    /// </summary>
    public static float DefenderMorale(uint hull, uint maximumHull) =>
        0.4f + (0.6f * HullFraction(hull, maximumHull));

    /// <summary>What a won boarding pays, from the game and not from the victim (SEA_3 §4.3).</summary>
    public static uint Haul(float baseGold, float lootMultiplier) =>
        Round(baseGold * HaulBaseMultiplier * lootMultiplier);

    /// <summary><c>min(25 x G(map), 0.05 x purse)</c> (SEA_2 §5.7).</summary>
    public static uint FailGold(float baseGold, uint purse) =>
        Math.Min(Round(baseGold * FailGoldMultiplier), Round(purse * FailPurseFraction));

    /// <summary>
    /// The checks, in the order SEA_5 §9.1 and SEA_3 §4.3 put them: alive, at peace or not, near
    /// enough, hurt enough, manned enough, and only then the two clocks.
    /// </summary>
    public static BoardingRejection Validate(BoardingRequest request)
    {
        if (!request.SourceAlive)
        {
            return BoardingRejection.SourceSunk;
        }

        if (!request.TargetSelected)
        {
            return BoardingRejection.NoTarget;
        }

        if (!request.TargetAlive)
        {
            return BoardingRejection.TargetSunk;
        }

        if (request.InPort)
        {
            return BoardingRejection.InPort;
        }

        if (!IsInReach(request.DistanceSquares))
        {
            return BoardingRejection.OutOfRange;
        }

        if (!CanBoard(request.DefenderHull, request.DefenderMaxHull))
        {
            return BoardingRejection.TargetNotBoardable;
        }

        if (!HasHandsToBoard(request.AttackerHands, request.AttackerMaxHands))
        {
            return BoardingRejection.NotEnoughHands;
        }

        if (request.CurrentTick < request.AttackerCooldownUntilTick)
        {
            return BoardingRejection.OnCooldown;
        }

        return request.CurrentTick < request.DefenderImmuneUntilTick
            ? BoardingRejection.TargetRecentlyBoarded
            : BoardingRejection.None;
    }

    private static float HullFraction(uint hull, uint maximumHull) =>
        maximumHull == 0 ? 1f : Math.Clamp((float)hull / maximumHull, 0f, 1f);

    private static uint Hull(uint maximumHull, float fraction) =>
        Round(maximumHull * fraction);

    private static uint Hands(uint hands, float fraction) =>
        Round(hands * fraction);

    private static uint Round(float value) =>
        value <= 0f ? 0u : (uint)MathF.Round(value, MidpointRounding.AwayFromZero);
}
