namespace Sea.Server;

public static class WorldRules
{
    public const float MapMin = -100f;
    public const float MapMax = 100f;
    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 10;
    public const float CollisionPadding = 0.5f;
    public const uint InitialCannonDamage = 25;
    public const uint InitialCannonCooldownTicks = 20;
    public const uint EnemyInitialHealth = 100;
    public const uint EnemyCannonDamage = 5;
    public const uint EnemyCannonCooldownTicks = 40;
    public const uint EnemyGoldReward = 100;
    public const float CannonRange = 60f;
    public const uint InitialProgressionLevel = 1;
    public const uint InitialCannonUpgradeLevel = 0;
    public const uint CannonUpgradeBaseCost = 100;
    public const uint CannonUpgradeCostStep = 100;
    public const uint CannonDamagePerUpgrade = 5;
    public const float PlayerShipSpeed = 12f;
    public const float PlayerShipTurnRateDegrees = 360f;

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

    public static bool IsInsideMap(float x, float y) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        x >= MapMin &&
        x <= MapMax &&
        y >= MapMin &&
        y <= MapMax;

    public static bool IsValidMove(float x, float y) => IsInsideMap(x, y);

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
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (distance <= maximumDistance)
        {
            return new SailingStep(destinationX, destinationY, true);
        }

        var scale = maximumDistance / distance;
        return new SailingStep(currentX + deltaX * scale, currentY + deltaY * scale, false);
    }

    public static bool IsBlocked(string kind, float entityX, float entityY, float radius, float x, float y)
    {
        if (kind != "island" && kind != "reef")
        {
            return false;
        }

        var dx = x - entityX;
        var dy = y - entityY;
        var collisionRadius = radius + CollisionPadding;
        return dx * dx + dy * dy < collisionRadius * collisionRadius;
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

        var dx = x - entityX;
        var dy = y - entityY;
        var collisionRadius = radius + CollisionPadding;
        return dx * dx + dy * dy < collisionRadius * collisionRadius;
    }

    public static bool IsInRange(float sourceX, float sourceY, float targetX, float targetY, float range)
    {
        var dx = targetX - sourceX;
        var dy = targetY - sourceY;
        return dx * dx + dy * dy <= range * range;
    }

    public static uint ApplyDamage(uint health, uint damage) => damage >= health ? 0 : health - damage;

    public static uint CannonUpgradeCost(uint upgradeLevel) =>
        checked(CannonUpgradeBaseCost + upgradeLevel * CannonUpgradeCostStep);

    public static uint CannonDamageAfterUpgrade(uint damage, uint upgradeLevel) =>
        checked(damage + CannonDamagePerUpgrade * upgradeLevel);
}
