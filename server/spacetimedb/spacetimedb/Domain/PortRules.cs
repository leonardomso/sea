namespace Sea.Server;

/// <summary>
/// Port Lowell. Inside the circle a ship cannot be hit, carries no effects and cannot fire;
/// leaving is a channel, so the port shelters a ship that has withdrawn rather than one that is
/// still trading shots.
/// </summary>
public static class PortRules
{
    /// <summary>Mirrors <c>stat_caps.portCastOffSeconds</c>.</summary>
    public const ulong CastOffTicks = 3 * WorldRules.TickRateHz;

    public static bool IsInside(float x, float y, float portX, float portY, float portRadius) =>
        WorldRules.IsInRange(x, y, portX, portY, portRadius);

    /// <summary>
    /// Only a course that ends outside the circle has to be cast off for. Moving from one berth
    /// to another inside the port is just sailing.
    /// </summary>
    public static bool RequiresCastOff(
        bool inPort,
        float destinationX,
        float destinationY,
        float portX,
        float portY,
        float portRadius) =>
        inPort && !IsInside(destinationX, destinationY, portX, portY, portRadius);

    /// <summary>
    /// SEA_5 §10.3: no fire either way inside thirty squares of a harbour. Wide
    /// enough that a hull leaving port is not chased out of it, narrow enough
    /// that it is not somewhere to hide from a fight.
    /// </summary>
    /// <remarks>
    /// This is a different circle from <see cref="IsInside"/>: that one takes a
    /// per-map berth radius (the harbor object's own drawn extent, 20 squares on
    /// Havenmere) and drives cast-off and the current in-port flag. Safe water is
    /// the flat thirty squares SEA_5 names, so it reads
    /// <see cref="WorldRules.HarborSafeRadiusSquares"/> -- already that value and
    /// already used for spawn protection and NPC avoidance elsewhere -- rather
    /// than a caller-supplied radius.
    /// </remarks>
    public static bool IsSafeWater(float x, float y, float harborX, float harborY) =>
        WorldRules.IsInRange(x, y, harborX, harborY, WorldRules.HarborSafeRadiusSquares);

    /// <summary>Shallow water crossable by no more than a third-rate hull (SEA_5 §10.1).</summary>
    public const byte DeepestShoalCrossingTier = 3;

    /// <summary>
    /// Whether a hull of this tier draws little enough water to cross a shoal. A
    /// small hull crosses it slowly (<see cref="TacticalRules.ShoalMultiplier"/>);
    /// a fourth or fifth rate draws too much and is turned back.
    /// </summary>
    public static bool CanCrossShoal(byte tier) => tier <= DeepestShoalCrossingTier;
}
