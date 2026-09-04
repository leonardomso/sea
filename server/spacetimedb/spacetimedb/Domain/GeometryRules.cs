namespace Sea.Server;

/// <summary>
/// The plane the whole game is played on. One unit is one square; there is no
/// other unit. Heading is a compass bearing: 0 is north (up the screen, -Y),
/// 90 is east, 180 south, 270 west.
/// </summary>
/// <remarks>
/// <para>
/// Every angle and distance in the simulation comes through here. Four files
/// used to keep a private NormalizeAngle and three a private Distance, which is
/// how a Y-down map ended up with 0 degrees pointing south.
/// </para>
/// <para>
/// Every entry point assumes finite inputs. Nothing here checks, because this
/// runs for every ship on every tick; a position is vetted at the boundary by
/// <see cref="WorldRules.IsInsideMap"/> and by the command policy before it ever
/// reaches the simulation. The cost of skipping the check is that garbage stays
/// silent: a NaN radius makes <see cref="SegmentIntersectsCircle"/> answer "no
/// collision" and a ship sails through a reef with nothing to say why. Keep the
/// guard at the edge rather than adding one here.
/// </para>
/// <para>
/// <see cref="MathF.Atan2"/> in <see cref="HeadingTo"/> is the one libm call in
/// the geometry layer. It is not correctly rounded and is not guaranteed to give
/// bit-identical answers in the wasm module, the native test host and Unity, so a
/// replay hash that matches on one platform and not another points here first,
/// before anyone goes looking for a logic change.
/// </para>
/// </remarks>
public static class GeometryRules
{
    private const float DegreesPerRadian = 180f / MathF.PI;

    /// <summary>
    /// Does double duty: two points this close together are the same point, and a
    /// segment this short has no length. A thousandth of a square, and nothing on
    /// this chart is that small.
    /// </summary>
    private const float NoMovementSquared = 0.000001f;

    /// <summary>
    /// The distance between two points, in squares. Prefer
    /// <see cref="DistanceSquared"/> when the answer is only compared against a
    /// threshold: squaring the threshold costs nothing and the square root is
    /// pure waste.
    /// </summary>
    public static float Distance(float fromX, float fromY, float toX, float toY) =>
        MathF.Sqrt(DistanceSquared(fromX, fromY, toX, toY));

    /// <summary>The squared distance between two points. See <see cref="Distance"/>.</summary>
    public static float DistanceSquared(float fromX, float fromY, float toX, float toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    /// <summary>
    /// The bearing from one point to another. When the two points are the same
    /// there is no bearing to give, so <paramref name="fallbackHeadingDegrees"/>
    /// is returned instead: a ship that has arrived keeps pointing the way she
    /// came in. The fallback is required on purpose. A default would quietly hand
    /// back north, and neither the caller nor this method could tell that apart
    /// from a real bearing.
    /// </summary>
    public static float HeadingTo(
        float fromX,
        float fromY,
        float toX,
        float toY,
        float fallbackHeadingDegrees)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        if ((deltaX * deltaX) + (deltaY * deltaY) <= NoMovementSquared)
        {
            return NormalizeAngle(fallbackHeadingDegrees);
        }

        return NormalizeAngle(MathF.Atan2(deltaX, -deltaY) * DegreesPerRadian);
    }

    /// <summary>
    /// The unit vector a hull on <paramref name="headingDegrees"/> travels along.
    /// Y is negated because the chart grows downwards, so north is -Y.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This reads <see cref="TrigonometryRules"/>, which samples a quarter of a
    /// degree at a time, so the vector points at the nearest quarter degree and
    /// can sit up to 0.125 degrees off the bearing <see cref="HeadingTo"/> would
    /// give for the same vector. At 400 squares that is well under a square at
    /// any range, but the mixed precision surprises people.
    /// </para>
    /// <para>
    /// Y is subtracted from zero rather than negated. Due west reads a table entry
    /// of exactly +0, and negating that gives -0, which is a different number to
    /// anything that hashes a vector. Subtracting costs the same instruction and
    /// keeps this file's promise that no zero it hands out is negative.
    /// </para>
    /// </remarks>
    public static (float X, float Y) Direction(float headingDegrees) =>
        (TrigonometryRules.SinDegrees(headingDegrees),
            0f - TrigonometryRules.CosDegrees(headingDegrees));

    /// <summary>Maps any angle onto [0, 360): 0 and 360 are both 0, and there is no negative zero.</summary>
    public static float NormalizeAngle(float angleDegrees)
    {
        var normalized = angleDegrees % 360f;
        if (normalized < 0f)
        {
            normalized += 360f;
        }

        // Two bearings that mean the same thing have to come back as the same bits: the replay
        // hash reads heading through SingleToUInt32Bits. -0f stays -0f through the remainder, and
        // a tiny negative angle rounds up to exactly 360f when 360f is added back.
        return normalized >= 360f ? 0f : normalized + 0f;
    }

    /// <summary>
    /// Maps any angle onto [-180, 180], so a bearing offset can be compared
    /// against an arc. Exactly -180 comes back as +180.
    /// </summary>
    /// <remarks>
    /// <c>CombatRules.NormalizeSignedAngle</c> rounds instead and answers -180 for
    /// that one input. Its caller takes an absolute value, so today the two agree
    /// on every answer that is used; they are still not interchangeable, and
    /// whoever migrates that call site has to check the sign matters nowhere else.
    /// </remarks>
    public static float NormalizeSignedAngle(float angleDegrees)
    {
        var normalized = NormalizeAngle(angleDegrees);
        return normalized > 180f ? normalized - 360f : normalized;
    }

    /// <summary>
    /// Whether a course from start to end passes through a circle. Touching is not
    /// passing through: a course exactly tangent to the circle is a miss. A
    /// zero-length segment is treated as the point it starts at. The radius is
    /// taken as given and squared, so a negative one behaves as its own magnitude
    /// rather than as an empty circle.
    /// </summary>
    public static bool SegmentIntersectsCircle(
        float startX,
        float startY,
        float endX,
        float endY,
        float centerX,
        float centerY,
        float radius)
    {
        var segmentX = endX - startX;
        var segmentY = endY - startY;
        var lengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
        var projection = lengthSquared <= NoMovementSquared
            ? 0f
            : Math.Clamp(
                (((centerX - startX) * segmentX) + ((centerY - startY) * segmentY)) / lengthSquared,
                0f,
                1f);
        var closestX = startX + (segmentX * projection);
        var closestY = startY + (segmentY * projection);
        return DistanceSquared(closestX, closestY, centerX, centerY) < radius * radius;
    }
}
