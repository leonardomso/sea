using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SimulationWorkRulesTests
{
    [Fact]
    public void PeriodicMovementWorkIsEvenlyStaggeredInsideEveryShard()
    {
        for (byte shard = 0; shard < SimulationWorkRules.MovementShardCount; shard++)
        {
            var entities = Enumerable.Range(0, 64)
                .Select(index => (ulong)(index * SimulationWorkRules.MovementShardCount + shard))
                .ToArray();

            for (ulong tick = 0; tick < SimulationWorkRules.CurrentRefreshBucketCount; tick++)
            {
                Assert.Equal(
                    entities.Length / SimulationWorkRules.CurrentRefreshBucketCount,
                    entities.Count(entity =>
                        SimulationWorkRules.ShouldRefreshCurrent(entity, tick)));
            }
        }
    }

    [Fact]
    public void LootPickupChecksRunOncePerTenTicks()
    {
        const ulong entityId = 1234;

        var dueTicks = Enumerable.Range(0, 20)
            .Where(tick => SimulationWorkRules.ShouldProcessLootPickup(
                entityId,
                (ulong)tick))
            .ToArray();

        Assert.Equal(2, dueTicks.Length);
        Assert.Equal(10, dueTicks[1] - dueTicks[0]);
    }

    [Fact]
    public void MovementSnapshotCursorVisitsEveryShardAndPartitionOncePerCycle()
    {
        var work = Enumerable.Range(
                0,
                SimulationWorkRules.MovementShardCount *
                    SimulationWorkRules.MovementSnapshotPartitionCount)
            .Select(cursor => (
                Shard: SimulationWorkRules.MovementSnapshotShard((ushort)cursor),
                Partition: SimulationWorkRules.MovementSnapshotPartition((ushort)cursor)))
            .ToArray();

        Assert.Equal(work.Length, work.Distinct().Count());
        Assert.Equal((ushort)0, SimulationWorkRules.NextMovementSnapshotCursor(
            (ushort)(work.Length - 1)));
    }

    [Fact]
    public void MovementSnapshotPartitionsCoverEachIndexExactlyOnce()
    {
        for (var index = 0; index < 100; index++)
        {
            Assert.Single(
                Enumerable.Range(
                    0,
                    SimulationWorkRules.MovementSnapshotPartitionCount),
                partition => SimulationWorkRules.IsInMovementSnapshotPartition(
                    index,
                    (byte)partition));
        }
    }

    [Theory]
    [InlineData(0ul, 2ul, 1ul)]
    [InlineData(10ul, 12ul, 11ul)]
    [InlineData(10ul, 15ul, 11ul)]
    [InlineData(10ul, 25ul, 18ul)]
    [InlineData(15ul, 15ul, 16ul)]
    public void MovementReducersRunAtMostTheConfiguredOrderedTenHertzSubsteps(
        ulong lastSimulatedTick,
        ulong currentTick,
        ulong expectedFirstTick)
    {
        Assert.Equal(
            expectedFirstTick,
            SimulationWorkRules.FirstMovementTick(lastSimulatedTick, currentTick));
    }

    [Fact]
    public void MovementShardsMapEvenlyOntoHazardShards()
    {
        for (byte hazardShard = 0;
             hazardShard < SimulationWorkRules.HazardShardCount;
             hazardShard++)
        {
            Assert.Equal(
                SimulationWorkRules.MovementShardCount /
                    SimulationWorkRules.HazardShardCount,
                Enumerable.Range(0, SimulationWorkRules.MovementShardCount)
                    .Count(shard => SimulationWorkRules.HazardShard((byte)shard) == hazardShard));
        }
    }

    [Fact]
    public void HazardCursorVisitsEveryKindAndShardOncePerCycle()
    {
        var work = Enumerable.Range(0, 2 * SimulationWorkRules.HazardShardCount)
            .Select(cursor => (
                Kind: SimulationWorkRules.HazardKind((byte)cursor),
                Shard: SimulationWorkRules.HazardDispatchShard((byte)cursor)))
            .ToArray();

        Assert.Equal(work.Length, work.Distinct().Count());
        Assert.Equal(
            (byte)0,
            SimulationWorkRules.NextHazardCursor((byte)(work.Length - 1)));
    }

    [Fact]
    public void DispatcherAdvancesTheWorldClockAtTenHertz()
    {
        Assert.Equal(
            (uint)WorldRules.TickRateHz,
            SimulationWorkRules.DispatchRateHz /
                (uint)SimulationWorkRules.DispatchSlotsPerWorldTick);
    }

    [Fact]
    public void SimulationCadenceSlowsOnlyWhenNoPlayerIsConnected()
    {
        Assert.Equal(10d, SimulationWorkRules.DispatchIntervalMilliseconds(true));
        Assert.Equal(16d, SimulationWorkRules.SnapshotIntervalMilliseconds(true));
        Assert.Equal(31.25d, SimulationWorkRules.HazardIntervalMilliseconds(true));
        Assert.Equal(100d, SimulationWorkRules.DispatchIntervalMilliseconds(false));
        Assert.Equal(1_000d, SimulationWorkRules.SnapshotIntervalMilliseconds(false));
        Assert.Equal(1_000d, SimulationWorkRules.HazardIntervalMilliseconds(false));
    }

    [Theory]
    [InlineData(0ul, false)]
    [InlineData(99ul, false)]
    [InlineData(100ul, true)]
    [InlineData(200ul, true)]
    public void TelemetryUsesSparseDeterministicTicks(ulong tick, bool expected)
    {
        Assert.Equal(expected, SimulationWorkRules.ShouldSampleTelemetry(tick));
    }

    [Theory]
    [InlineData(false, 10ul, 10ul, false)]
    [InlineData(true, 11ul, 10ul, false)]
    [InlineData(true, 10ul, 10ul, true)]
    [InlineData(true, 9ul, 10ul, true)]
    public void Work_is_due_only_when_active_and_scheduled_at_or_before_the_tick(
        bool active,
        ulong nextProcessTick,
        ulong currentTick,
        bool expected)
    {
        Assert.Equal(
            expected,
            SimulationWorkRules.IsDue(active, nextProcessTick, currentTick));
    }

    [Theory]
    [InlineData(0ul, 5ul, 5ul)]
    [InlineData(9ul, 5ul, 10ul)]
    [InlineData(10ul, 5ul, 15ul)]
    [InlineData(ulong.MaxValue, 5ul, ulong.MaxValue)]
    public void Periodic_work_advances_to_the_first_tick_after_now(
        ulong currentTick,
        ulong interval,
        ulong expected)
    {
        Assert.Equal(expected, SimulationWorkRules.NextPeriodicTick(currentTick, interval));
    }

    [Theory]
    [InlineData(WorldObjectCode.Island, true)]
    [InlineData(WorldObjectCode.Reef, true)]
    [InlineData(WorldObjectCode.Storm, false)]
    [InlineData(WorldObjectCode.Shoal, false)]
    public void Only_land_and_reefs_block_navigation(WorldObjectCode kind, bool expected)
    {
        Assert.Equal(expected, HotPathCodes.BlocksMovement(kind));
    }

    [Fact]
    public void Ships_never_block_other_ships()
    {
        Assert.False(SimulationWorkRules.ShipsBlockMovement);
    }

    [Fact]
    public void Spatial_bounds_include_neighboring_chunks_for_large_hazards()
    {
        var bounds = SpatialRules.BoundsAround(
            0f,
            0f,
            SpatialRules.MaximumWorldInfluenceRadius);

        Assert.Equal(3, bounds.MinX);
        Assert.Equal(4, bounds.MaxX);
        Assert.Equal(3, bounds.MinY);
        Assert.Equal(4, bounds.MaxY);
    }

    [Fact]
    public void Course_bounds_cover_the_whole_route_with_obstacle_padding()
    {
        var bounds = SpatialRules.BoundsForSegment(-80f, -80f, 80f, 80f, 20f);

        Assert.Equal(new ChunkBounds(0, 7, 0, 7), bounds);
    }

    [Theory]
    [InlineData("round", AmmunitionCode.Round)]
    [InlineData("chain", AmmunitionCode.Chain)]
    [InlineData("grapeshot", AmmunitionCode.Grapeshot)]
    [InlineData("incendiary", AmmunitionCode.Incendiary)]
    public void Ammunition_ids_are_resolved_before_the_hot_path(
        string id,
        AmmunitionCode expected)
    {
        Assert.True(HotPathCodes.TryParseAmmunition(id, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("hull", WeakPointCode.Hull)]
    [InlineData("sails", WeakPointCode.Sails)]
    [InlineData("cannons", WeakPointCode.Cannons)]
    public void Weak_point_ids_are_resolved_before_the_hot_path(
        string id,
        WeakPointCode expected)
    {
        Assert.True(HotPathCodes.TryParseWeakPoint(id, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(StatusCode.Burning, 10ul)]
    [InlineData(StatusCode.Flooding, 10ul)]
    [InlineData(StatusCode.EmergencyPump, 5ul)]
    [InlineData(StatusCode.Brace, ulong.MaxValue)]
    [InlineData(StatusCode.DisabledSails, ulong.MaxValue)]
    public void Status_work_is_scheduled_only_when_it_has_periodic_effects(
        StatusCode status,
        ulong expectedInterval)
    {
        Assert.Equal(expectedInterval, SimulationWorkRules.StatusInterval(status));
    }

    [Theory]
    [InlineData(StatusCode.Burning, 40ul, 100ul, 50ul)]
    [InlineData(StatusCode.EmergencyPump, 40ul, 43ul, 43ul)]
    [InlineData(StatusCode.Brace, 40ul, 80ul, 80ul)]
    public void Status_work_uses_the_next_period_or_expiry_whichever_comes_first(
        StatusCode status,
        ulong currentTick,
        ulong expiresAtTick,
        ulong expected)
    {
        Assert.Equal(
            expected,
            SimulationWorkRules.NextStatusProcessTick(status, currentTick, expiresAtTick));
    }

    [Fact]
    public void Rescheduled_work_cannot_execute_twice_on_the_same_tick()
    {
        var nextTick = SimulationWorkRules.NextPeriodicTick(50, 10);

        Assert.False(SimulationWorkRules.IsDue(true, nextTick, 50));
        Assert.True(SimulationWorkRules.IsDue(true, nextTick, 60));
    }

    [Theory]
    [InlineData(StatusCode.FullSail, HotPathCodes.FullSailMovementMask)]
    [InlineData(StatusCode.Slowed, HotPathCodes.SlowedMovementMask)]
    [InlineData(StatusCode.Burning, 0)]
    public void Only_movement_statuses_are_cached_on_the_ship(StatusCode status, byte expected)
    {
        Assert.Equal(expected, HotPathCodes.MovementMask(status));
    }

    [Fact]
    public void Movement_shards_are_deterministic_and_evenly_distributed()
    {
        var counts = new int[SimulationWorkRules.MovementShardCount];
        var entityCount = SimulationWorkRules.MovementShardCount * 100;
        for (ulong entityId = 0; entityId < (ulong)entityCount; entityId++)
        {
            counts[SimulationWorkRules.MovementShard(entityId)]++;
        }

        Assert.All(counts, count => Assert.Equal(100, count));
    }

    [Fact]
    public void Npc_shards_are_deterministic_and_evenly_distributed()
    {
        var counts = new int[SimulationWorkRules.NpcShardCount];
        var entityCount = SimulationWorkRules.NpcShardCount * 100;
        for (ulong entityId = 0; entityId < (ulong)entityCount; entityId++)
        {
            counts[SimulationWorkRules.NpcShard(entityId)]++;
        }

        Assert.All(counts, count => Assert.Equal(100, count));
    }
}
