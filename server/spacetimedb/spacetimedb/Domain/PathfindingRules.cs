namespace Sea.Server;

/// <summary>
/// The working memory one A* search needs, sized for one map and reused for
/// every search on it.
/// </summary>
/// <remarks>
/// A 400-square map is 160,000 cells. Clearing four arrays that size on every
/// MoveTo would cost more than the search, so each cell carries the search
/// number that last wrote it and anything stamped with an older number reads
/// as untouched.
/// </remarks>
public sealed class PathfindingScratch
{
    /// <summary>The longest raw cell path a search will reconstruct.</summary>
    public const int MaximumCorners = 4096;

    internal readonly float[] Cost;
    internal readonly uint[] Stamp;
    internal readonly int[] CameFrom;
    internal readonly bool[] Closed;
    internal readonly int[] HeapCell;
    internal readonly float[] HeapScore;
    internal readonly RouteWaypoint[] Corners;
    internal uint Search;
    internal int HeapCount;

    public PathfindingScratch(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        Size = size;
        var cells = size * size;
        Cost = new float[cells];
        Stamp = new uint[cells];
        CameFrom = new int[cells];
        Closed = new bool[cells];
        HeapCell = new int[cells];
        HeapScore = new float[cells];
        Corners = new RouteWaypoint[MaximumCorners];
    }

    public int Size { get; }
}

public enum PathOutcome : byte
{
    /// <summary>The straight line was clear; the route is one segment.</summary>
    Direct = 0,

    /// <summary>A* found a way round; the route has two or more segments.</summary>
    Routed = 1,

    /// <summary>There is no way there (SEA_5 §4.1.5, rejected with NO_PATH).</summary>
    NoPath = 2,
}

/// <summary>
/// Plotting a course round land: SEA_5 §4.1.5. The straight line is tried
/// first, then eight-direction A* on the one-square mask with diagonal cost
/// sqrt(2), then the cell path is pulled straight into as few water-only legs
/// as will fit in 32 waypoints.
/// </summary>
/// <remarks>
/// This runs on a player's command, on the tick thread, up to eight times a
/// second per ship. The straight-line test answers the overwhelming majority
/// of requests without a search at all; a search that has not finished within
/// <see cref="MaximumExpansions"/> cells is refused rather than allowed to
/// spend a tick, because a course that hard is a course into a lake.
/// </remarks>
public static class PathfindingRules
{
    /// <summary>How far a click may be nudged off land onto water (SEA_5 §4.1.2).</summary>
    public const float NudgeSearchSquares = 3f;

    /// <summary>The cell budget for one search.</summary>
    public const int MaximumExpansions = 20000;

    private const float DiagonalCost = 1.41421356f;

    private static readonly int[] NeighbourX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighbourY = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly float[] NeighbourCost =
    {
        1f, DiagonalCost, 1f, DiagonalCost, 1f, DiagonalCost, 1f, DiagonalCost,
    };

    public static PathOutcome TryBuildRoute(
        LandMask mask,
        PathfindingScratch scratch,
        float startX,
        float startY,
        float goalX,
        float goalY,
        Span<RouteWaypoint> route,
        out int count)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(scratch);
        count = 0;
        if (mask.Size != scratch.Size)
        {
            throw new ArgumentException("the scratch was sized for another map", nameof(scratch));
        }

        if (mask.IsLand(goalX, goalY) || mask.IsLand(startX, startY))
        {
            return PathOutcome.NoPath;
        }

        if (mask.SegmentIsClear(startX, startY, goalX, goalY))
        {
            route[0] = new RouteWaypoint(goalX, goalY);
            count = 1;
            return PathOutcome.Direct;
        }

        var cells = Search(mask, scratch, startX, startY, goalX, goalY);
        if (cells <= 0)
        {
            return PathOutcome.NoPath;
        }

        count = StringPull(mask, scratch.Corners.AsSpan(0, cells), route);
        return count == 0 ? PathOutcome.NoPath : PathOutcome.Routed;
    }

    /// <summary>
    /// A* from the start cell to the goal cell. Writes the cell path, start
    /// point first and goal point last, into the scratch corners and returns
    /// how many points it wrote, or 0 when there is no way through.
    /// </summary>
    private static int Search(
        LandMask mask,
        PathfindingScratch scratch,
        float startX,
        float startY,
        float goalX,
        float goalY)
    {
        var size = mask.Size;
        var startCell = (((int)MathF.Floor(startY)) * size) + (int)MathF.Floor(startX);
        var goalCell = (((int)MathF.Floor(goalY)) * size) + (int)MathF.Floor(goalX);
        unchecked
        {
            scratch.Search++;
        }

        var search = scratch.Search;
        scratch.HeapCount = 0;
        scratch.Cost[startCell] = 0f;
        scratch.Stamp[startCell] = search;
        scratch.CameFrom[startCell] = -1;
        scratch.Closed[startCell] = false;
        HeapPush(scratch, startCell, Heuristic(startCell, goalCell, size));

        var found = ExpandUntilGoalOrExhausted(mask, scratch, search, goalCell, size);
        if (!found)
        {
            return 0;
        }

        return Reconstruct(scratch, startCell, goalCell, size, startX, startY, goalX, goalY);
    }

    /// <summary>
    /// Pops the open set until the goal is reached, the cell budget runs out,
    /// or the set empties. Split out of <see cref="Search"/> to stay under the
    /// one-method-per-concern line budget this file otherwise blows through.
    /// </summary>
    private static bool ExpandUntilGoalOrExhausted(
        LandMask mask, PathfindingScratch scratch, uint search, int goalCell, int size)
    {
        var expansions = 0;
        while (scratch.HeapCount > 0)
        {
            var cell = HeapPop(scratch);
            if (cell == goalCell)
            {
                return true;
            }

            if (scratch.Stamp[cell] == search && scratch.Closed[cell])
            {
                continue;
            }

            scratch.Closed[cell] = true;
            if (++expansions > MaximumExpansions)
            {
                return false;
            }

            RelaxNeighbours(mask, scratch, search, cell, goalCell, size);
        }

        return false;
    }

    /// <summary>
    /// Offers every open water neighbour of <paramref name="cell"/> a shorter
    /// path through it, in the eight compass directions, refusing to cut the
    /// corner between two rocks on a diagonal step.
    /// </summary>
    private static void RelaxNeighbours(
        LandMask mask, PathfindingScratch scratch, uint search, int cell, int goalCell, int size)
    {
        var cellX = cell % size;
        var cellY = cell / size;
        for (var direction = 0; direction < 8; direction++)
        {
            var nextX = cellX + NeighbourX[direction];
            var nextY = cellY + NeighbourY[direction];
            if (mask.IsLandCell(nextX, nextY))
            {
                continue;
            }

            // A hull does not cut the corner between two rocks, so a
            // diagonal step needs both of its sides open.
            if (NeighbourX[direction] != 0 && NeighbourY[direction] != 0 &&
                (mask.IsLandCell(nextX, cellY) || mask.IsLandCell(cellX, nextY)))
            {
                continue;
            }

            var next = (nextY * size) + nextX;
            if (scratch.Stamp[next] == search && scratch.Closed[next])
            {
                continue;
            }

            var cost = scratch.Cost[cell] + NeighbourCost[direction];
            if (scratch.Stamp[next] == search && cost >= scratch.Cost[next])
            {
                continue;
            }

            scratch.Stamp[next] = search;
            scratch.Closed[next] = false;
            scratch.Cost[next] = cost;
            scratch.CameFrom[next] = cell;
            HeapPush(scratch, next, cost + Heuristic(next, goalCell, size));
        }
    }

    /// <summary>
    /// Walks the parents back from the goal and writes the path forward, with
    /// the ship's true position first and the true destination last so the
    /// first and last leg are not bent to a cell centre.
    /// </summary>
    private static int Reconstruct(
        PathfindingScratch scratch,
        int startCell,
        int goalCell,
        int size,
        float startX,
        float startY,
        float goalX,
        float goalY)
    {
        var length = 0;
        for (var cell = goalCell; cell != -1; cell = scratch.CameFrom[cell])
        {
            length++;
            if (length > PathfindingScratch.MaximumCorners - 2)
            {
                return 0;
            }

            if (cell == startCell)
            {
                break;
            }
        }

        // One corner per path cell: the start and goal cells get the ship's exact
        // position rather than their cell centre, so `length` cells need exactly
        // `length` corners, not one more. Walking the parent chain down to, but not
        // including, the start cell is what keeps the start from being written twice.
        var count = length;
        scratch.Corners[0] = new RouteWaypoint(startX, startY);
        var write = count - 1;
        scratch.Corners[write] = new RouteWaypoint(goalX, goalY);
        write--;
        for (var cell = scratch.CameFrom[goalCell]; cell != startCell && write > 0; cell = scratch.CameFrom[cell])
        {
            scratch.Corners[write--] = new RouteWaypoint((cell % size) + 0.5f, (cell / size) + 0.5f);
        }

        return count;
    }

    /// <summary>
    /// Turns a cell path into the fewest straight legs that stay on water:
    /// from the current anchor, reach as far down the path as line of sight
    /// allows, keep that point, and start again from it.
    /// </summary>
    private static int StringPull(
        LandMask mask,
        ReadOnlySpan<RouteWaypoint> path,
        Span<RouteWaypoint> route)
    {
        var count = 0;
        var anchor = 0;
        while (anchor < path.Length - 1)
        {
            var furthest = anchor + 1;
            for (var candidate = path.Length - 1; candidate > anchor + 1; candidate--)
            {
                if (mask.SegmentIsClear(
                        path[anchor].X, path[anchor].Y, path[candidate].X, path[candidate].Y))
                {
                    furthest = candidate;
                    break;
                }
            }

            if (count == route.Length)
            {
                // More corners than SEA_5 §4.1.5 allows in one course.
                return 0;
            }

            route[count++] = path[furthest];
            anchor = furthest;
        }

        return count;
    }

    private static float Heuristic(int cell, int goalCell, int size)
    {
        // Octile distance: exact for an 8-direction grid, so A* never expands a
        // cell it did not have to.
        var deltaX = MathF.Abs((cell % size) - (goalCell % size));
        var deltaY = MathF.Abs((cell / size) - (goalCell / size));
        var smaller = MathF.Min(deltaX, deltaY);
        return (deltaX + deltaY) - ((2f - DiagonalCost) * smaller);
    }

    private static void HeapPush(PathfindingScratch scratch, int cell, float score)
    {
        var index = scratch.HeapCount++;
        scratch.HeapCell[index] = cell;
        scratch.HeapScore[index] = score;
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (scratch.HeapScore[parent] <= scratch.HeapScore[index])
            {
                break;
            }

            Swap(scratch, parent, index);
            index = parent;
        }
    }

    private static int HeapPop(PathfindingScratch scratch)
    {
        var top = scratch.HeapCell[0];
        var last = --scratch.HeapCount;
        scratch.HeapCell[0] = scratch.HeapCell[last];
        scratch.HeapScore[0] = scratch.HeapScore[last];
        var index = 0;
        while (true)
        {
            var left = (index * 2) + 1;
            if (left >= last)
            {
                break;
            }

            var smallest = left;
            var right = left + 1;
            if (right < last && scratch.HeapScore[right] < scratch.HeapScore[left])
            {
                smallest = right;
            }

            if (scratch.HeapScore[index] <= scratch.HeapScore[smallest])
            {
                break;
            }

            Swap(scratch, index, smallest);
            index = smallest;
        }

        return top;
    }

    private static void Swap(PathfindingScratch scratch, int left, int right)
    {
        (scratch.HeapCell[left], scratch.HeapCell[right]) =
            (scratch.HeapCell[right], scratch.HeapCell[left]);
        (scratch.HeapScore[left], scratch.HeapScore[right]) =
            (scratch.HeapScore[right], scratch.HeapScore[left]);
    }
}
