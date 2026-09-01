using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SimulationWorkRulesTests
{
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
        for (ulong entityId = 0; entityId < 800; entityId++)
        {
            counts[SimulationWorkRules.MovementShard(entityId)]++;
        }

        Assert.All(counts, count => Assert.Equal(100, count));
    }
}
