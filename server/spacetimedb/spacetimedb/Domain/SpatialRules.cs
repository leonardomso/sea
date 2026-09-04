namespace Sea.Server;

public static class SpatialRules
{
    /// <summary>
    /// The map is cut into an 8 x 8 grid of chunks, so one chunk is 50 squares on a
    /// side. Keeping the count at eight keeps every chunk index and every
    /// subscription shape the module already has; only the size changes to cover
    /// the 400-square map (SEA_5 §3.1).
    /// </summary>
    public const float ChunkSizeSquares = 50f;

    public const int ChunkCountPerAxis = 8;

    /// <summary>The widest a storm reaches, so a chunk query can bound it (SEA_5 §5.2).</summary>
    public const float MaximumWorldInfluenceRadiusSquares = 40f;

    /// <summary>
    /// The widest a current zone reaches. SEA_5 §5.2 only bounds current DRIFT (at
    /// most 0.3 sq/s); it says nothing about how wide a current zone itself is
    /// allowed to be. This gate keeps its pre-migration value rather than adopting
    /// the storm radius: 28 squares is the largest current zone Havenmere's own
    /// content authors today (<c>maps.json</c> zone 1). Raise it only once a map
    /// actually needs a wider current zone.
    /// </summary>
    public const float MaximumCurrentRadiusSquares = 28f;

    public static int ChunkCoordinate(float position)
    {
        if (!float.IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var coordinate = (int)MathF.Floor(position / ChunkSizeSquares);
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
