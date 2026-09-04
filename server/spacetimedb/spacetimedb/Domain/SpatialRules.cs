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
    /// allowed to be. This gate therefore keeps its pre-migration value rather than
    /// borrowing the storm radius, which would raise a publish gate on a number no
    /// specification backs.
    /// </summary>
    /// <remarks>
    /// The 28 is not yet a number of squares, whatever the name says. It came across
    /// unchanged from the world-unit scale, where Havenmere's widest current zone
    /// (<c>maps.json</c> zone 1) reads 28 units on a 200-unit chart -- 2.8 squares.
    /// Task 1.6 doubles that radius to 56 when it rescales the content, which this
    /// gate rejects and the storm radius of 40 would reject too. Task 1.6 has to
    /// settle the real ceiling; until it does, this number is a placeholder in the
    /// right place rather than a measurement.
    /// </remarks>
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
