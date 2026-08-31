namespace Sea.Server;

public static class WorldRules
{
    public const float MapMin = -100f;
    public const float MapMax = 100f;
    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 20;
    public const float CollisionPadding = 0.5f;

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
}
