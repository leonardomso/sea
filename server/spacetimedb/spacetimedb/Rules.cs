namespace Sea.Server;

public static class WorldRules
{
    public const float MapMin = -100f;
    public const float MapMax = 100f;
    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 20;
    public const float CollisionPadding = 0.5f;
    public const uint InitialCannonDamage = 25;
    public const uint InitialCannonCooldownTicks = 20;
    public const uint EnemyInitialHealth = 100;
    public const uint EnemyCannonDamage = 5;
    public const uint EnemyCannonCooldownTicks = 40;
    public const uint EnemyGoldReward = 100;
    public const float CannonRange = 60f;

    public static bool IsInsideMap(float x, float y) =>
        float.IsFinite(x) &&
        float.IsFinite(y) &&
        x >= MapMin &&
        x <= MapMax &&
        y >= MapMin &&
        y <= MapMax;

    public static bool IsValidMove(float x, float y) => IsInsideMap(x, y);

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

    public static bool IsInRange(float sourceX, float sourceY, float targetX, float targetY, float range)
    {
        var dx = targetX - sourceX;
        var dy = targetY - sourceY;
        return dx * dx + dy * dy <= range * range;
    }

    public static uint ApplyDamage(uint health, uint damage) => damage >= health ? 0 : health - damage;
}
