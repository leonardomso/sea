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

/// <summary>
/// What every hull can do with her way, as opposed to how fast she can go, which is her own.
/// </summary>
/// <remarks>
/// These are set against the chart rather than against a ship: a captain clicks a square and
/// expects the answer inside it, so a hull stops and comes about within roughly one square.
/// </remarks>
public static class HandlingRules
{
    // Squares per second per second. These read 10 and 30 while a square was ten units.
    // Deleting the conversion moved every position and speed in the simulation to squares,
    // and these had to move with them: left alone they would give a hull a ten square
    // stopping distance on a chart four hundred across, and the remark above would be a lie.
    public const float Acceleration = 1f;
    public const float Deceleration = 3f;

    /// <summary>How much sea a hull needs to come to rest from <paramref name="speed"/>.</summary>
    public static float StoppingDistance(float speed) => speed * speed / (2f * Deceleration);

    /// <summary>The radius of the circle a hull turns through at <paramref name="speed"/>.</summary>
    public static float TurningRadius(float speed, float turnRateDegrees) =>
        speed / (turnRateDegrees * (MathF.PI / 180f));
}

public static class SailingRules
{
    /// <summary>
    /// How near the mark a ship has to be before she has arrived at it: roughly a
    /// seventh of a chart square, which is the only unit there is.
    /// </summary>
    /// <remarks>
    /// Without this a ship could only finish a course by pointing at the mark, and a hull
    /// whose turning circle is wider than her distance to it can never point at it. Clicking
    /// a fraction of a square off the bow used to put her into a circle she orbited forever.
    /// Being close enough is arriving.
    /// </remarks>
    public const float ArrivalRadius = 0.15f;

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

        if (!stopping && LastStrideCoversTheMark(
                remainingSquared,
                travel,
                speed,
                parameters.Deceleration * deltaSeconds,
                thrustAlignment))
        {
            return new AuthoritativeSailingStep(
                destinationX, destinationY, heading, 0f, false, true);
        }

        if (!stopping && remainingSquared <= Square(ArrivalRadius))
        {
            return new AuthoritativeSailingStep(
                state.PositionX, state.PositionY, heading, 0f, false, true);
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

    /// <summary>
    /// Whether this tick's way carries her onto the mark, in which case she is put on it
    /// exactly. A ship inside <see cref="ArrivalRadius"/> but further off than one stride
    /// has still arrived, but she comes to rest where she swims: pulling her onto the mark
    /// from there would read as a jump.
    /// </summary>
    private static bool LastStrideCoversTheMark(
        float remainingSquared,
        float travel,
        float speed,
        float decelerationStep,
        float thrustAlignment) =>
        (remainingSquared <= Square(MathF.Max(0.05f, travel)) && speed <= decelerationStep) ||
        (travel * travel >= remainingSquared && thrustAlignment >= 0.95f);

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
