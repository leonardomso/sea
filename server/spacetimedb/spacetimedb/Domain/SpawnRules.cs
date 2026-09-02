namespace Sea.Server;

public readonly struct SpawnBlocker
{
    public SpawnBlocker(float x, float y, float radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }
}

public readonly struct SpawnPoint
{
    public SpawnPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }
    public float Y { get; }
}

public static class SpawnRules
{
    public const float EdgeMargin = 5f;
    public const float Separation = 5f;
    public const int MaximumAttempts = 256;

    public static bool TryFindSafePosition(
        ulong seed,
        IReadOnlyList<SpawnBlocker> blockers,
        out SpawnPoint point)
    {
        ArgumentNullException.ThrowIfNull(blockers);

        var random = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        var minimum = WorldRules.MapMin + EdgeMargin;
        var span = WorldRules.MapMax - WorldRules.MapMin - EdgeMargin * 2f;
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var x = minimum + NextUnit(ref random) * span;
            var y = minimum + NextUnit(ref random) * span;
            if (IsClear(x, y, blockers))
            {
                point = new SpawnPoint(x, y);
                return true;
            }
        }

        point = default;
        return false;
    }

    private static bool IsClear(float x, float y, IReadOnlyList<SpawnBlocker> blockers)
    {
        for (var index = 0; index < blockers.Count; index++)
        {
            if (Overlaps(x, y, blockers[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool Overlaps(float x, float y, SpawnBlocker blocker)
    {
        var deltaX = x - blocker.X;
        var deltaY = y - blocker.Y;
        var radius = blocker.Radius + Separation;
        return deltaX * deltaX + deltaY * deltaY < radius * radius;
    }

    private static float NextUnit(ref ulong state)
    {
        state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
        return (float)((state >> 40) / 16_777_216d);
    }
}
