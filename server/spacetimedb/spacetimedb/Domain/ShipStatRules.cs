namespace Sea.Server;

public enum BonusSourceKind : byte
{
    HullVariant = 0,
    Plates = 1,
    Sails = 2,
    Crew = 3,
    Skills = 4,
    Buffs = 5,
}

public readonly record struct StatBonuses(
    float Damage,
    float Reload,
    int Magazine,
    float HitPoints,
    float ArmorPoints,
    float Speed,
    float Turn,
    int RangeSquares,
    float RepairAmount,
    float RepairChannel,
    int ExtraCannonSlots)
{
    public static StatBonuses None => default;

    /// <summary>
    /// The integer sums are <c>checked</c>: a wrapped total would be negative, and the caps only bound
    /// a total from above.
    /// </summary>
    public StatBonuses Add(StatBonuses other) => new(
        Damage: Damage + other.Damage,
        Reload: Reload + other.Reload,
        Magazine: checked(Magazine + other.Magazine),
        HitPoints: HitPoints + other.HitPoints,
        ArmorPoints: ArmorPoints + other.ArmorPoints,
        Speed: Speed + other.Speed,
        Turn: Turn + other.Turn,
        RangeSquares: checked(RangeSquares + other.RangeSquares),
        RepairAmount: RepairAmount + other.RepairAmount,
        RepairChannel: RepairChannel + other.RepairChannel,
        ExtraCannonSlots: checked(ExtraCannonSlots + other.ExtraCannonSlots));
}

/// <summary>
/// One contributor of bonuses. <see cref="SourceId"/> breaks ties between same-kind sources so the
/// drop order is a function of the data alone: SpacetimeDB row order is not a replay contract.
/// </summary>
public readonly record struct BonusSource(BonusSourceKind Kind, ulong SourceId, StatBonuses Bonuses);

/// <summary>
/// Validation lives in the <c>init</c> accessors, not in the property initialisers alone: a record's
/// copy constructor bypasses initialisers, so a <c>with</c> expression would otherwise slip past them.
/// </summary>
public sealed record ShipLoadout(
    HullContent Hull,
    CannonContent Cannon,
    int CannonCount,
    float AmmoDamageMultiplier,
    float AmmoReloadMultiplier)
{
    private readonly HullContent hull = Armed(Hull, nameof(Hull));
    private readonly CannonContent cannon = Present(Cannon, nameof(Cannon));
    private readonly int cannonCount = AtLeastOne(CannonCount, nameof(CannonCount));
    private readonly float ammoDamageMultiplier = PositiveRatio(AmmoDamageMultiplier, nameof(AmmoDamageMultiplier));
    private readonly float ammoReloadMultiplier = PositiveRatio(AmmoReloadMultiplier, nameof(AmmoReloadMultiplier));

    public HullContent Hull
    {
        get => hull;
        init => hull = Armed(value, nameof(Hull));
    }

    public CannonContent Cannon
    {
        get => cannon;
        init => cannon = Present(value, nameof(Cannon));
    }

    public int CannonCount
    {
        get => cannonCount;
        init => cannonCount = AtLeastOne(value, nameof(CannonCount));
    }

    public float AmmoDamageMultiplier
    {
        get => ammoDamageMultiplier;
        init => ammoDamageMultiplier = PositiveRatio(value, nameof(AmmoDamageMultiplier));
    }

    public float AmmoReloadMultiplier
    {
        get => ammoReloadMultiplier;
        init => ammoReloadMultiplier = PositiveRatio(value, nameof(AmmoReloadMultiplier));
    }

    private static HullContent Armed(HullContent value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value.CannonSlots >= 1
            ? value
            : throw new ArgumentOutOfRangeException(name, value.CannonSlots, "A hull must have a cannon slot.");
    }

    private static CannonContent Present(CannonContent value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }

    private static int AtLeastOne(int value, string name) => value >= 1
        ? value
        : throw new ArgumentOutOfRangeException(name, value, "A loadout must carry at least one cannon.");

    private static float PositiveRatio(float value, string name) => float.IsFinite(value) && value > 0f
        ? value
        : throw new ArgumentOutOfRangeException(name, value, "An ammunition multiplier must be finite and positive.");
}

public readonly record struct ShipStatSheet(
    uint VolleyDamage,
    uint ReloadMilliseconds,
    byte Magazine,
    uint MaxHitPoints,
    float ArmorFront,
    float ArmorSides,
    float ArmorBack,
    float SpeedSquaresPerSecond,
    float TurnDegreesPerSecond,
    byte RangeSquares,
    float RepairAmount,
    uint RepairChannelMilliseconds,
    float CombatPowerUsed,
    float CombatPowerInactive,
    float FightScore);

public static class ShipStatRules
{
    /// <summary>Ratio bonuses are carried as basis points so every scaling step is exact integer math.</summary>
    private const long BonusScale = 10_000;

    /// <summary>Combat Power is carried in centis (hundredths of a point) for the same reason.</summary>
    private const long PowerScale = 100;

    /// <summary>
    /// Sums every source (each floored at zero first), clamps the total to the caps, then drops sources
    /// from the end of the HullVariant, Plates, Sails, Crew, Skills, Buffs order until the Combat Power
    /// budget is met. Dropped sources count as inactive.
    /// </summary>
    public static ShipStatSheet Compute(ShipLoadout loadout, IReadOnlyList<BonusSource> sources, StatCapsContent caps)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(caps);

        // An empty source list needs neither the sort/select chain nor a fresh one-element prefix array.
        var ordered = sources.Count == 0 ? Array.Empty<StatBonuses>() : Ordered(sources);
        var prefix = Prefix(ordered);

        // Start with every source and drop from the tail until the budget is met, computing each prefix once.
        var budget = Centis(caps.CombatPowerBudget);
        var active = ordered.Length;
        var bonuses = Cap(prefix[active], caps);
        var wanted = CombatPowerCentis(bonuses, loadout.Hull, caps);
        var used = wanted;

        while (active > 0 && used > budget)
        {
            active--;
            bonuses = Cap(prefix[active], caps);
            used = CombatPowerCentis(bonuses, loadout.Hull, caps);
        }

        // Power is monotone in the prefix length, so what the budget could not afford is never negative.
        var inactive = wanted - used;

        var baseline = Derive(loadout, StatBonuses.None, caps);
        var stats = Derive(loadout, bonuses, caps);

        return new ShipStatSheet(
            stats.VolleyDamage,
            stats.ReloadMilliseconds,
            stats.Magazine,
            stats.MaxHitPoints,
            stats.ArmorFront,
            stats.ArmorSides,
            stats.ArmorBack,
            stats.SpeedSquaresPerSecond,
            stats.TurnDegreesPerSecond,
            stats.RangeSquares,
            stats.RepairAmount,
            stats.RepairChannelMilliseconds,
            (float)used / PowerScale,
            (float)inactive / PowerScale,
            FightScore(stats, baseline));
    }

    public static float SustainedDps(ShipStatSheet sheet) => Dps(sheet.VolleyDamage, sheet.ReloadMilliseconds);

    public static float EffectiveHitPoints(uint maxHitPoints, float armor) => maxHitPoints / (1f - armor);

    /// <summary>Sides armor is the reference face: Math section 12 measures every fight broadside on.</summary>
    public static float EffectiveHitPoints(ShipStatSheet sheet) =>
        EffectiveHitPoints(sheet.MaxHitPoints, sheet.ArmorSides);

    /// <summary>Every stat a loadout produces before Combat Power and the fight score are attached.</summary>
    private readonly record struct DerivedStats(
        uint VolleyDamage,
        uint ReloadMilliseconds,
        byte Magazine,
        uint MaxHitPoints,
        float ArmorFront,
        float ArmorSides,
        float ArmorBack,
        float SpeedSquaresPerSecond,
        float TurnDegreesPerSecond,
        byte RangeSquares,
        float RepairAmount,
        uint RepairChannelMilliseconds);

    /// <summary>Sorted by drop order (Task 4 amendment) with every source floored at zero first.</summary>
    private static StatBonuses[] Ordered(IReadOnlyList<BonusSource> sources) =>
        sources
            .OrderBy(source => (byte)source.Kind)
            .ThenBy(source => source.SourceId)
            .Select(source => NonNegative(source.Bonuses))
            .ToArray();

    /// <summary>
    /// prefix[n] is the sum of the first n sources, so dropping a tail is a single index step. An
    /// empty input returns the shared, never-written <see cref="EmptyPrefix"/> instead of allocating
    /// a fresh one-element array.
    /// </summary>
    private static StatBonuses[] Prefix(StatBonuses[] ordered)
    {
        if (ordered.Length == 0)
        {
            return EmptyPrefix;
        }

        var prefix = new StatBonuses[ordered.Length + 1];
        for (var index = 0; index < ordered.Length; index++)
        {
            prefix[index + 1] = prefix[index].Add(ordered[index]);
        }

        return prefix;
    }

    /// <summary>The zero source, shared across every call and never written to.</summary>
    private static readonly StatBonuses[] EmptyPrefix = new StatBonuses[1];

    private static DerivedStats Derive(ShipLoadout loadout, StatBonuses bonuses, StatCapsContent caps)
    {
        var hull = loadout.Hull;
        var cannon = loadout.Cannon;
        var cannons = (long)loadout.CannonCount + bonuses.ExtraCannonSlots;

        var volley = ScaleUp(cannons * cannon.Damage, loadout.AmmoDamageMultiplier, bonuses.Damage);
        var reload = Math.Max(
            Milliseconds(caps.ReloadFloorSeconds),
            ScaleDown(Milliseconds(cannon.ReloadSeconds), loadout.AmmoReloadMultiplier, bonuses.Reload));
        var maxHitPoints = ScaleUp(hull.HitPoints, 1f, bonuses.HitPoints);

        // Floored like the reload so a repair channel bonus above 1.0 can never go negative.
        var channel = Math.Max(0L, ScaleDown(Milliseconds(caps.RepairChannelSeconds), 1f, bonuses.RepairChannel));

        return new DerivedStats(
            checked((uint)volley),
            checked((uint)reload),
            (byte)Math.Clamp(hull.Magazine + bonuses.Magazine, 1, byte.MaxValue),
            checked((uint)maxHitPoints),
            ArmorFace(hull.ArmorFront, bonuses.ArmorPoints, caps),
            ArmorFace(hull.ArmorSides, bonuses.ArmorPoints, caps),
            ArmorFace(hull.ArmorBack, bonuses.ArmorPoints, caps),
            hull.SpeedSquaresPerSecond * (1f + bonuses.Speed),
            hull.TurnDegreesPerSecond * (1f + bonuses.Turn),
            (byte)Math.Clamp(cannon.RangeSquares + bonuses.RangeSquares, 1, byte.MaxValue),
            caps.RepairBaseAmount * (1f + bonuses.RepairAmount),
            checked((uint)channel));
    }

    /// <summary>A source only ever helps: negative and non-finite contributions are dropped per field.</summary>
    private static StatBonuses NonNegative(StatBonuses bonuses) => new(
        Damage: Floor(bonuses.Damage),
        Reload: Floor(bonuses.Reload),
        Magazine: Math.Max(bonuses.Magazine, 0),
        HitPoints: Floor(bonuses.HitPoints),
        ArmorPoints: Floor(bonuses.ArmorPoints),
        Speed: Floor(bonuses.Speed),
        Turn: Floor(bonuses.Turn),
        RangeSquares: Math.Max(bonuses.RangeSquares, 0),
        RepairAmount: Floor(bonuses.RepairAmount),
        RepairChannel: Floor(bonuses.RepairChannel),
        ExtraCannonSlots: Math.Max(bonuses.ExtraCannonSlots, 0));

    /// <summary>Upper bound only: <see cref="NonNegative"/> already established the lower bound of zero.</summary>
    private static StatBonuses Cap(StatBonuses total, StatCapsContent caps) => new(
        Damage: Math.Min(total.Damage, caps.DamageBonusCap),
        Reload: Math.Min(total.Reload, caps.ReloadBonusCap),
        Magazine: Math.Min(total.Magazine, caps.MagazineBonusCap),
        HitPoints: Math.Min(total.HitPoints, caps.HitPointBonusCap),
        ArmorPoints: Math.Min(total.ArmorPoints, caps.ArmorPointsCap),
        Speed: Math.Min(total.Speed, caps.SpeedBonusCap),
        Turn: Math.Min(total.Turn, caps.TurnBonusCap),
        RangeSquares: Math.Min(total.RangeSquares, caps.RangeBonusCapSquares),
        RepairAmount: Math.Min(total.RepairAmount, caps.RepairAmountBonusCap),
        RepairChannel: Math.Min(total.RepairChannel, caps.RepairChannelBonusCap),
        ExtraCannonSlots: Math.Min(total.ExtraCannonSlots, caps.CannonSlotBonusCap));

    // Math section 2.3. Magazine, range, speed, turn and repair cost no Combat Power by design.
    // The hull is known to have at least one cannon slot: ShipLoadout and ContentCatalog.Validate both enforce it.
    private static long CombatPowerCentis(StatBonuses capped, HullContent hull, StatCapsContent caps) =>
        PowerFromRatio(capped.Damage)
        + PowerFromRatio(capped.Reload)
        + PowerFromRatio(capped.HitPoints)
        + PowerFromRatio((double)capped.ExtraCannonSlots / hull.CannonSlots)
        + Centis((double)caps.CombatPowerArmorWeight * capped.ArmorPoints);

    private static float ArmorFace(float baseFace, float armorPoints, StatCapsContent caps) =>
        Math.Min(caps.ArmorAbsoluteMax, baseFace + (armorPoints / 100f));

    private static float Dps(uint volleyDamage, uint reloadMilliseconds) => volleyDamage * 1000f / reloadMilliseconds;

    private static float FightScore(DerivedStats stats, DerivedStats baseline)
    {
        var reference = Dps(baseline.VolleyDamage, baseline.ReloadMilliseconds)
            * EffectiveHitPoints(baseline.MaxHitPoints, baseline.ArmorSides);
        var score = Dps(stats.VolleyDamage, stats.ReloadMilliseconds)
            * EffectiveHitPoints(stats.MaxHitPoints, stats.ArmorSides);
        return reference > 0f ? score / reference : 1f;
    }

    // Rounding convention: Round is away-from-zero, and the composed integer expressions below truncate
    // toward zero in one final division. Both are exact and therefore identical on every platform.
    private static long ScaleUp(long baseUnits, float multiplier, float bonus) =>
        checked(baseUnits * BasisPoints(multiplier) * (BonusScale + BasisPoints(bonus)) / (BonusScale * BonusScale));

    private static long ScaleDown(long baseUnits, float multiplier, float bonus) =>
        checked(baseUnits * BasisPoints(multiplier) * (BonusScale - BasisPoints(bonus)) / (BonusScale * BonusScale));

    private static float Floor(float value) => float.IsFinite(value) && value > 0f ? value : 0f;

    private static long PowerFromRatio(double ratio) => Centis(100.0 * ratio);

    private static long BasisPoints(float value) => Round(value * (double)BonusScale);

    private static long Milliseconds(float seconds) => Round(seconds * 1000.0);

    private static long Centis(double points) => Round(points * PowerScale);

    private static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
}
