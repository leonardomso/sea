namespace Sea.LoadTests;

public sealed record LoadWorkloadPlan(
    int ClientIndex,
    LoadClientMode Mode,
    uint Seed)
{
    public static LoadWorkloadPlan Create(
        int clientIndex,
        int totalClients,
        int activeClients)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalClients);
        ArgumentOutOfRangeException.ThrowIfNegative(activeClients);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(activeClients, totalClients);
        ArgumentOutOfRangeException.ThrowIfNegative(clientIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(clientIndex, totalClients);

        return new LoadWorkloadPlan(
            clientIndex,
            clientIndex < activeClients ? LoadClientMode.Sailing : LoadClientMode.Dormant,
            Mix((uint)clientIndex + 1));
    }

    public IReadOnlyList<LoadCourse> CourseAttempts(
        float currentX,
        float currentY,
        uint cycle = 0)
    {
        const float mapLimit = 95f;
        var corners = new[]
        {
            new LoadCourse(-mapLimit, -mapLimit),
            new LoadCourse(-mapLimit, mapLimit),
            new LoadCourse(mapLimit, -mapLimit),
            new LoadCourse(mapLimit, mapLimit),
        };
        var distantCorners = corners
            .Where(course => DistanceSquared(course, currentX, currentY) >= 14_400f)
            .ToArray();
        var cycleSeed = Mix(Seed ^ Mix(cycle + 1));
        var candidates = Enumerable.Range(0, 64)
            .Select(index => CourseToMapEdge(cycleSeed, (uint)index))
            .Where(course => DistanceSquared(course, currentX, currentY) >= 14_400f)
            .Take(8)
            .ToArray();
        return candidates.Length > 0
            ? candidates
            : [distantCorners[(int)(((uint)ClientIndex + cycle) % (uint)distantCorners.Length)]];

        LoadCourse CourseToMapEdge(uint seed, uint index)
        {
            var random = Mix(seed ^ (index * 0x9E3779B9u + 1));
            var radians = random / (double)uint.MaxValue * Math.PI * 2d;
            var directionX = (float)Math.Sin(radians);
            var directionY = (float)Math.Cos(radians);
            var distanceX = DistanceToBoundary(currentX, directionX);
            var distanceY = DistanceToBoundary(currentY, directionY);
            var distance = MathF.Min(distanceX, distanceY);
            return new LoadCourse(
                Math.Clamp(currentX + directionX * distance, -mapLimit, mapLimit),
                Math.Clamp(currentY + directionY * distance, -mapLimit, mapLimit));
        }

        static float DistanceToBoundary(float coordinate, float direction) =>
            MathF.Abs(direction) <= 0.000001f
                ? float.PositiveInfinity
                : ((direction > 0f ? mapLimit : -mapLimit) - coordinate) / direction;

        static float DistanceSquared(LoadCourse course, float x, float y)
        {
            var deltaX = course.X - x;
            var deltaY = course.Y - y;
            return deltaX * deltaX + deltaY * deltaY;
        }
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352d;
        value ^= value >> 15;
        value *= 0x846ca68b;
        value ^= value >> 16;
        return value;
    }
}
