namespace Sea.Server;

/// <summary>
/// Where a map's land is, one bit per square. This is what movement means by
/// "land": the authored islands and reefs are shapes a person drew and the
/// client draws back, but nothing in the simulation asks a shape a question.
/// </summary>
/// <remarks>
/// A 400-square map is 160,000 bits, 2,500 words, 20 KB. It is built once when
/// content is loaded and never written again, so it can be shared by every
/// reader on the tick without a copy.
/// </remarks>
public sealed class LandMask
{
    private readonly ulong[] bits;

    public LandMask(int size, ulong[] bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        var expected = WordCount(size);
        if (bits.Length != expected)
        {
            throw new ArgumentException(
                $"a {size} x {size} mask needs {expected} words, not {bits.Length}",
                nameof(bits));
        }

        Size = size;
        this.bits = bits;
    }

    /// <summary>Squares on a side.</summary>
    public int Size { get; }

    public static int WordCount(int size) => ((size * size) + 63) / 64;

    /// <summary>
    /// Whether a square is land. Anything off the map is land as well, so the
    /// map edge needs no separate check anywhere: a route cannot leave the sea
    /// and drift cannot push a hull past the border.
    /// </summary>
    public bool IsLandCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0 || cellX >= Size || cellY >= Size)
        {
            return true;
        }

        var index = (cellY * Size) + cellX;
        return (bits[index >> 6] & (1UL << (index & 63))) != 0UL;
    }

    public bool IsLand(float x, float y) =>
        IsLandCell((int)MathF.Floor(x), (int)MathF.Floor(y));

    /// <summary>
    /// Whether a straight line from one point to another stays on water. This
    /// walks every square the line actually touches rather than sampling it, so
    /// a course cannot slip through the corner between two rocks.
    /// </summary>
    public bool SegmentIsClear(float startX, float startY, float endX, float endY)
    {
        var cellX = (int)MathF.Floor(startX);
        var cellY = (int)MathF.Floor(startY);
        if (IsLandCell(cellX, cellY))
        {
            return false;
        }

        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var stepX = deltaX > 0f ? 1 : deltaX < 0f ? -1 : 0;
        var stepY = deltaY > 0f ? 1 : deltaY < 0f ? -1 : 0;
        var perCellX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / deltaX);
        var perCellY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / deltaY);
        var nextX = stepX == 0
            ? float.PositiveInfinity
            : (stepX > 0 ? cellX + 1 - startX : startX - cellX) * perCellX;
        var nextY = stepY == 0
            ? float.PositiveInfinity
            : (stepY > 0 ? cellY + 1 - startY : startY - cellY) * perCellY;

        // The walk ends by distance along the ray, not by reaching the end cell:
        // a diagonal step crosses two boundaries at once and can pass a corner
        // straight over the end cell without ever landing on it. Both boundaries
        // being a full segment length away means the ray stops before it enters
        // another cell, and an entry at exactly one length means it stops on that
        // edge rather than crossing it.
        while (nextX < 1f || nextY < 1f)
        {
            if (nextX < nextY)
            {
                cellX += stepX;
                nextX += perCellX;
            }
            else if (nextY < nextX)
            {
                cellY += stepY;
                nextY += perCellY;
            }
            else
            {
                if (CornerIsBlocked(cellX, cellY, stepX, stepY))
                {
                    return false;
                }

                cellX += stepX;
                nextX += perCellX;
                cellY += stepY;
                nextY += perCellY;
            }

            if (IsLandCell(cellX, cellY))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the corner a diagonal ray is passing exactly through is pinched by
    /// land on either side of it. A hull cannot cut between two rocks that meet at
    /// a point, so both cells flanking the corner have to be open, not just the one
    /// the walk in <see cref="SegmentIsClear"/> would otherwise step into first.
    /// </summary>
    private bool CornerIsBlocked(int cellX, int cellY, int stepX, int stepY) =>
        IsLandCell(cellX + stepX, cellY) || IsLandCell(cellX, cellY + stepY);

    /// <summary>
    /// The nearest square of water to a point, searched outward a ring at a
    /// time. SEA_5 §4.1.2 uses this to nudge a click that landed on an island;
    /// beyond <paramref name="searchSquares"/> the click is refused instead.
    /// </summary>
    public bool TryNearestWater(
        float x,
        float y,
        float searchSquares,
        out float waterX,
        out float waterY)
    {
        waterX = x;
        waterY = y;
        if (!IsLand(x, y))
        {
            return true;
        }

        var originX = (int)MathF.Floor(x);
        var originY = (int)MathF.Floor(y);
        var rings = (int)MathF.Ceiling(searchSquares);
        var bestSquared = searchSquares * searchSquares;
        var found = false;
        for (var ring = 1; ring <= rings; ring++)
        {
            for (var offsetY = -ring; offsetY <= ring; offsetY++)
            {
                for (var offsetX = -ring; offsetX <= ring; offsetX++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != ring)
                    {
                        continue;
                    }

                    var cellX = originX + offsetX;
                    var cellY = originY + offsetY;
                    if (IsLandCell(cellX, cellY))
                    {
                        continue;
                    }

                    var centerX = cellX + 0.5f;
                    var centerY = cellY + 0.5f;
                    var distanceSquared = GeometryRules.DistanceSquared(x, y, centerX, centerY);
                    if (distanceSquared >= bestSquared)
                    {
                        continue;
                    }

                    bestSquared = distanceSquared;
                    waterX = centerX;
                    waterY = centerY;
                    found = true;
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }
}
