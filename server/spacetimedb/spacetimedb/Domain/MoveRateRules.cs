namespace Sea.Server;

/// <summary>
/// SEA_5 4.1.8: at most eight MoveTo a second per ship. Extra requests are
/// dropped, never queued, and every drop is counted against the trust score.
/// </summary>
/// <remarks>
/// A fixed window rather than a leaky bucket, because the rule is written as
/// "eight per second" and a captain who sees eight answered and four refused
/// can work out what happened. The state is two fields on the ship's own row,
/// so the check costs nothing and needs no table of its own.
/// </remarks>
public static class MoveRateRules
{
    public const uint MaximumPerSecond = 8;

    public const ulong WindowTicks = WorldRules.TickRateHz;

    public static bool Allow(ref ulong windowStartTick, ref uint usedInWindow, ulong tick)
    {
        if (tick >= windowStartTick + WindowTicks)
        {
            windowStartTick = tick;
            usedInWindow = 0;
        }

        if (usedInWindow >= MaximumPerSecond)
        {
            return false;
        }

        usedInWindow++;
        return true;
    }
}
