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

public static class NavigationRules
{
    public const float DetourClearance = 4f;
    public const float WaypointArrivalRadius = 2.5f;
    private const int RingSamples = 16;

    public static bool IsDestinationBlocked(
        float x,
        float y,
        IReadOnlyCollection<NavigationBlocker> blockers) => blockers.Any(blocker =>
        Distance(x, y, blocker.X, blocker.Y) <=
        blocker.Radius + WorldRules.LandHazardPadding);

    public static bool TryFindDetour(
        float startX,
        float startY,
        float destinationX,
        float destinationY,
        IReadOnlyCollection<NavigationBlocker> blockers,
        out SpawnPoint waypoint)
    {
        waypoint = default;
        var courseX = destinationX - startX;
        var courseY = destinationY - startY;
        var courseLength = MathF.Sqrt(courseX * courseX + courseY * courseY);
        if (courseLength <= 0.001f)
        {
            return false;
        }

        var directionX = courseX / courseLength;
        var directionY = courseY / courseLength;
        var obstacle = NearestBlocker(
            startX,
            startY,
            destinationX,
            destinationY,
            directionX,
            directionY,
            blockers);
        if (obstacle is not NavigationBlocker nearest)
        {
            return false;
        }

        var perpendicularX = -directionY;
        var perpendicularY = directionX;
        var offset = nearest.Radius + WorldRules.LandHazardPadding + DetourClearance;
        var first = new SpawnPoint(
            nearest.X + perpendicularX * offset,
            nearest.Y + perpendicularY * offset);
        var second = new SpawnPoint(
            nearest.X - perpendicularX * offset,
            nearest.Y - perpendicularY * offset);
        var firstScore = CandidateScore(
            startX, startY, destinationX, destinationY, first, blockers);
        var secondScore = CandidateScore(
            startX, startY, destinationX, destinationY, second, blockers);
        if (!float.IsFinite(firstScore) && !float.IsFinite(secondScore))
        {
            return false;
        }

        waypoint = firstScore <= secondScore ? first : second;
        return true;
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

    private static NavigationBlocker? NearestBlocker(
        float startX,
        float startY,
        float destinationX,
        float destinationY,
        float directionX,
        float directionY,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        NavigationBlocker? nearest = null;
        var nearestProjection = float.MaxValue;
        foreach (var blocker in blockers)
        {
            var collisionRadius = blocker.Radius + WorldRules.LandHazardPadding;
            if (!GeometryRules.SegmentIntersectsCircle(
                    startX, startY, destinationX, destinationY,
                    blocker.X, blocker.Y, collisionRadius))
            {
                continue;
            }

            var projection = (blocker.X - startX) * directionX +
                (blocker.Y - startY) * directionY;
            if (projection >= 0f && projection < nearestProjection)
            {
                nearest = blocker;
                nearestProjection = projection;
            }
        }

        return nearest;
    }

    public static float Distance(float fromX, float fromY, float toX, float toY)
    {
        var x = toX - fromX;
        var y = toY - fromY;
        return MathF.Sqrt(x * x + y * y);
    }

    private static float CandidateScore(
        float startX,
        float startY,
        float destinationX,
        float destinationY,
        SpawnPoint candidate,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        if (!WorldRules.IsInsideMap(candidate.X, candidate.Y) ||
            !SegmentIsClear(startX, startY, candidate.X, candidate.Y, blockers))
        {
            return float.PositiveInfinity;
        }

        var score = Distance(startX, startY, candidate.X, candidate.Y) +
            Distance(candidate.X, candidate.Y, destinationX, destinationY);
        if (!SegmentIsClear(
                candidate.X,
                candidate.Y,
                destinationX,
                destinationY,
                blockers))
        {
            score += 10_000f;
        }

        return score;
    }

    private static bool SegmentIsClear(
        float startX,
        float startY,
        float endX,
        float endY,
        IReadOnlyCollection<NavigationBlocker> blockers) => blockers.All(blocker =>
        !GeometryRules.SegmentIntersectsCircle(
            startX,
            startY,
            endX,
            endY,
            blocker.X,
            blocker.Y,
            blocker.Radius + WorldRules.LandHazardPadding));
}
