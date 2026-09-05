using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>
    /// One set of A* buffers for the whole module. A dispatch is single-threaded and a
    /// search never outlives the call that started it, so one is enough, and it means
    /// answering a click allocates nothing but the course itself.
    /// </summary>
    /// <remarks>
    /// Module statics are undefined across reducer calls, which is exactly what this
    /// wants: the buffers carry no state between searches, only their size.
    /// </remarks>
    private static PathfindingScratch? pathfindingScratch;

    private static PathfindingScratch ScratchFor(LandMask mask) =>
        pathfindingScratch is { } scratch && scratch.Size == mask.Size
            ? scratch
            : pathfindingScratch = new PathfindingScratch(mask.Size);

    /// <summary>
    /// Answering a click: SEA_5 4.1.2. The point is pulled inside the chart, then off
    /// land if there is water close enough to it, then a course is plotted to it. The
    /// old course is replaced in one step, so a captain never sails a mixture of two
    /// orders.
    /// </summary>
    private static CommandRejectionCode SetCourse(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        float requestedX,
        float requestedY)
    {
        var windowStart = ship.MoveWindowStartTick;
        var used = ship.MovesInWindow;
        var allowed = MoveRateRules.Allow(ref windowStart, ref used, world.Tick);
        ship.MoveWindowStartTick = windowStart;
        ship.MovesInWindow = used;
        if (!allowed)
        {
            RecordTrustPenalty(ref ship, TrustScoreRules.DroppedMovePenalty);
            return CommandRejectionCode.RateLimited;
        }

        var mask = ContentCatalog.LandMaskFor(ship.MapId);
        var (clampedX, clampedY) = WorldRules.ClampToMap(requestedX, requestedY);
        if (!mask.TryNearestWater(
                clampedX,
                clampedY,
                PathfindingRules.NudgeSearchSquares,
                out var goalX,
                out var goalY))
        {
            return CommandRejectionCode.NoPath;
        }

        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var outcome = PathfindingRules.TryBuildRoute(
            mask,
            ScratchFor(mask),
            ship.PositionX,
            ship.PositionY,
            goalX,
            goalY,
            route,
            out var count);
        if (outcome == PathOutcome.NoPath)
        {
            return CommandRejectionCode.NoPath;
        }

        StoreRoute(ctx, world, ref ship, route[..count]);
        return CommandRejectionCode.None;
    }

    /// <summary>
    /// Marks a command the server threw away. The count goes on the row the caller is
    /// already holding rather than through the table, because the caller writes that row
    /// back afterwards and a second write here would be overwritten by it. Phase 12 moves
    /// the score itself to its own table and leaves this counter as the raw feed.
    /// </summary>
    private static void RecordTrustPenalty(ref Ship ship, int penalty) =>
        ship.DroppedCommandCount += (uint)penalty;

    private static void StoreRoute(
        ReducerContext ctx,
        TickWorld world,
        ref Ship ship,
        ReadOnlySpan<RouteWaypoint> route)
    {
        var pointsX = new List<float>(route.Length);
        var pointsY = new List<float>(route.Length);
        foreach (var waypoint in route)
        {
            pointsX.Add(waypoint.X);
            pointsY.Add(waypoint.Y);
        }

        ship.RouteVersion++;
        ship.RouteIndex = 0;
        ship.HasRoute = route.Length > 0;
        ship.IsMoving = ship.HasRoute;
        ship.DestinationX = route.Length > 0 ? route[^1].X : ship.PositionX;
        ship.DestinationY = route.Length > 0 ? route[^1].Y : ship.PositionY;

        var stored = new ShipRoute
        {
            EntityId = ship.EntityId,
            Version = ship.RouteVersion,
            PointsX = pointsX,
            PointsY = pointsY,
        };
        if (ctx.Db.ShipRoute.EntityId.Find(ship.EntityId) is null)
        {
            ctx.Db.ShipRoute.Insert(stored);
        }
        else
        {
            ctx.Db.ShipRoute.EntityId.Update(stored);
        }

        world.StoreRoute(ship.EntityId, route);
    }

    /// <summary>
    /// Stopping: SEA_5 4.1.4. The course is gone and the ship is at rest in the same
    /// tick, wherever she happens to be. Sinking, freezing and a berth change all come
    /// through here.
    /// </summary>
    private static void ClearRoute(ReducerContext ctx, TickWorld world, ref Ship ship)
    {
        ClearRoute(ctx, ref ship);
        world.StoreRoute(ship.EntityId, ReadOnlySpan<RouteWaypoint>.Empty);
    }

    /// <summary>
    /// Tears up a ship's course and brings her to rest. The tick's cached copy is left
    /// alone, so callers that hold a <see cref="TickWorld"/> should use the overload that
    /// takes one; this is for the paths -- sinking, respawning -- that do not.
    /// </summary>
    private static void ClearRoute(ReducerContext ctx, ref Ship ship)
    {
        ship.HasRoute = false;
        ship.IsMoving = false;
        ship.RouteIndex = 0;
        ship.Speed = 0f;
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.EffectiveSpeedSquaresPerSecond = 0f;
        if (ctx.Db.ShipRoute.EntityId.Find(ship.EntityId) is not null)
        {
            ctx.Db.ShipRoute.EntityId.Delete(ship.EntityId);
        }
    }
}
