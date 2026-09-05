namespace Sea.Server;

public readonly struct NavigationBlocker
{
    public NavigationBlocker(float x, float y, float radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }
}

/// <summary>
/// What is left of the circle-and-detour navigator: the nudge that pulls a point out of
/// an obstacle. Courses are laid on the land mask by <see cref="PathfindingRules"/> now,
/// and only the NPC steering still reasons about blockers as circles.
/// </summary>
public static class NavigationRules
{
    public const float DetourClearance = 4f;
    private const int RingSamples = 16;

    public static bool IsDestinationBlocked(
        float x,
        float y,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        foreach (var blocker in blockers)
        {
            if (Distance(x, y, blocker.X, blocker.Y) <=
                blocker.Radius + WorldRules.LandHazardPadding)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pulls a point that sits inside a blocker back out to open water on the side
    /// nearest to it, so a course plotted at an island still has somewhere to go.
    /// </summary>
    public static SpawnPoint NearestClearPoint(
        float x,
        float y,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        var point = new SpawnPoint(x, y);
        // Leaving one blocker can land inside a neighbour, so sweep a few times.
        for (var pass = 0; pass < 4; pass++)
        {
            var moved = false;
            foreach (var blocker in blockers)
            {
                var deltaX = point.X - blocker.X;
                var deltaY = point.Y - blocker.Y;
                var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance > blocker.Radius + WorldRules.LandHazardPadding)
                {
                    continue;
                }

                if (distance <= 0.001f)
                {
                    deltaX = 1f;
                    deltaY = 0f;
                    distance = 1f;
                }

                var clearance = blocker.Radius + WorldRules.LandHazardPadding + DetourClearance;
                point = new SpawnPoint(
                    blocker.X + deltaX / distance * clearance,
                    blocker.Y + deltaY / distance * clearance);
                moved = true;
            }

            if (!moved)
            {
                return point;
            }
        }

        // Overlapping blockers can bounce the nudge between them; widen a ring
        // around the original point until open water turns up.
        for (var radius = DetourClearance; radius <= WorldRules.MapSizeSquares; radius += DetourClearance)
        {
            for (var step = 0; step < RingSamples; step++)
            {
                var angle = step * MathF.PI * 2f / RingSamples;
                var candidate = new SpawnPoint(
                    x + MathF.Cos(angle) * radius,
                    y + MathF.Sin(angle) * radius);
                if (!IsDestinationBlocked(candidate.X, candidate.Y, blockers))
                {
                    return candidate;
                }
            }
        }

        return point;
    }

    public static float Distance(float fromX, float fromY, float toX, float toY)
    {
        var x = toX - fromX;
        var y = toY - fromY;
        return MathF.Sqrt(x * x + y * y);
    }
}
