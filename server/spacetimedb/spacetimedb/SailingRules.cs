namespace Sea.Server;

public readonly struct ChartCell
{
    public ChartCell(int column, int row, float x, float y)
    {
        Column = column;
        Row = row;
        X = x;
        Y = y;
    }

    public int Column { get; }
    public int Row { get; }
    public float X { get; }
    public float Y { get; }
}

public static class ChartCoordinates
{
    public const int ColumnCount = 78;
    public const int RowCount = 61;
    public const int MaximumRow = RowCount - 1;
    public const float CellWidth = (WorldRules.MapMax - WorldRules.MapMin) / ColumnCount;
    public const float CellHeight = (WorldRules.MapMax - WorldRules.MapMin) / RowCount;

    public static string ColumnLabel(int column)
    {
        if (column < 0 || column >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        var value = column + 1;
        Span<char> characters = stackalloc char[2];
        var index = characters.Length;
        while (value > 0)
        {
            value--;
            characters[--index] = (char)('A' + value % 26);
            value /= 26;
        }

        return new string(characters[index..]);
    }

    public static bool TryColumnIndex(string? label, out int column)
    {
        column = -1;
        if (string.IsNullOrWhiteSpace(label) || label.Length > 2)
        {
            return false;
        }

        var value = 0;
        foreach (var character in label.ToUpperInvariant())
        {
            if (character < 'A' || character > 'Z')
            {
                return false;
            }

            value = checked(value * 26 + character - 'A' + 1);
        }

        column = value - 1;
        return column >= 0 && column < ColumnCount;
    }

    public static bool TryCellCenter(string? coordinate, out ChartCell cell)
    {
        cell = default;
        if (string.IsNullOrWhiteSpace(coordinate))
        {
            return false;
        }

        var parts = coordinate.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryColumnIndex(parts[0], out var column) ||
            !int.TryParse(parts[1], out var row) ||
            row < 0 ||
            row > MaximumRow)
        {
            return false;
        }

        cell = CellCenter(column, row);
        return true;
    }

    public static ChartCell CellCenter(int column, int row)
    {
        if (column < 0 || column >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (row < 0 || row > MaximumRow)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        return new ChartCell(
            column,
            row,
            WorldRules.MapMin + (column + 0.5f) * CellWidth,
            WorldRules.MapMin + (row + 0.5f) * CellHeight);
    }

    public static string LabelAt(float x, float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        var column = Math.Clamp(
            (int)MathF.Floor((x - WorldRules.MapMin) / CellWidth),
            0,
            ColumnCount - 1);
        var row = Math.Clamp(
            (int)MathF.Floor((y - WorldRules.MapMin) / CellHeight),
            0,
            MaximumRow);
        return $"{ColumnLabel(column)} {row}";
    }
}

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
        if (!stopping && remaining > 0.001f)
        {
            var desiredHeading = NormalizeAngle(
                MathF.Atan2(deltaX, deltaY) * (180f / MathF.PI));
            heading = MoveTowardsAngle(
                heading,
                desiredHeading,
                parameters.TurnRateDegrees * deltaSeconds);
        }

        var brakingSpeed = stopping
            ? 0f
            : MathF.Sqrt(MathF.Max(0f, 2f * parameters.Deceleration * remaining));
        var targetSpeed = MathF.Min(parameters.MaximumSpeed, brakingSpeed);
        var speedChange = targetSpeed > state.Speed
            ? parameters.Acceleration * deltaSeconds
            : parameters.Deceleration * deltaSeconds;
        var speed = MoveTowards(state.Speed, targetSpeed, speedChange);
        var averageSpeed = (state.Speed + speed) * 0.5f;
        var travel = averageSpeed * deltaSeconds;

        if (!stopping && remaining <= MathF.Max(0.05f, travel) && speed <= parameters.Deceleration * deltaSeconds)
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

public readonly struct SpawnBlocker
{
    public SpawnBlocker(float x, float y, float radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }
}

public readonly struct SpawnPoint
{
    public SpawnPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }
    public float Y { get; }
}

public static class SpawnRules
{
    public const float EdgeMargin = 5f;
    public const float Separation = 5f;
    public const int MaximumAttempts = 256;

    public static bool TryFindSafePosition(
        ulong seed,
        IReadOnlyCollection<SpawnBlocker> blockers,
        out SpawnPoint point)
    {
        var random = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        var minimum = WorldRules.MapMin + EdgeMargin;
        var span = WorldRules.MapMax - WorldRules.MapMin - EdgeMargin * 2f;
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var x = minimum + NextUnit(ref random) * span;
            var y = minimum + NextUnit(ref random) * span;
            if (blockers.All(blocker => !Overlaps(x, y, blocker)))
            {
                point = new SpawnPoint(x, y);
                return true;
            }
        }

        point = default;
        return false;
    }

    public static bool Overlaps(float x, float y, SpawnBlocker blocker)
    {
        var deltaX = x - blocker.X;
        var deltaY = y - blocker.Y;
        var radius = blocker.Radius + Separation;
        return deltaX * deltaX + deltaY * deltaY < radius * radius;
    }

    private static float NextUnit(ref ulong state)
    {
        state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
        return (float)((state >> 40) / 16_777_216d);
    }
}

public readonly struct WindSnapshot
{
    public WindSnapshot(float directionDegrees, float strength)
    {
        DirectionDegrees = directionDegrees;
        Strength = strength;
    }

    public float DirectionDegrees { get; }
    public float Strength { get; }
}

public static class EnvironmentRules
{
    public const ulong WindEpochTicks = 300;

    public static WindSnapshot WindForEpoch(ulong seed, ulong epoch)
    {
        var state = unchecked(seed ^ (epoch + 1) * 0x9E3779B97F4A7C15UL);
        state = Mix(state);
        var direction = (float)((state >> 32) / (double)uint.MaxValue * 360d);
        state = Mix(state);
        var strength = 0.2f + (float)((state >> 32) / (double)uint.MaxValue * 0.6d);
        return new WindSnapshot(direction, strength);
    }

    public static float WindSpeedMultiplier(
        float headingDegrees,
        float windDirectionDegrees,
        float windStrength)
    {
        var difference = (headingDegrees - windDirectionDegrees) * (MathF.PI / 180f);
        return 1f + MathF.Cos(difference) * Math.Clamp(windStrength, 0f, 1f) * 0.15f;
    }

    public static (float X, float Y) DirectionalVelocity(
        float directionDegrees,
        float strength)
    {
        var radians = directionDegrees * (MathF.PI / 180f);
        return (MathF.Sin(radians) * strength, MathF.Cos(radians) * strength);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
