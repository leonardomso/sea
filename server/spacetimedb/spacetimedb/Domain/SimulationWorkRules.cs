namespace Sea.Server;

public static class SimulationWorkRules
{
    public const bool ShipsBlockMovement = false;
    public const ulong PeriodicEffectIntervalTicks = 5;
    public const byte MovementShardCount = 8;

    public static byte MovementShard(ulong shipEntityId) =>
        (byte)(shipEntityId % MovementShardCount);

    public static bool IsDue(bool active, ulong nextProcessTick, ulong currentTick) =>
        active && nextProcessTick <= currentTick;

    public static ulong NextPeriodicTick(ulong currentTick, ulong interval)
    {
        ArgumentOutOfRangeException.ThrowIfZero(interval);

        if (currentTick == ulong.MaxValue)
        {
            return ulong.MaxValue;
        }

        var remainder = currentTick % interval;
        var delta = remainder == 0 ? interval : interval - remainder;
        return currentTick > ulong.MaxValue - delta
            ? ulong.MaxValue
            : currentTick + delta;
    }

    public static ulong StatusInterval(StatusCode code) => code switch
    {
        StatusCode.Burning or StatusCode.Flooding => WorldRules.TickRateHz,
        StatusCode.EmergencyPump => PeriodicEffectIntervalTicks,
        _ => ulong.MaxValue,
    };

    public static ulong NextStatusProcessTick(
        StatusCode code,
        ulong currentTick,
        ulong expiresAtTick)
    {
        var interval = StatusInterval(code);
        return interval == ulong.MaxValue
            ? expiresAtTick
            : Math.Min(expiresAtTick, NextPeriodicTick(currentTick, interval));
    }
}
