namespace Sea.Server;

/// <summary>
/// What the water a hull is sitting in does to her, for everything the debuff
/// floor applies to.
/// </summary>
/// <remarks>
/// Turn rate is gone with the rest of the inertia model, and so is the weapon
/// effectiveness term, which no caller ever read. The storm is not here: SEA_5
/// §5.1 puts it outside the 0.50 floor, so SpeedRules owns it and this owns
/// only the terms the floor binds.
/// </remarks>
public readonly record struct TacticalModifiers(float SpeedMultiplier);

public static class TacticalRules
{
    /// <summary>Shallow water a tier-1 to tier-3 hull can cross, slowly.</summary>
    public const float ShoalMultiplier = 0.65f;

    /// <summary>A hull under repair holds station rather than running.</summary>
    public const float RepairingMultiplier = 0.5f;

    public static TacticalModifiers Resolve(
        bool slowed,
        float slowMagnitude,
        bool inShoal,
        bool repairing)
    {
        var multiplier = slowed ? 1f - Math.Clamp(slowMagnitude, 0f, 1f) : 1f;
        if (inShoal)
        {
            multiplier *= ShoalMultiplier;
        }

        if (repairing)
        {
            multiplier *= RepairingMultiplier;
        }

        return new TacticalModifiers(multiplier);
    }

    /// <summary>
    /// Where a storm has drifted to. A storm that reaches the border stops
    /// against it and stays until it blows out; it used to be teleported to the
    /// opposite edge, which put a squall on top of a harbour with no warning.
    /// </summary>
    public static (float X, float Y) MoveStorm(
        float positionX,
        float positionY,
        float directionDegrees,
        float speedSquaresPerSecond,
        float deltaSeconds)
    {
        var (directionX, directionY) = GeometryRules.Direction(directionDegrees);
        var travel = speedSquaresPerSecond * deltaSeconds;
        return WorldRules.ClampToMap(
            positionX + (directionX * travel),
            positionY + (directionY * travel));
    }
}
