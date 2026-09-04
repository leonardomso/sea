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

    /// <summary>
    /// The widest radius any authored map object may declare -- island, reef, harbor
    /// or storm alike, since <c>ValidateObjects</c> applies it to every one of them.
    /// Sized to the storm, which SEA_5 §5.2 puts at 40 squares and which is the
    /// largest thing the specification describes.
    /// </summary>
    public const float MaximumWorldInfluenceRadiusSquares = 40f;

    /// <summary>
    /// The widest an authored current zone may be. SEA_5 bounds current drift and never a
    /// zone's size, so this stays a placeholder rather than a specified figure: it is set to
    /// Havenmere's widest zone (current zone 1, rescaled to 56 squares) rather than to the
    /// 40-square storm bound, because a current is drift, not a hazard, and there is no
    /// reason yet to believe it needs the same ceiling. No specification backs 56 -- it is
    /// the widest zone the content happens to author today. Phase 8 sets the real ceiling
    /// when the maps are drawn by hand instead of bridged from the old chart.
    /// </summary>
    public const float MaximumCurrentRadiusSquares = 56f;

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
