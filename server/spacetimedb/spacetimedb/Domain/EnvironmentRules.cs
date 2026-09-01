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
        var difference = (headingDegrees - windDirectionDegrees) * (MathF.PI / 180f);
        return 1f + MathF.Cos(difference) * Math.Clamp(windStrength, 0f, 1f) * 0.15f;
    }

    public static (float X, float Y) DirectionalVelocity(
        float directionDegrees,
        float strength)
    {
        var radians = directionDegrees * (MathF.PI / 180f);
        return (MathF.Sin(radians) * strength, MathF.Cos(radians) * strength);
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
