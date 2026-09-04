namespace Sea.Server;

public static class WorldRules
{
    /// <summary>
    /// The playable world is this many squares on a side (SEA_5 §3.1). One square is
    /// one unit; there is no second unit and no conversion. (0,0) is the top-left
    /// corner, x grows east, y grows south. Per-map dimensions also live in content
    /// as <c>MapContent.Width</c>/<c>Height</c>, and <c>ValidateWorldExtent</c> is what
    /// holds the two equal: a map that disagrees with this figure fails to publish.
    /// </summary>
    public const float MapSizeSquares = 400f;

    public const float MapMin = 0f;

    /// <summary>The far edge as a coordinate. Equal to the extent only while the origin is 0.</summary>
    public const float MapMax = MapMin + MapSizeSquares;

    public const uint InitialHealth = 100;
    public const uint InitialGold = 0;
    public const uint TickRateHz = 10;

    /// <summary>How much time one tick of the simulation covers.</summary>
    public const float SecondsPerTick = 1f / TickRateHz;

    // Ships never collide with each other (SEA_5 §4.1.6); this only keeps a hull from
    // clipping the drawn edge of land, so a reef with radius 10 still blocks a touch at 10.
    // NavigationRules is its only reader now that the two IsBlocked overloads are gone, and
    // Phase 2's land mask replaces that reader in turn.
    public const float LandHazardPadding = 0.5f;

    public const uint InitialCannonDamage = 25;
    public const uint InitialCannonCooldownTicks = 20;
    public const uint EnemyInitialHealth = 100;
    public const uint EnemyCannonDamage = 5;
    public const uint EnemyCannonCooldownTicks = 40;
    public const uint EnemyGoldReward = 100;

    /// <summary>
    /// The circle of protected water around the harbor, in squares. Players spawn
    /// and respawn inside it and NPCs never pick a target sailing in it, so a fresh
    /// spawn is not sunk before it moves. That is all this constant does today.
    /// </summary>
    /// <remarks>
    /// <see cref="PortRules"/> already documents the other half of SEA_5 §10.3, that
    /// no shot crosses the line either way, but it takes the radius as an argument
    /// instead of reading this. Which of the two is the real safe-water radius is
    /// unsettled: §10.3 says zones are circles baked per map, which argues for
    /// content and against a global constant, and the harbor object in
    /// <c>maps.json</c> already carries a radius of its own that means something
    /// else again. Task 9.3 has to pick one before it adds <c>IsSafeWater</c>, or
    /// the port ends up with two radii from two sources.
    /// </remarks>
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

    /// <summary>
    /// Whether a position is on the chart. Both edges are closed, so exactly
    /// <see cref="MapMax"/> is inside.
    /// </summary>
    /// <remarks>
    /// The range comparisons already reject every non-finite input on their own: NaN
    /// fails both, and neither infinity is between the bounds. The explicit
    /// <see cref="float.IsFinite(float)"/> calls are here so the contract survives
    /// someone rewriting the comparisons, because <see cref="GeometryRules"/> and
    /// <see cref="SectorRules"/> both cite this method as the reason they can skip
    /// finite checks on the hot path. Deleting them as dead would leave the suite
    /// green and the contract broken. Note the guard is shared: GeometryRules names
    /// the command policy alongside this method, and several callers in this file
    /// reach GeometryRules without coming through here at all.
    /// </remarks>
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

    public static bool IsInRange(float fromX, float fromY, float toX, float toY, float range) =>
        GeometryRules.DistanceSquared(fromX, fromY, toX, toY) <= range * range;

    public static uint ApplyDamage(uint health, uint damage) => damage >= health ? 0 : health - damage;
}
