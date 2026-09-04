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

    [Theory]
    [InlineData(10ul, 25ul, true, 25ul)]
    [InlineData(10ul, 25ul, false, 18ul)]
    [InlineData(10ul, 12ul, true, 12ul)]
    [InlineData(10ul, 12ul, false, 11ul)]
    [InlineData(15ul, 15ul, true, 15ul)]
    public void IdleShardsResumeAtTheCurrentTickInsteadOfCatchingUp(
        ulong lastSimulatedTick,
        ulong currentTick,
        bool shardWasIdle,
        ulong expectedFirstTick)
    {
        Assert.Equal(
            expectedFirstTick,
            SimulationWorkRules.FirstMovementTick(lastSimulatedTick, currentTick, shardWasIdle));
    }

    [Fact]
    public void DispatcherRunsOneWorldTickPerIntervalWhilePlayersAreConnected()
    {
        Assert.Equal(
            1000d / WorldRules.TickRateHz,
            SimulationWorkRules.DispatchIntervalMilliseconds);
    }

    [Fact]
    public void DispatchIntervalStaysAtThePlayRateSoTheScheduleNeverNeedsRewriting()
    {
        Assert.Equal(100d, SimulationWorkRules.DispatchIntervalMilliseconds);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(1u, true)]
    [InlineData(7u, true)]
    public void AnIdleWorldSkipsItsTickInsteadOfSlowingTheTimer(
        uint connectedPlayerCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            SimulationWorkRules.ShouldAdvanceWorld(connectedPlayerCount));
    }

    [Theory]
    [InlineData(1ul, false)]
    [InlineData(4ul, false)]
    [InlineData(5ul, true)]
    [InlineData(10ul, true)]
    public void HazardsApplyEveryHalfSecond(ulong tick, bool expected)
    {
        Assert.Equal(
            500d,
            SimulationWorkRules.HazardIntervalTicks * 1000d / WorldRules.TickRateHz);
        Assert.Equal(expected, SimulationWorkRules.ShouldApplyHazards(tick));
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
        // 175,175 sits inside chunk 3, not on a boundary, so only a radius that
        // actually reaches spills into its neighbours: 40 squares covers 135..215
        // and takes in chunks 2 through 4, where 10 would stay in chunk 3 alone.
        // Centring this on a boundary instead would give the same answer for any
        // radius under a chunk, and the name would stop meaning anything.
        var bounds = SpatialRules.BoundsAround(
            175f,
            175f,
            SpatialRules.MaximumWorldInfluenceRadiusSquares);

        Assert.Equal(2, bounds.MinX);
        Assert.Equal(4, bounds.MaxX);
        Assert.Equal(2, bounds.MinY);
        Assert.Equal(4, bounds.MaxY);
    }

    [Fact]
    public void Course_bounds_cover_the_whole_route_with_obstacle_padding()
    {
        // The route itself runs from chunk 1 to chunk 6. Only the padding carries the
        // bounds out to the edges of the grid, so this fails if the padding is dropped.
        // A route that already overshot the map would clamp to 0-7 with no padding at
        // all and the name of this test would be a lie.
        var bounds = SpatialRules.BoundsForSegment(60f, 60f, 340f, 340f, 20f);

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

    [Fact]
    public void Rescheduled_work_cannot_execute_twice_on_the_same_tick()
    {
        var nextTick = SimulationWorkRules.NextPeriodicTick(50, 10);

        Assert.False(SimulationWorkRules.IsDue(true, nextTick, 50));
        Assert.True(SimulationWorkRules.IsDue(true, nextTick, 60));
    }

    [Theory]
    [InlineData(EffectCode.Slowed, HotPathCodes.SlowedMovementMask)]
    [InlineData(EffectCode.Burning, 0)]
    [InlineData(EffectCode.ReloadSlowed, 0)]
    [InlineData(EffectCode.None, 0)]
    public void Only_movement_effects_are_cached_on_the_ship(EffectCode effect, byte expected)
    {
        Assert.Equal(expected, HotPathCodes.MovementMask(effect));
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

    [Fact]
    public void EveryMovementShardSailsOnceInsideTheStride()
    {
        for (byte shard = 0; shard < SimulationWorkRules.MovementShardCount; shard++)
        {
            var due = Enumerable
                .Range(0, SimulationWorkRules.MovementShardStride)
                .Count(tick => SimulationWorkRules.ShouldAdvanceMovementShard(shard, (ulong)tick));

            Assert.Equal(1, due);
        }
    }

    [Fact]
    public void ATickSailsTheSameShareOfTheFleetEveryTime()
    {
        var expected = SimulationWorkRules.MovementShardCount /
            SimulationWorkRules.MovementShardStride;

        for (var tick = 0UL; tick < 16UL; tick++)
        {
            var advanced = Enumerable
                .Range(0, SimulationWorkRules.MovementShardCount)
                .Count(shard => SimulationWorkRules.ShouldAdvanceMovementShard((byte)shard, tick));

            Assert.Equal(expected, advanced);
        }
    }

    // A shard that sat out the stride replays every tick it missed when its turn comes, so
    // the water a ship sails through is the same whatever the stride is set to.
    [Fact]
    public void AShardThatSatOutTheStrideCatchesUpOnEveryTickItMissed()
    {
        var first = SimulationWorkRules.FirstMovementTick(
            lastSimulatedTick: 10,
            currentTick: 10 + SimulationWorkRules.MovementShardStride,
            shardWasIdle: false);

        Assert.Equal(11UL, first);
    }
}
