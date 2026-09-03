namespace Sea.Server;

public enum RepairRejection
{
    None,
    SourceSunk,
    Busy,
    NoRepairKit,
    NothingToRepair,
}

public readonly record struct RepairRequest(
    bool SourceAlive,
    bool IsIdle,
    bool HasRepairKit,
    bool IsDamaged);

public readonly record struct TacticalModifiers(
    float MaximumSpeed,
    float Acceleration,
    float TurnRate,
    float WeaponEffectiveness);

public readonly record struct HazardPosition(float X, float Y);

public static class TacticalRules
{
    public const uint RepairDurationTicks = 30;

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

    public static RepairRejection ValidateRepair(RepairRequest request)
    {
        if (!request.SourceAlive)
        {
            return RepairRejection.SourceSunk;
        }

        if (!request.IsIdle)
        {
            return RepairRejection.Busy;
        }

        if (!request.HasRepairKit)
        {
            return RepairRejection.NoRepairKit;
        }

        return request.IsDamaged ? RepairRejection.None : RepairRejection.NothingToRepair;
    }

    public static uint ProgressiveRestore(
        uint initial,
        uint maximum,
        uint restoreAmount,
        ulong elapsedTicks,
        uint durationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfZero(durationTicks);

        var progress = Math.Clamp((float)elapsedTicks / durationTicks, 0f, 1f);
        var restored = (uint)MathF.Round(
            restoreAmount * progress,
            MidpointRounding.AwayFromZero);
        return Math.Min(maximum, checked(initial + restored));
    }

    public static HazardPosition MoveStorm(
        float x,
        float y,
        float directionDegrees,
        float speed,
        float deltaSeconds)
    {
        var radians = directionDegrees * MathF.PI / 180f;
        var nextX = x + MathF.Sin(radians) * speed * deltaSeconds;
        var nextY = y + MathF.Cos(radians) * speed * deltaSeconds;
        return new HazardPosition(WrapMapCoordinate(nextX), WrapMapCoordinate(nextY));
    }

    private static float WrapMapCoordinate(float value)
    {
        var span = WorldRules.MapMax - WorldRules.MapMin;
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
