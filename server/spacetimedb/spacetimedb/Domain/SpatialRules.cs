namespace Sea.Server;

public static class SpatialRules
{
    public const float ChunkSize = 25f;
    public const int ChunkCountPerAxis = 8;
    public const float MaximumWorldInfluenceRadius = 16f;
    public const float MaximumCurrentRadius = 28f;

    public static int ChunkCoordinate(float position)
    {
        if (!float.IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var coordinate = (int)MathF.Floor((position - WorldRules.MapMin) / ChunkSize);
        return Math.Clamp(coordinate, 0, ChunkCountPerAxis - 1);
    }

    public static ChunkBounds BoundsAround(float x, float y, float radius)
    {
        if (!float.IsFinite(radius) || radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        return new ChunkBounds(
            ChunkCoordinate(x - radius),
            ChunkCoordinate(x + radius),
            ChunkCoordinate(y - radius),
            ChunkCoordinate(y + radius));
    }

    public static ChunkBounds BoundsForSegment(
        float startX,
        float startY,
        float endX,
        float endY,
        float padding) => new(
            ChunkCoordinate(MathF.Min(startX, endX) - padding),
            ChunkCoordinate(MathF.Max(startX, endX) + padding),
            ChunkCoordinate(MathF.Min(startY, endY) - padding),
            ChunkCoordinate(MathF.Max(startY, endY) + padding));
}

public readonly record struct ChunkBounds(int MinX, int MaxX, int MinY, int MaxY);
