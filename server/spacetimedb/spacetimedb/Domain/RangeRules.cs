namespace Sea.Server;

/// <summary>How far a gun reaches and how far a captain sees (SEA_5 §7).</summary>
/// <remarks>
/// Everything here is in squares, which is the only unit the world has.
/// </remarks>
public static class RangeRules
{
    private static readonly float[] BaseRangesByTier = { 18f, 21f, 24f, 27f, 30f };

    /// <summary>Range gear adds together and stops at ten per cent (SEA_5 §7.1).</summary>
    public const float BonusCap = 0.10f;

    /// <summary>
    /// A shot fired at the edge is allowed half a square of slack, so a target
    /// that steps out between the click and the tick is still hit. It is checked
    /// when the trigger is pulled and never again: a shot already in the air
    /// cannot miss for range.
    /// </summary>
    public const float GraceSquares = 0.5f;

    /// <summary>How far a captain can see, in squares (SEA_5 §7.5).</summary>
    public const float ViewDistanceSquares = 60f;

    /// <summary>
    /// The five squares past the horizon a client subscribes to, so a hull is
    /// already replicated when she sails into sight rather than popping in
    /// (SEA_5 §7.5).
    /// </summary>
    public const float SubscriptionMarginSquares = 5f;

    public const float SubscriptionRadiusSquares =
        ViewDistanceSquares + SubscriptionMarginSquares;

    /// <summary>
    /// Fast enough that the longest shot on the map lands inside one second, so
    /// the flight is something a captain sees rather than something she leads.
    /// </summary>
    public const float ProjectileSpeedSquaresPerSecond = 40f;

    /// <summary>
    /// How long a cannonball is in the air, in seconds. Visual only: the damage was applied when
    /// the trigger was pulled and no amount of sailing can undo it (SEA_5 §8.3, §8.4). The client
    /// waits this out before it draws the impact, so a shot and its number land together, and
    /// holds a sinking for the same time so a hull never goes down before the ball reaches her.
    /// </summary>
    public static float FlightSeconds(float distanceSquares) =>
        distanceSquares / ProjectileSpeedSquaresPerSecond;

    /// <summary>The base reach of a gun at the given tier (1-5), in squares (SEA_5 §7.1).</summary>
    public static float BaseRangeSquares(byte tier) =>
        BaseRangesByTier[Math.Clamp(tier, (byte)1, (byte)5) - 1];

    /// <summary>
    /// Range bonuses add together before they are applied, and the total is
    /// capped at <see cref="BonusCap"/> before it touches the base range
    /// (SEA_5 §7.1).
    /// </summary>
    public static float EffectiveRangeSquares(float baseRangeSquares, float bonusFraction) =>
        baseRangeSquares * (1f + Math.Clamp(bonusFraction, 0f, BonusCap));

    /// <summary>
    /// Whether a shot fired now lands inside range, with the half-square grace
    /// applied (SEA_5 §7.2).
    /// </summary>
    public static bool IsWithinRange(float distanceSquares, float effectiveRangeSquares) =>
        distanceSquares <= effectiveRangeSquares + GraceSquares;

    /// <summary>
    /// Twice what a captain can see. A hull inside it is a dot on the minimap and
    /// nothing else: she cannot be selected or fired on until she is inside
    /// <see cref="ViewDistanceSquares"/> (SEA_5 §7.5).
    /// </summary>
    public const float MinimapRadiusSquares = ViewDistanceSquares * 2f;

    /// <summary>Half of base is as much as any debuff can take (SEA_5 §7.6).</summary>
    public const float DebuffFloorFraction = 0.50f;

    /// <summary>
    /// A range or view debuff subtracts flat squares, floored at half of base.
    /// Flat rather than proportional, because a fixed fraction would cost a tier
    /// 5 gun 3 squares where it costs a tier 1 gun 1.8, which is backwards: the
    /// cheap gun is the one that needs the room.
    /// </summary>
    public static float DebuffedSquares(float baseSquares, float subtractedSquares) =>
        MathF.Max(
            baseSquares * DebuffFloorFraction,
            baseSquares - MathF.Max(0f, subtractedSquares));
}
