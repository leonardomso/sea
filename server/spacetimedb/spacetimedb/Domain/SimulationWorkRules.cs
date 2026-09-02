namespace Sea.Server;

public static class SimulationWorkRules
{
    public const bool ShipsBlockMovement = false;
    public const ulong PeriodicEffectIntervalTicks = 5;
    public const byte MovementShardCount = 64;
    public const byte HazardShardCount = 16;
    public const byte NpcShardCount = 4;
    public const byte CurrentRefreshBucketCount = 16;
    public const byte LootPickupBucketCount = 10;
    public const byte MovementReducerRateHz = 5;
    public const byte NpcReducerRateHz = 2;
    public const byte DispatchRateHz = 100;
    public const byte DispatchSlotsPerWorldTick = 10;
    public const byte MaximumMovementCatchUpTicks = 8;
    public const byte MovementSnapshotPartitionCount = 1;
    public const ushort MovementSnapshotDispatchIntervalMilliseconds = 16;
    public const byte HazardDispatchRateHz = 32;
    public const ushort IdleDispatchIntervalMilliseconds = 100;
    public const ushort IdleBackgroundIntervalMilliseconds = 1_000;
    public const ulong TelemetrySampleIntervalTicks = 100;

    public static bool ShouldSampleTelemetry(ulong tick) =>
        tick > 0 && tick % TelemetrySampleIntervalTicks == 0;

    public static double DispatchIntervalMilliseconds(bool hasConnectedPlayers) =>
        hasConnectedPlayers
            ? 1000d / DispatchRateHz
            : IdleDispatchIntervalMilliseconds;

    public static double SnapshotIntervalMilliseconds(bool hasConnectedPlayers) =>
        hasConnectedPlayers
            ? MovementSnapshotDispatchIntervalMilliseconds
            : IdleBackgroundIntervalMilliseconds;

    public static double HazardIntervalMilliseconds(bool hasConnectedPlayers) =>
        hasConnectedPlayers
            ? 1000d / HazardDispatchRateHz
            : IdleBackgroundIntervalMilliseconds;

    public static byte MovementShard(ulong shipEntityId) =>
        (byte)(shipEntityId % MovementShardCount);

    public static byte HazardShard(byte movementShard) =>
        (byte)(movementShard % HazardShardCount);

    public static byte NpcShard(ulong shipEntityId) =>
        (byte)(shipEntityId % NpcShardCount);

    public static bool ShouldRefreshCurrent(ulong shipEntityId, ulong tick) =>
        IsStaggeredWorkDue(shipEntityId, tick, CurrentRefreshBucketCount);

    public static bool ShouldProcessLootPickup(ulong shipEntityId, ulong tick) =>
        IsStaggeredWorkDue(shipEntityId, tick, LootPickupBucketCount);

    public static byte MovementSnapshotShard(ushort cursor) =>
        (byte)(cursor / MovementSnapshotPartitionCount);

    public static byte MovementSnapshotPartition(ushort cursor) =>
        (byte)(cursor % MovementSnapshotPartitionCount);

    public static ushort NextMovementSnapshotCursor(ushort cursor) =>
        (ushort)((cursor + 1) %
            (MovementShardCount * MovementSnapshotPartitionCount));

    public static bool IsInMovementSnapshotPartition(int index, byte partition) =>
        index >= 0 && partition < MovementSnapshotPartitionCount &&
        index % MovementSnapshotPartitionCount == partition;

    public static WorldObjectCode HazardKind(byte cursor) =>
        cursor < HazardShardCount ? WorldObjectCode.Storm : WorldObjectCode.Shoal;

    public static byte HazardDispatchShard(byte cursor) =>
        (byte)(cursor % HazardShardCount);

    public static byte NextHazardCursor(byte cursor) =>
        (byte)((cursor + 1) % (2 * HazardShardCount));

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
