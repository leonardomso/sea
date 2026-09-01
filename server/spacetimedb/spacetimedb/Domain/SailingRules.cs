namespace Sea.Server;

public readonly struct SailingState
{
    public SailingState(float positionX, float positionY, float headingDegrees, float speed)
    {
        PositionX = positionX;
        PositionY = positionY;
        HeadingDegrees = headingDegrees;
        Speed = speed;
    }

    public float PositionX { get; }
    public float PositionY { get; }
    public float HeadingDegrees { get; }
    public float Speed { get; }
}

public readonly struct SailingParameters
{
    public SailingParameters(
        float maximumSpeed,
        float acceleration,
        float deceleration,
        float turnRateDegrees)
    {
        MaximumSpeed = maximumSpeed;
        Acceleration = acceleration;
        Deceleration = deceleration;
        TurnRateDegrees = turnRateDegrees;
    }

    public float MaximumSpeed { get; }
    public float Acceleration { get; }
    public float Deceleration { get; }
    public float TurnRateDegrees { get; }
}

public readonly struct AuthoritativeSailingStep
{
    public AuthoritativeSailingStep(
        float positionX,
        float positionY,
        float headingDegrees,
        float speed,
        bool isMoving,
        bool arrived)
    {
        PositionX = positionX;
        PositionY = positionY;
        HeadingDegrees = headingDegrees;
        Speed = speed;
        IsMoving = isMoving;
        Arrived = arrived;
    }

    public float PositionX { get; }
    public float PositionY { get; }
    public float HeadingDegrees { get; }
    public float Speed { get; }
    public bool IsMoving { get; }
    public bool Arrived { get; }
}

public static class SailingRules
{
    public static AuthoritativeSailingStep Step(
        SailingState state,
        float destinationX,
        float destinationY,
        bool stopping,
        SailingParameters parameters,
        float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }

        var deltaX = destinationX - state.PositionX;
        var deltaY = destinationY - state.PositionY;
        var remaining = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var heading = NormalizeAngle(state.HeadingDegrees);
        var thrustAlignment = 1f;
        if (!stopping && remaining > 0.001f)
        {
            var desiredHeading = NormalizeAngle(
                MathF.Atan2(deltaX, deltaY) * (180f / MathF.PI));
            heading = MoveTowardsAngle(
                heading,
                desiredHeading,
                parameters.TurnRateDegrees * deltaSeconds);
            var headingErrorRadians = NormalizeSignedAngle(desiredHeading - heading) *
                (MathF.PI / 180f);
            thrustAlignment = MathF.Max(0f, MathF.Cos(headingErrorRadians));
        }

        var brakingSpeed = stopping
            ? 0f
            : MathF.Sqrt(MathF.Max(0f, 2f * parameters.Deceleration * remaining));
        var alignedMaximumSpeed = parameters.MaximumSpeed * thrustAlignment;
        var targetSpeed = MathF.Min(alignedMaximumSpeed, brakingSpeed);
        var speedChange = targetSpeed > state.Speed
            ? parameters.Acceleration * deltaSeconds
            : parameters.Deceleration * deltaSeconds;
        var speed = MoveTowards(state.Speed, targetSpeed, speedChange);
        var averageSpeed = (state.Speed + speed) * 0.5f;
        var travel = averageSpeed * deltaSeconds;

        if (!stopping &&
            ((remaining <= MathF.Max(0.05f, travel) &&
              speed <= parameters.Deceleration * deltaSeconds) ||
             (travel >= remaining && thrustAlignment >= 0.95f)))
        {
            return new AuthoritativeSailingStep(
                destinationX,
                destinationY,
                heading,
                0f,
                false,
                true);
        }

        var radians = heading * (MathF.PI / 180f);
        var positionX = state.PositionX + MathF.Sin(radians) * travel;
        var positionY = state.PositionY + MathF.Cos(radians) * travel;
        var moving = speed > 0.001f || (!stopping && remaining > 0.05f);
        return new AuthoritativeSailingStep(
            positionX,
            positionY,
            heading,
            speed,
            moving,
            false);
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
        var lengthSquared = segmentX * segmentX + segmentY * segmentY;
        var projection = lengthSquared <= 0.000001f
            ? 0f
            : Math.Clamp(
                ((centerX - startX) * segmentX + (centerY - startY) * segmentY) /
                lengthSquared,
                0f,
                1f);
        var closestX = startX + segmentX * projection;
        var closestY = startY + segmentY * projection;
        var deltaX = closestX - centerX;
        var deltaY = closestY - centerY;
        return deltaX * deltaX + deltaY * deltaY < radius * radius;
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (MathF.Abs(target - current) <= maximumDelta)
        {
            return target;
        }

        return current + MathF.Sign(target - current) * maximumDelta;
    }

    private static float MoveTowardsAngle(float current, float target, float maximumDelta)
    {
        var delta = NormalizeSignedAngle(target - current);
        if (MathF.Abs(delta) <= maximumDelta)
        {
            return target;
        }

        return NormalizeAngle(current + MathF.Sign(delta) * maximumDelta);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle = NormalizeAngle(angle);
        return angle > 180f ? angle - 360f : angle;
    }
}
