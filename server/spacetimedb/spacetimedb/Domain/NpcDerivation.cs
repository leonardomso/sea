namespace Sea.Server;

/// <summary>
/// The yardstick every hostile is measured against: the ship a player brings to the map. Section
/// seven of the maths sizes an enemy as a fraction of this hull's staying power and a fraction of
/// its output, so both numbers are read off the player's own tier one loadout rather than authored
/// enemy by enemy and left to drift when the sloop is retuned.
/// </summary>
public readonly record struct BaseShipProfile(
    uint MaximumHull,
    float SidesArmor,
    uint VolleyDamage,
    float ReloadSeconds,
    float SpeedSquaresPerSecond)
{
    /// <summary>Section 5.3: armour is damage reduction, so it multiplies the hull behind it.</summary>
    public float EffectiveHitPoints => MaximumHull / (1f - SidesArmor);

    /// <summary>Section 3.4: one full volley every reload, averaged over the reload.</summary>
    public float SustainedDamagePerSecond => VolleyDamage / ReloadSeconds;

    /// <summary>
    /// The broadside a whole hull fires: every slot loaded with the cannon it was bought with.
    /// </summary>
    public static BaseShipProfile From(HullContent hull, CannonContent cannon)
    {
        ArgumentNullException.ThrowIfNull(hull);
        ArgumentNullException.ThrowIfNull(cannon);
        return new BaseShipProfile(
            hull.HitPoints,
            hull.ArmorSides,
            cannon.Damage * hull.CannonSlots,
            cannon.ReloadSeconds,
            hull.SpeedSquaresPerSecond);
    }
}

/// <summary>
/// Everything a tier decides about a hostile. The content author picks which enemy it is; the
/// tier picks how hard it hits, how long it lives, how fast it sails, what it is worth and how
/// long the sea waits before it is back. How far it watches is not a tier's to pick: SEA_5 §11.3
/// gives one figure to every hostile afloat, and it is carried here so a boss that wants its own
/// has somewhere to put it.
/// </summary>
public readonly record struct NpcStatLine(
    uint MaximumHull,
    uint VolleyDamage,
    float Armor,
    uint GoldReward,
    float MaximumSpeedSquares,
    float AggroRangeSquares,
    ulong RespawnDelayTicks);

/// <summary>
/// Section 7.1's tier table, applied. The multipliers themselves are content -- they sit in
/// stat_caps.json beside the player's own caps -- and everything else here is the behaviour
/// Appendix D gives each tier.
/// </summary>
public static class NpcDerivation
{
    /// <summary>Gold, as a multiple of the map's own base reward G(N).</summary>
    private static readonly IReadOnlyList<float> GoldMultipliers = [1f, 2.5f, 8f, 25f, 150f, 400f];

    /// <summary>Appendix D: how much of the player's speed each tier keeps.</summary>
    private static readonly IReadOnlyList<float> SpeedFractions = [0.8f, 0.9f, 0.8f, 0.9f, 0.7f, 0.6f];

    /// <summary>
    /// How long the sea stays empty where one sank. Commons come straight back so the map is
    /// never bare; a named ship is an appointment, not a patrol, and is worth waiting for.
    /// </summary>
    private static readonly IReadOnlyList<float> RespawnSeconds =
        [30f, 30f, 600f, 2700f, 2700f, 2700f];

    /// <summary>The tiers the table covers: Common, Veteran, Elite, Named, Boss, World Boss.</summary>
    public const byte HighestTier = 6;

    public static NpcStatLine Derive(
        byte tier,
        byte mapId,
        BaseShipProfile baseShip,
        StatCapsContent caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        var index = TierIndex(tier, caps);

        // Hit points are the effective pool section 7.2 quotes, not a raw hull: the armour a tier
        // carries is applied on top of it, exactly as the player's own sloop is measured.
        return new NpcStatLine(
            Round(caps.NpcHitPointMultipliers[index] * baseShip.EffectiveHitPoints),
            Round(
                caps.NpcDpsMultipliers[index] *
                baseShip.SustainedDamagePerSecond *
                baseShip.ReloadSeconds),
            caps.NpcArmorByTier[index],
            Round(GoldMultipliers[index] * BaseGold(mapId, caps)),
            SpeedFractions[index] * baseShip.SpeedSquaresPerSecond,
            NpcMovementRules.AggroRadiusSquares,
            (ulong)MathF.Round(RespawnSeconds[index] * WorldRules.TickRateHz));
    }

    /// <summary>
    /// G(N) from section 7.1: what a common kill pays on map N. Every other reward on the map is
    /// a multiple of it, so raising one number raises the whole map with it.
    /// </summary>
    public static float BaseGold(byte mapId, StatCapsContent caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return mapId < 1
            ? throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "Maps are numbered from one.")
            : caps.GoldBase * MathF.Pow(caps.GoldGrowth, mapId - 1);
    }

    private static int TierIndex(byte tier, StatCapsContent caps)
    {
        var covered = Math.Min(
            HighestTier,
            Math.Min(
                caps.NpcHitPointMultipliers.Count,
                Math.Min(caps.NpcDpsMultipliers.Count, caps.NpcArmorByTier.Count)));
        return tier >= 1 && tier <= covered
            ? tier - 1
            : throw new ArgumentOutOfRangeException(
                nameof(tier),
                tier,
                $"The tier table covers tiers 1 to {covered}.");
    }

    private static uint Round(float value) =>
        (uint)MathF.Round(value, MidpointRounding.AwayFromZero);
}
