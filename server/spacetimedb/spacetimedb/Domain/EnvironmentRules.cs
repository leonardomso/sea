namespace Sea.Server;

public readonly struct WindSnapshot
{
    public WindSnapshot(float directionDegrees, float strength)
    {
        DirectionDegrees = directionDegrees;
        Strength = strength;
    }

    public float DirectionDegrees { get; }
    public float Strength { get; }
}

public static class EnvironmentRules
{
    public const ulong WindEpochTicks = 300;

    public static WindSnapshot WindForEpoch(ulong seed, ulong epoch)
    {
        var state = unchecked(seed ^ (epoch + 1) * 0x9E3779B97F4A7C15UL);
        state = Mix(state);
        var direction = (float)((state >> 32) / (double)uint.MaxValue * 360d);
        state = Mix(state);
        var strength = 0.2f + (float)((state >> 32) / (double)uint.MaxValue * 0.6d);
        return new WindSnapshot(direction, strength);
    }

    public static float WindSpeedMultiplier(
        float headingDegrees,
        float windDirectionDegrees,
        float windStrength)
    {
        var difference = headingDegrees - windDirectionDegrees;
        return 1f + TrigonometryRules.CosDegrees(difference) *
            Math.Clamp(windStrength, 0f, 1f) * 0.15f;
    }

    /// <summary>
    /// The velocity a set of <paramref name="strength"/> squares per second on
    /// bearing <paramref name="directionDegrees"/> imparts. A northward set carries
    /// a hull up the screen, so its Y component is negative (SEA_5 §3.3).
    /// </summary>
    /// <remarks>
    /// This is <see cref="GeometryRules.Direction"/> scaled, and it has to stay that
    /// way: the second component used to be a bare <c>CosDegrees</c>, which is
    /// north-positive, so every current zone pushed south where the content said
    /// north. One place turns a bearing into a vector.
    /// </remarks>
    public static (float X, float Y) DirectionalVelocity(
        float directionDegrees,
        float strength)
    {
        var (x, y) = GeometryRules.Direction(directionDegrees);
        return (x * strength, y * strength);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
