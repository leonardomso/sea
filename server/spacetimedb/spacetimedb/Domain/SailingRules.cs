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
        return StepTowardHeading(
            state,
            destinationX,
            destinationY,
            DesiredHeading(state.PositionX, state.PositionY, destinationX, destinationY),
            stopping,
            parameters,
            deltaSeconds);
    }

    public static AuthoritativeSailingStep StepTowardHeading(
        SailingState state,
        float destinationX,
        float destinationY,
        float desiredHeadingDegrees,
        bool stopping,
        SailingParameters parameters,
        float deltaSeconds)
    {
        ValidateDeltaSeconds(deltaSeconds);

        var deltaX = destinationX - state.PositionX;
        var deltaY = destinationY - state.PositionY;
        var remainingSquared = deltaX * deltaX + deltaY * deltaY;
        var heading = ResolveHeading(
            state.HeadingDegrees,
            desiredHeadingDegrees,
            remainingSquared,
            stopping,
            parameters.TurnRateDegrees * deltaSeconds,
            out var thrustAlignment);

        var alignedMaximumSpeed = parameters.MaximumSpeed * thrustAlignment;
        var targetSpeed = stopping
            ? 0f
            : BrakingLimitedSpeed(
                alignedMaximumSpeed,
                parameters.Deceleration,
                remainingSquared);
        var speedChange = targetSpeed > state.Speed
            ? parameters.Acceleration * deltaSeconds
            : parameters.Deceleration * deltaSeconds;
        var speed = MoveTowards(state.Speed, targetSpeed, speedChange);
        var averageSpeed = (state.Speed + speed) * 0.5f;
        var travel = averageSpeed * deltaSeconds;
        var directionX = TrigonometryRules.SinDegrees(heading);
        var directionY = TrigonometryRules.CosDegrees(heading);

        if (!stopping &&
            ((remainingSquared <= Square(MathF.Max(0.05f, travel)) &&
              speed <= parameters.Deceleration * deltaSeconds) ||
             (travel * travel >= remainingSquared && thrustAlignment >= 0.95f)))
        {
            return new AuthoritativeSailingStep(
                destinationX,
                destinationY,
                heading,
                0f,
                false,
                true);
        }

        var positionX = state.PositionX + directionX * travel;
        var positionY = state.PositionY + directionY * travel;
        var moving = speed > 0.001f || (!stopping && remainingSquared > 0.0025f);
        return new AuthoritativeSailingStep(
            positionX,
            positionY,
            heading,
            speed,
            moving,
            false);
    }

    private static void ValidateDeltaSeconds(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }
    }

    public static float DesiredHeading(
        float positionX,
        float positionY,
        float destinationX,
        float destinationY)
    {
        var deltaX = destinationX - positionX;
        var deltaY = destinationY - positionY;
        return deltaX * deltaX + deltaY * deltaY <= 0.000001f
            ? 0f
            : NormalizeAngle(MathF.Atan2(deltaX, deltaY) * (180f / MathF.PI));
    }

    private static float ResolveHeading(
        float currentHeading,
        float desiredHeading,
        float remainingSquared,
        bool stopping,
        float maximumTurn,
        out float thrustAlignment)
    {
        var heading = NormalizeAngle(currentHeading);
        thrustAlignment = 1f;
        if (stopping || remainingSquared <= 0.000001f)
        {
            return heading;
        }

        desiredHeading = NormalizeAngle(desiredHeading);
        heading = MoveTowardsAngle(heading, desiredHeading, maximumTurn);
        var headingError = NormalizeSignedAngle(desiredHeading - heading);
        thrustAlignment = MathF.Max(0f, TrigonometryRules.CosDegrees(headingError));
        return heading;
    }

    private static float BrakingLimitedSpeed(
        float maximumSpeed,
        float deceleration,
        float remainingSquared)
    {
        if (deceleration <= 0f)
        {
            return maximumSpeed;
        }

        var brakingDistance = maximumSpeed * maximumSpeed / (2f * deceleration);
        if (remainingSquared >= brakingDistance * brakingDistance)
        {
            return maximumSpeed;
        }

        return MathF.Sqrt(
            MathF.Max(0f, 2f * deceleration * MathF.Sqrt(remainingSquared)));
    }

    private static float Square(float value) => value * value;

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
