namespace Sea.Server;

public static class WorldRules
{
    /// <summary>
    /// Every map is this many squares on a side (SEA_5 §3.1). One square is one
    /// unit; there is no second unit and no conversion. (0,0) is the top-left
    /// corner, x grows east, y grows south.
    /// </summary>
    public const float MapSizeSquares = 400f;

    public const float MapMin = 0f;
    public const float MapMax = MapSizeSquares;

    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 10;

    /// <summary>How much time one tick of the simulation covers.</summary>
    public const float SecondsPerTick = 1f / TickRateHz;

    // Ships never collide with each other (SEA_5 §4.1.6); this only keeps a hull from
    // clipping the drawn edge of land, so a reef with radius 10 still blocks a touch at 10.
    public const float LandHazardPadding = 0.5f;

    public const uint InitialCannonDamage = 25;
    public const uint InitialCannonCooldownTicks = 20;
    public const uint EnemyInitialHealth = 100;
    public const uint EnemyCannonDamage = 5;
    public const uint EnemyCannonCooldownTicks = 40;
    public const uint EnemyGoldReward = 100;

    /// <summary>
    /// The circle of protected water around a harbour, in squares (SEA_5 §10.3).
    /// Players spawn and respawn inside it and NPCs never pick a target sailing in
    /// it, so a fresh spawn is not sunk before it moves. §10.3 also forbids firing
    /// across this line either way; that half arrives with <c>PortRules</c> in Phase
    /// 9 and will read the same constant, so moving this radius moves both.
    /// </summary>
    public const float HarborSafeRadiusSquares = 30f;

    public readonly struct SailingStep
    {
        public SailingStep(float x, float y, bool arrived)
        {
            X = x;
            Y = y;
            Arrived = arrived;
        }

        public float X { get; }
        public float Y { get; }
        public bool Arrived { get; }
    }

    // The documented boundary guard: GeometryRules assumes finite inputs and skips its
    // own checks on the hot path, trusting that a position was vetted here first.
    public static bool IsInsideMap(float x, float y) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        x >= MapMin &&
        x <= MapMax &&
        y >= MapMin &&
        y <= MapMax;

    public static bool IsValidMove(float x, float y) => IsInsideMap(x, y);

    public static (float X, float Y) ClampToMap(float x, float y) =>
        (Math.Clamp(x, MapMin, MapMax), Math.Clamp(y, MapMin, MapMax));

    public static SailingStep AdvanceTowards(
        float currentX,
        float currentY,
        float destinationX,
        float destinationY,
        float maximumDistance)
    {
        if (!float.IsFinite(maximumDistance) || maximumDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        }

        var deltaX = destinationX - currentX;
        var deltaY = destinationY - currentY;
        var distance = GeometryRules.Distance(currentX, currentY, destinationX, destinationY);
        if (distance <= maximumDistance)
        {
            return new SailingStep(destinationX, destinationY, true);
        }

        var scale = maximumDistance / distance;
        return new SailingStep(currentX + deltaX * scale, currentY + deltaY * scale, false);
    }

    public static bool IsBlocked(string kind, float entityX, float entityY, float radius, float x, float y)
    {
        if (!string.Equals(kind, "island", StringComparison.Ordinal) &&
            !string.Equals(kind, "reef", StringComparison.Ordinal))
        {
            return false;
        }

        var collisionRadius = radius + LandHazardPadding;
        return GeometryRules.DistanceSquared(entityX, entityY, x, y) < collisionRadius * collisionRadius;
    }

    public static bool IsBlocked(
        WorldObjectCode kind,
        float entityX,
        float entityY,
        float radius,
        float x,
        float y)
    {
        if (!HotPathCodes.BlocksMovement(kind))
        {
            return false;
        }

        var collisionRadius = radius + LandHazardPadding;
        return GeometryRules.DistanceSquared(entityX, entityY, x, y) < collisionRadius * collisionRadius;
    }

    public static bool IsInRange(float fromX, float fromY, float toX, float toY, float range) =>
        GeometryRules.DistanceSquared(fromX, fromY, toX, toY) <= range * range;

    public static uint ApplyDamage(uint health, uint damage) => damage >= health ? 0 : health - damage;
}
