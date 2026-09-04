namespace Sea.Server;

public static class SimulationWorkRules
{
    public const bool ShipsBlockMovement = false;
    public const ulong PeriodicEffectIntervalTicks = 5;
    public const byte MovementShardCount = 8;
    // A ship's motion is recomputed on the tick it happens. Sailing half the fleet per tick
    // halved the shard rows a tick touched, but it also meant every hull's published position
    // was up to 200ms old before it left the server, and a captain feels that on every click.
    // The saving was throughput we do not need at the fleet sizes we actually sail.
    public const byte MovementShardStride = 1;
    public const byte NpcShardCount = 4;
    public const byte CurrentRefreshBucketCount = 16;
    public const byte LootPickupBucketCount = 10;
    public const byte MaximumMovementCatchUpTicks = 8;
    public const ulong HazardIntervalTicks = 5;
    public const ulong TelemetrySampleIntervalTicks = 100;

    // Flip on to log a "PROF <phase>" line after every dispatch phase; feed the module
    // logs to scripts/profile-dispatch.mjs for per-phase timings.
    public static bool ProfileDispatchPhases => false;

    public static bool ShouldSampleTelemetry(ulong tick) =>
        tick > 0 && tick % TelemetrySampleIntervalTicks == 0;

    // Reducers execute one at a time per database, so the whole world tick runs as a
    // single transaction: splitting it into finer timers only multiplies commit and
    // subscription overhead without adding parallelism.
    //
    // The interval is fixed for the lifetime of the schedule: SpacetimeDB binds a
    // scheduled row's interval when the row is inserted, so rewriting ScheduleAt later
    // leaves the host firing at the interval it was created with. The dispatch timer is
    // therefore always created at the play interval and an idle world skips its work
    // instead of slowing the timer down.
    public static double DispatchIntervalMilliseconds => 1000d / WorldRules.TickRateHz;

    // Nobody is watching an empty world, so the dispatch returns before it touches the
    // clock: the simulation resumes on the tick the first player connects.
    public static bool ShouldAdvanceWorld(uint connectedPlayerCount) =>
        connectedPlayerCount > 0;

    // A tick sails half the fleet. A shard integrates every tick it sat out when its turn
    // comes round, so the water is the same either way; what halves is the number of rows a
    // tick touches, which is the whole of its cost. A course set between a shard's turns
    // waits out the remainder of the stride, so a command bites within a tick of the ack.
    public static bool ShouldAdvanceMovementShard(byte shardId, ulong tick) =>
        (ulong)(shardId % MovementShardStride) == tick % MovementShardStride;

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

    private static bool IsStaggeredWorkDue(
        ulong shipEntityId,
        ulong tick,
        byte bucketCount) =>
        shipEntityId / MovementShardCount % bucketCount == tick % bucketCount;
}
