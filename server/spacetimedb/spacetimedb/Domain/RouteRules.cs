namespace Sea.Server;

/// <summary>One corner of a course, in squares on the chart.</summary>
/// <remarks>
/// A waypoint is a point, not a cell. A* works on whole cells, but the mark a captain
/// clicks is wherever he clicked and the straightened course keeps that precision, so
/// this is a pair of floats rather than a pair of coordinates.
/// </remarks>
public readonly record struct RouteWaypoint(float X, float Y);

/// <summary>Where a hull stands after one tick of following her course.</summary>
/// <remarks>
/// <para>
/// The whole of a ship's movement state after a tick, and nothing else: there is no speed
/// here because speed is not a thing a hull carries between ticks (SEA_5 §4.2). The caller
/// works out how far she goes this tick from her stats and the weather (§5) and hands that
/// over as a distance; this only says where the distance puts her.
/// </para>
/// <para>
/// <see cref="Arrived"/> is <c>WaypointIndex &gt;= route.Length</c> and is carried anyway.
/// It is what the tick switches on to take a hull out of the moving set, and reconstructing
/// it at each call site is how off-by-one bugs get in.
/// </para>
/// </remarks>
public readonly record struct RouteStep(
    float PositionX,
    float PositionY,
    float HeadingDegrees,
    int WaypointIndex,
    bool Arrived);

/// <summary>
/// Following a course. A ship holds an ordered list of waypoints and walks a fixed
/// distance along it each tick, corner to corner, in straight lines.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the whole of the old <see cref="SailingRules"/>: there is no acceleration,
/// no braking curve and no turning circle, because SEA_5 §4.1.3 makes position exact linear
/// interpolation and §4.2 says the game has no inertia at all. Heading is an output, never
/// an input, and never slows her down (§4.1.7): a hull given a mark astern of her makes way
/// astern on the same tick.
/// </para>
/// <para>
/// A tick is a distance, not a time. Effective speed (§5) is somebody else's problem — wind,
/// storms, hull damage and buffs all land in the one <c>travel</c> figure — which is what
/// keeps this file the same three lines of arithmetic on the server, in the NPC steering and
/// in the client's prediction, and therefore what keeps the three agreeing.
/// </para>
/// <para>
/// Every float here is a square on a 400 x 400 chart with (0,0) at the top left. There is no
/// world unit and no conversion.
/// </para>
/// </remarks>
public static class RouteRules
{
    /// <summary>
    /// SEA_5 §4.1.5. A course longer than this is refused when it is built; nothing is
    /// checked here, because <see cref="Advance"/> runs for every hull on every tick and a
    /// route that got as far as the tick was vetted when it was laid.
    /// </summary>
    public const int MaximumWaypoints = 32;

    /// <summary>
    /// How close to her last mark counts as standing on it, in squares.
    /// </summary>
    /// <remarks>
    /// A tick moves a hull a whole stride, so she almost never lands on the mark exactly:
    /// without a radius she would overshoot it, be handed a leg pointing back the way she
    /// came, and shuttle across the mark forever. Being close enough is arriving. The
    /// corners in between get no such grace -- see <c>ReachesCorner</c>.
    /// </remarks>
    public const float ArrivalRadius = 0.15f;

    /// <summary>
    /// Walks one tick's <paramref name="travel"/> along the course, rounding as many corners
    /// as that distance reaches. A course therefore takes exactly its own length over her
    /// speed, however it bends: a corner costs nothing to turn.
    /// </summary>
    /// <param name="route">The corners still to come, the first of them being leg
    /// <paramref name="waypointIndex"/>. An empty course means she is not underway.</param>
    /// <param name="waypointIndex">Which corner she is steering for. Out of range past the
    /// end means the course is finished; below zero is read as the start of it.</param>
    /// <param name="headingDegrees">Her bearing coming in, kept when there is no leg left to
    /// take one from — a ship that has arrived goes on pointing the way she came in.</param>
    /// <param name="travel">How much sea this tick covers, in squares. Zero is allowed and
    /// leaves her where she is; negative or non-finite is a caller bug.</param>
    public static RouteStep Advance(
        ReadOnlySpan<RouteWaypoint> route,
        int waypointIndex,
        float positionX,
        float positionY,
        float headingDegrees,
        float travel)
    {
        if (!float.IsFinite(travel) || travel < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(travel));
        }

        var heading = GeometryRules.NormalizeAngle(headingDegrees);
        var index = Math.Max(0, waypointIndex);
        var x = positionX;
        var y = positionY;
        var left = travel;

        while (index < route.Length)
        {
            var corner = route[index];
            var deltaX = corner.X - x;
            var deltaY = corner.Y - y;
            var remaining = GeometryRules.Distance(x, y, corner.X, corner.Y);

            // She steers straight at the corner. HeadingTo keeps the bearing she came in on
            // when she is already standing on it, which is what §13 test 14 asks for.
            heading = GeometryRules.HeadingTo(x, y, corner.X, corner.Y, heading);

            if (!ReachesCorner(remaining, left, index, route.Length))
            {
                var fraction = left / remaining;
                return new RouteStep(
                    x + (deltaX * fraction),
                    y + (deltaY * fraction),
                    heading,
                    index,
                    false);
            }

            // She passes the corner this tick, so she is put on it exactly and the rest of
            // the tick is spent on the next leg. Rounding a corner costs nothing: SEA_5
            // §4.1.7, reversing is instant.
            x = corner.X;
            y = corner.Y;
            left = MathF.Max(0f, left - remaining);
            index++;
        }

        return new RouteStep(x, y, heading, index, true);
    }

    /// <summary>
    /// How much sea is left along the course from where she stands, in squares. Measured
    /// along the legs, not across them, so a dogleg round an island reads as the distance
    /// she actually has to sail; divide by her speed for the time it will take.
    /// </summary>
    public static float RemainingDistance(
        ReadOnlySpan<RouteWaypoint> route,
        int waypointIndex,
        float positionX,
        float positionY)
    {
        var total = 0f;
        var x = positionX;
        var y = positionY;
        for (var index = Math.Max(0, waypointIndex); index < route.Length; index++)
        {
            var corner = route[index];
            total += GeometryRules.Distance(x, y, corner.X, corner.Y);
            x = corner.X;
            y = corner.Y;
        }

        return total;
    }

    /// <summary>
    /// Whether this tick's remaining way carries her onto the corner she is steering for.
    /// </summary>
    /// <remarks>
    /// The last mark has <see cref="ArrivalRadius"/> around it and the corners
    /// before it have none. Standing a tenth of a square off her destination is standing on
    /// it — SEA_5 §4.1.3 stops a ship exactly on the last waypoint and §13 test 5 wants two
    /// ships sent to one point to hold that same point — and without the radius a becalmed
    /// hull, or one whose last stride is shorter than the float noise in her position, would
    /// report herself still under way forever over sea she cannot cross. The corners in
    /// between get no such grace: rounding one off early would cut the straightened A* path
    /// back across the land it was laid to avoid.
    /// </remarks>
    private static bool ReachesCorner(float remaining, float left, int index, int length) =>
        remaining <= left ||
        (index == length - 1 && remaining <= ArrivalRadius);
}
