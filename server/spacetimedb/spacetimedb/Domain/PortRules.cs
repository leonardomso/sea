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
}
