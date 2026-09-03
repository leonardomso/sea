using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed partial class NpcRulesTests
{
    [Fact]
    public void Fixed_seed_roaming_replays_and_stays_inside_the_chart()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { HomeX = 30f, HomeY = -20f };

        var first = NpcRules.RoamDestination(snapshot);
        var replay = NpcRules.RoamDestination(snapshot);

        Assert.Equal(first, replay);
        Assert.InRange(first.X, WorldRules.MapMin, WorldRules.MapMax);
        Assert.InRange(first.Y, WorldRules.MapMin, WorldRules.MapMax);
    }

    [Theory]
    [InlineData(0f, 0f, 500UL)]
    [InlineData(30f, -20f, 505UL)]
    [InlineData(-60f, 70f, 510UL)]
    public void Roaming_patrols_the_waters_around_home_with_a_real_leg(
        float homeX,
        float homeY,
        ulong tick)
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = homeX,
            Y = homeY,
            HomeX = homeX,
            HomeY = homeY,
            DecisionTick = tick,
        };

        var destination = NpcRules.RoamDestination(snapshot);

        Assert.InRange(
            CombatRules.Distance(homeX, homeY, destination.X, destination.Y),
            NpcRules.MinimumRoamLeg,
            NpcRules.RoamRadius);
    }

    [Fact]
    public void Roaming_near_the_chart_edge_stays_on_the_chart()
    {
        for (var tick = 0UL; tick < 200; tick += NpcRules.DecisionIntervalTicks)
        {
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                X = WorldRules.MapMax,
                Y = WorldRules.MapMin,
                HomeX = WorldRules.MapMax,
                HomeY = WorldRules.MapMin,
                DecisionTick = tick,
            });

            Assert.InRange(destination.X, WorldRules.MapMin, WorldRules.MapMax);
            Assert.InRange(destination.Y, WorldRules.MapMin, WorldRules.MapMax);
        }
    }

    [Fact]
    public void Idle_npc_sets_course_for_a_roam_waypoint()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { HomeX = 30f, HomeY = -20f };

        var decision = NpcRules.Decide(snapshot);
        var waypoint = NpcRules.RoamDestination(snapshot);

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.Equal(waypoint.X, decision.DestinationX);
        Assert.Equal(waypoint.Y, decision.DestinationY);
    }

    [Fact]
    public void RoamingNpcKeepsItsExistingCourse()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            HasCourse = true,
            CourseX = 30f,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    [Fact]
    public void Roaming_npc_plots_the_next_leg_just_before_the_current_one_ends()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = 24f,
            HasCourse = true,
            CourseX = 30f,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.True(
            CombatRules.Distance(24f, 0f, decision.DestinationX, decision.DestinationY) >=
            NpcRules.MinimumRoamLeg);
    }

    [Fact]
    public void Roaming_never_plots_a_leg_into_an_island()
    {
        var island = new NavigationBlocker(20f, 0f, 30f);
        for (var tick = 0UL; tick < 400; tick += NpcRules.DecisionIntervalTicks)
        {
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                DecisionTick = tick,
                Blockers = [island],
            });

            Assert.False(NavigationRules.IsDestinationBlocked(destination.X, destination.Y, [island]));
        }
    }

    [Fact]
    public void Hostile_homes_sit_beyond_a_full_roam_leg_from_the_harbor_waters()
    {
        Assert.Equal(
            NpcRules.RoamRadius + WorldRules.HarborSafeRadius,
            NpcRules.HostileHomeClearance);
    }

    [Theory]
    [InlineData(98UL, 60f)]
    [InlineData(99UL, -60f)]
    public void Roaming_swings_the_next_leg_on_around_home_in_one_fixed_direction(
        ulong seed,
        float expectedStepDegrees)
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = 20f,
            Y = 0f,
            HomeX = 0f,
            HomeY = 0f,
            DecisionSeed = seed,
        };

        var destination = NpcRules.RoamDestination(snapshot);
        var bearing = MathF.Atan2(destination.Y, destination.X) * 180f / MathF.PI;

        Assert.Equal(expectedStepDegrees, bearing, 0.5f);
        Assert.InRange(
            CombatRules.Distance(0f, 0f, destination.X, destination.Y),
            NpcRules.MinimumRoamLeg,
            NpcRules.RoamRadius);
    }

    [Fact]
    public void Roaming_closes_a_full_loop_around_home()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = 25f,
            Y = 0f,
            HomeX = 0f,
            HomeY = 0f,
            DecisionSeed = 98,
        };
        var bearings = new List<float>();
        for (var leg = 0; leg < 6; leg++)
        {
            var destination = NpcRules.RoamDestination(snapshot with
            {
                DecisionTick = snapshot.DecisionTick + (ulong)leg * NpcRules.DecisionIntervalTicks,
            });
            bearings.Add((MathF.Atan2(destination.Y, destination.X) * 180f / MathF.PI + 360f) % 360f);
            snapshot = snapshot with { X = destination.X, Y = destination.Y };
        }

        Assert.Equal([60f, 120f, 180f, 240f, 300f, 0f], bearings.Select(bearing => MathF.Round(bearing)));
    }
}
