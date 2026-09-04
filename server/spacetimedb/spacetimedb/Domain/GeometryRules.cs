namespace Sea.Server;

/// <summary>
/// The plane the whole game is played on. One unit is one square; there is no
/// other unit. Heading is a compass bearing: 0 is north (up the screen, -Y),
/// 90 is east, 180 south, 270 west.
/// </summary>
/// <remarks>
/// Every angle and distance in the simulation comes through here. Four files
/// used to keep a private NormalizeAngle and three a private Distance, which is
/// how a Y-down map ended up with 0 degrees pointing south.
/// </remarks>
public static class GeometryRules
{
    private const float DegreesPerRadian = 180f / MathF.PI;
    private const float NoMovementSquared = 0.000001f;

    public static float Distance(float fromX, float fromY, float toX, float toY) =>
        MathF.Sqrt(DistanceSquared(fromX, fromY, toX, toY));

    public static float DistanceSquared(float fromX, float fromY, float toX, float toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    /// <summary>
    /// The bearing from one point to another. When the two points are the same
    /// there is no bearing to give, so the caller's current one is kept: a ship
    /// that has arrived keeps pointing the way she came in.
    /// </summary>
    public static float HeadingTo(
        float fromX,
        float fromY,
        float toX,
        float toY,
        float currentHeadingDegrees = 0f)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        if ((deltaX * deltaX) + (deltaY * deltaY) <= NoMovementSquared)
        {
            return NormalizeAngle(currentHeadingDegrees);
        }

        return NormalizeAngle(MathF.Atan2(deltaX, -deltaY) * DegreesPerRadian);
    }

    /// <summary>The unit vector a hull on <paramref name="headingDegrees"/> travels along.</summary>
    public static (float X, float Y) Direction(float headingDegrees) =>
        (TrigonometryRules.SinDegrees(headingDegrees), -TrigonometryRules.CosDegrees(headingDegrees));

    public static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    public static float NormalizeSignedAngle(float angle)
    {
        angle = NormalizeAngle(angle);
        return angle > 180f ? angle - 360f : angle;
    }

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
