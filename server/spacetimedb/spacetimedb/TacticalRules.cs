namespace Sea.Server;

public readonly record struct TacticalModifiers(
    float MaximumSpeed,
    float Acceleration,
    float TurnRate,
    float WeaponEffectiveness);

public readonly record struct HazardPosition(float X, float Y);

public static class TacticalRules
{
    /// <summary>
    /// How a ship's own state and the water it is in scale its sailing parameters. With the sail
    /// pool gone the only handling penalties left are the Chain Shot slow, shoals, storms, and
    /// holding station to repair.
    /// </summary>
    public static TacticalModifiers MovementModifiers(
        bool slowed,
        float slowMagnitude,
        bool inShoal,
        bool inStorm,
        bool repairing)
    {
        var maximumSpeed = EffectRules.SpeedMultiplier(slowed, slowMagnitude);

        if (inShoal)
        {
            maximumSpeed *= 0.65f;
        }

        if (repairing)
        {
            maximumSpeed *= 0.5f;
        }

        return new TacticalModifiers(
            maximumSpeed,
            1f,
            inStorm ? 0.65f : 1f,
            inStorm ? 0.75f : 1f);
    }

    /// <summary>
    /// Where a storm on <paramref name="directionDegrees"/> stands after
    /// <paramref name="deltaSeconds"/>. A storm bearing north travels up the screen,
    /// so it reads its vector from <see cref="GeometryRules.Direction"/> like every
    /// other moving thing; the private <c>MathF.Cos</c> it used to keep here was
    /// north-positive and drove every storm the opposite way to its own bearing.
    /// </summary>
    public static HazardPosition MoveStorm(
        float x,
        float y,
        float directionDegrees,
        float speed,
        float deltaSeconds)
    {
        var (headingX, headingY) = GeometryRules.Direction(directionDegrees);
        var nextX = x + headingX * speed * deltaSeconds;
        var nextY = y + headingY * speed * deltaSeconds;
        return new HazardPosition(WrapMapCoordinate(nextX), WrapMapCoordinate(nextY));
    }

    private static float WrapMapCoordinate(float value)
    {
        const float span = WorldRules.MapSizeSquares;
        while (value > WorldRules.MapMax)
        {
            value -= span;
        }

        while (value < WorldRules.MapMin)
        {
            value += span;
        }

        return value;
    }
}
