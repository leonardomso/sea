namespace Sea.Server;

public static class SimulationWorkRules
{
    public const bool ShipsBlockMovement = false;
    public const ulong PeriodicEffectIntervalTicks = 5;
    public const byte MovementShardCount = 8;
    public const byte NpcShardCount = 4;
    public const byte CurrentRefreshBucketCount = 16;
    public const byte LootPickupBucketCount = 10;
    public const byte MaximumMovementCatchUpTicks = 8;
    public const ulong HazardIntervalTicks = 5;
    public const ushort IdleDispatchIntervalMilliseconds = 1_000;
    public const ulong TelemetrySampleIntervalTicks = 100;

    // Flip on to log a "PROF <phase>" line after every dispatch phase; feed the module
    // logs to scripts/profile-dispatch.mjs for per-phase timings.
    public static bool ProfileDispatchPhases => false;

    public static bool ShouldSampleTelemetry(ulong tick) =>
        tick > 0 && tick % TelemetrySampleIntervalTicks == 0;

    // Reducers execute one at a time per database, so the whole world tick runs as a
    // single transaction: splitting it into finer timers only multiplies commit and
    // subscription overhead without adding parallelism.
    public static double DispatchIntervalMilliseconds(bool hasConnectedPlayers) =>
        hasConnectedPlayers
            ? 1000d / WorldRules.TickRateHz
            : IdleDispatchIntervalMilliseconds;

    public static bool ShouldApplyHazards(ulong tick) =>
        tick % HazardIntervalTicks == 0;

    public static byte MovementShard(ulong shipEntityId) =>
        (byte)(shipEntityId % MovementShardCount);

    public static byte NpcShard(ulong shipEntityId) =>
        (byte)(shipEntityId % NpcShardCount);

    public static bool ShouldRefreshCurrent(ulong shipEntityId, ulong tick) =>
        IsStaggeredWorkDue(shipEntityId, tick, CurrentRefreshBucketCount);

    public static bool ShouldProcessLootPickup(ulong shipEntityId, ulong tick) =>
        IsStaggeredWorkDue(shipEntityId, tick, LootPickupBucketCount);

    // A shard that had nothing to sail last tick has nothing to catch up on: a ship
    // that just joined it starts sailing now instead of replaying the idle gap.
    public static ulong FirstMovementTick(
        ulong lastSimulatedTick,
        ulong currentTick,
        bool shardWasIdle) =>
        shardWasIdle ? currentTick : FirstMovementTick(lastSimulatedTick, currentTick);

    public static ulong FirstMovementTick(ulong lastSimulatedTick, ulong currentTick)
    {
        if (currentTick <= lastSimulatedTick)
        {
            return currentTick + 1;
        }

        var availableTicks = currentTick - lastSimulatedTick;
        return availableTicks <= MaximumMovementCatchUpTicks
            ? lastSimulatedTick + 1
            : currentTick - MaximumMovementCatchUpTicks + 1;
    }

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

    private static bool IsStaggeredWorkDue(
        ulong shipEntityId,
        ulong tick,
        byte bucketCount) =>
        shipEntityId / MovementShardCount % bucketCount == tick % bucketCount;
}
