namespace Sea.Server;

/// <summary>Everything that decides how fast a hull is moving this tick.</summary>
public readonly record struct SpeedInputs(
    float BaseSquaresPerSecond,
    float BonusFraction,
    uint Hull,
    uint MaxHull,
    float HeadingDegrees,
    float WindDirectionDegrees,
    bool InStorm,
    float DebuffMultiplier,
    bool IsFrozen);

/// <summary>
/// SEA_5 §5.1, and the only place effective speed is worked out. The answer is in
/// squares per second; there is no other unit.
/// </summary>
/// <remarks>
/// <para>
/// This used to be four calculations in four files. <c>TacticalRules</c> holds the
/// debuff product and points the storm at the turn rate rather than at speed;
/// <c>EnvironmentRules</c> holds a random wind strength on a thirty-second clock;
/// <c>EffectRules</c> floors one term at 0.1, so the product of two slows could
/// reach 0.01 of a hull's rating; and the sailing step multiplied whatever came out
/// by whatever it had cached. Every one of those was a bug, and none of them was
/// visible from any of the others.
/// </para>
/// <para>
/// The order of the multiplications is the order SEA_5 §5.1 prints them in. It is
/// not arithmetically load-bearing — they all commute — but keeping it means a
/// reader can check this file against the document line by line, and the floats
/// come out with the same bits the replay hash was pinned against.
/// </para>
/// </remarks>
public static class SpeedRules
{
    /// <summary>
    /// SEA_5 §5.1 line 185 and the constants table line 479. The document was
    /// drafted at 0.20 and amended to 0.25 in Task 0.1, to record the cap
    /// <c>Content/Data/stat_caps.json</c> has actually shipped since Milestone 1;
    /// there is no longer a conflict here for a reader to resolve.
    /// </summary>
    public const float BonusCap = 0.25f;

    /// <summary>Above half hull a ship sails at her rating (SEA_5 §5.2).</summary>
    public const float NormalHpMultiplier = 1.00f;

    /// <summary>Between a quarter and half her hull, a ship is Damaged.</summary>
    public const float DamagedHpMultiplier = 0.92f;

    /// <summary>At a quarter hull or less, a ship is Burning.</summary>
    public const float BurningHpMultiplier = 0.85f;

    /// <summary>The hull fraction at or below which a ship counts as Damaged.</summary>
    public const float DamagedHpFraction = 0.50f;

    /// <summary>The hull fraction at or below which a ship counts as Burning.</summary>
    public const float BurningHpFraction = 0.25f;

    /// <summary>Downwind is this much faster, upwind this much slower.</summary>
    public const float WindStrength = 0.10f;

    /// <summary>Inside a storm a ship makes this fraction of her way (SEA_5 §5.2).</summary>
    public const float StormMultiplier = 0.85f;

    /// <summary>Slows multiply, but never take a hull below half her way.</summary>
    public const float DebuffFloor = 0.50f;

    /// <summary>
    /// The three-state hull penalty. A ship with no rated hull is not a damaged
    /// ship, she is a ship nobody has given a maximum, so she sails at her rating
    /// rather than dividing by zero.
    /// </summary>
    public static float HpStateMultiplier(uint hull, uint maxHull)
    {
        if (maxHull == 0)
        {
            return NormalHpMultiplier;
        }

        var fraction = (float)hull / maxHull;
        if (fraction <= BurningHpFraction)
        {
            return BurningHpMultiplier;
        }

        return fraction <= DamagedHpFraction ? DamagedHpMultiplier : NormalHpMultiplier;
    }

    /// <summary>
    /// The wind's direction is the way it blows, so a hull on the same bearing is
    /// running before it and gains a tenth, one on the opposite bearing loses a
    /// tenth, and one across it gains nothing either way.
    /// </summary>
    /// <remarks>
    /// The difference of two bearings goes through
    /// <see cref="GeometryRules.NormalizeSignedAngle"/> rather than straight into the
    /// cosine: it is the one place this file compares two compass bearings, and the
    /// geometry layer owns that. The cosine itself is
    /// <see cref="TrigonometryRules"/>, sampled every quarter degree, so the answer
    /// can sit a fraction of a per cent off the continuous curve. That is deliberate
    /// — the table is what every platform agrees on.
    /// </remarks>
    public static float WindMultiplier(float headingDegrees, float windDirectionDegrees)
    {
        var offset = GeometryRules.NormalizeSignedAngle(headingDegrees - windDirectionDegrees);
        return 1f + (WindStrength * TrigonometryRules.CosDegrees(offset));
    }

    /// <summary>How fast this hull is actually moving, in squares per second.</summary>
    public static float Effective(in SpeedInputs inputs)
    {
        if (inputs.IsFrozen)
        {
            // Freeze is not a slow: the ship stops dead and keeps her course, and
            // picks it up again when it lifts (SEA_5 §5.2).
            return 0f;
        }

        var speed = inputs.BaseSquaresPerSecond;
        speed *= 1f + Math.Clamp(inputs.BonusFraction, 0f, BonusCap);
        speed *= HpStateMultiplier(inputs.Hull, inputs.MaxHull);
        speed *= WindMultiplier(inputs.HeadingDegrees, inputs.WindDirectionDegrees);
        if (inputs.InStorm)
        {
            speed *= StormMultiplier;
        }

        // The floor is on the product, not on each slow. Floored term by term, a
        // chained and grapeshotted hull ended up at a hundredth of her rating.
        speed *= Math.Clamp(inputs.DebuffMultiplier, DebuffFloor, 1f);
        return MathF.Max(0f, speed);
    }
}
