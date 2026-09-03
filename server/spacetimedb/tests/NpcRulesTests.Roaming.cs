using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed partial class NpcRulesTests
{
    private static SpawnPoint OnRoute(PatrolRoute route, float bearingDegrees)
    {
        var radians = bearingDegrees * MathF.PI / 180f;
        return new SpawnPoint(
            route.CenterX + MathF.Cos(radians) * route.Radius,
            route.CenterY + MathF.Sin(radians) * route.Radius);
    }

    private static float BearingOnRoute(PatrolRoute route, float x, float y) =>
        (MathF.Atan2(y - route.CenterY, x - route.CenterX) * 180f / MathF.PI + 360f) % 360f;

    [Fact]
    public void Fixed_seed_roaming_replays_and_stays_inside_the_chart()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { X = 30f, Y = -20f };

        var first = NpcRules.RoamDestination(snapshot);
        var replay = NpcRules.RoamDestination(snapshot);

        Assert.Equal(first, replay);
        Assert.InRange(first.X, WorldRules.MapMin, WorldRules.MapMax);
        Assert.InRange(first.Y, WorldRules.MapMin, WorldRules.MapMax);
    }

    [Fact]
    public void Every_patrol_route_is_a_wide_loop_that_fits_on_the_chart()
    {
        for (var seed = 0UL; seed < 64UL; seed++)
        {
            var route = NpcRules.RouteFor(seed);

            Assert.Equal(route, NpcRules.RouteFor(seed));
            Assert.InRange(route.Radius, NpcRules.MinimumRouteRadius, NpcRules.MaximumRouteRadius);
            Assert.InRange(
                route.CenterX - route.Radius,
                WorldRules.MapMin,
                WorldRules.MapMax);
            Assert.InRange(
                route.CenterX + route.Radius,
                WorldRules.MapMin,
                WorldRules.MapMax);
            Assert.InRange(
                route.CenterY - route.Radius,
                WorldRules.MapMin,
                WorldRules.MapMax);
            Assert.InRange(
                route.CenterY + route.Radius,
                WorldRules.MapMin,
                WorldRules.MapMax);
        }
    }

    [Fact]
    public void Patrol_routes_are_spread_across_the_chart_rather_than_stacked()
    {
        var centers = Enumerable
            .Range(0, 64)
            .Select(seed => NpcRules.RouteFor((ulong)seed))
            .ToArray();

        // A fleet whose routes all sat in one corner would leave most of the sea empty, so the
        // seeded centres have to reach both halves of the chart on both axes.
        Assert.Contains(centers, route => route.CenterX > 20f);
        Assert.Contains(centers, route => route.CenterX < -20f);
        Assert.Contains(centers, route => route.CenterY > 20f);
        Assert.Contains(centers, route => route.CenterY < -20f);
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(98UL)]
    [InlineData(505UL)]
    public void Roaming_plots_the_next_leg_on_the_route_ring(ulong seed)
    {
        var route = NpcRules.RouteFor(seed);
        var start = OnRoute(route, 25f);
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = start.X,
            Y = start.Y,
            DecisionSeed = seed,
        };

        var destination = NpcRules.RoamDestination(snapshot);

        Assert.Equal(
            route.Radius,
            CombatRules.Distance(route.CenterX, route.CenterY, destination.X, destination.Y),
            0.01f);
        Assert.True(
            CombatRules.Distance(start.X, start.Y, destination.X, destination.Y) >=
            NpcRules.MinimumRoamLeg);
    }

    [Fact]
    public void Roaming_near_the_chart_edge_stays_on_the_chart()
    {
        for (var seed = 0UL; seed < 64UL; seed++)
        {
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                X = WorldRules.MapMax,
                Y = WorldRules.MapMin,
                DecisionSeed = seed,
            });

            Assert.InRange(destination.X, WorldRules.MapMin, WorldRules.MapMax);
            Assert.InRange(destination.Y, WorldRules.MapMin, WorldRules.MapMax);
        }
    }

    [Fact]
    public void Idle_npc_sets_course_for_a_roam_waypoint()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { X = 30f, Y = -20f };

        var decision = NpcRules.Decide(snapshot);
        var waypoint = NpcRules.RoamDestination(snapshot);

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.Equal(waypoint.X, decision.DestinationX);
        Assert.Equal(waypoint.Y, decision.DestinationY);
    }

    [Fact]
    public void RoamingNpcKeepsItsExistingCourse()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol);
        var leg = OnRoute(NpcRules.RouteFor(snapshot.DecisionSeed), 0f);

        var decision = NpcRules.Decide(snapshot with
        {
            HasCourse = true,
            CourseX = leg.X,
            CourseY = leg.Y,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    [Fact]
    public void A_leg_plotted_off_the_route_is_replaced_by_one_back_onto_it()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol);
        var route = NpcRules.RouteFor(snapshot.DecisionSeed);

        // A course left over from a chase: it ends on the route centre, far off the ring.
        var decision = NpcRules.Decide(snapshot with
        {
            HasCourse = true,
            CourseX = route.CenterX,
            CourseY = route.CenterY,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.Equal(
            route.Radius,
            CombatRules.Distance(
                route.CenterX,
                route.CenterY,
                decision.DestinationX,
                decision.DestinationY),
            0.01f);
    }

    [Fact]
    public void Roaming_npc_plots_the_next_leg_just_before_the_current_one_ends()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol);
        var route = NpcRules.RouteFor(snapshot.DecisionSeed);
        var arrival = OnRoute(route, 0f);
        var ship = OnRoute(route, 6f);

        var decision = NpcRules.Decide(snapshot with
        {
            X = ship.X,
            Y = ship.Y,
            HasCourse = true,
            CourseX = arrival.X,
            CourseY = arrival.Y,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.True(
            CombatRules.Distance(ship.X, ship.Y, decision.DestinationX, decision.DestinationY) >=
            NpcRules.MinimumRoamLeg);
    }

    [Fact]
    public void Roaming_never_plots_a_leg_into_an_island()
    {
        for (var seed = 0UL; seed < 64UL; seed++)
        {
            // An island parked on the leg the ship would otherwise pick.
            var route = NpcRules.RouteFor(seed);
            var start = OnRoute(route, 0f);
            var blocked = OnRoute(route, 60f);
            var island = new NavigationBlocker(blocked.X, blocked.Y, 20f);
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                X = start.X,
                Y = start.Y,
                DecisionSeed = seed,
                Blockers = [island],
            });

            Assert.False(
                NavigationRules.IsDestinationBlocked(destination.X, destination.Y, [island]));
        }
    }

    [Fact]
    public void Hostile_homes_sit_clear_of_the_harbor_waters()
    {
        Assert.Equal(
            NpcRules.HomeAnchorRadius + WorldRules.HarborSafeRadius,
            NpcRules.HostileHomeClearance);
    }

    [Theory]
    [InlineData(98UL, 60f)]
    [InlineData(99UL, 300f)]
    public void Roaming_swings_the_next_leg_on_around_the_route_in_one_fixed_direction(
        ulong seed,
        float expectedBearingDegrees)
    {
        var route = NpcRules.RouteFor(seed);
        var start = OnRoute(route, 0f);
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = start.X,
            Y = start.Y,
            DecisionSeed = seed,
        };

        var destination = NpcRules.RoamDestination(snapshot);

        Assert.Equal(
            expectedBearingDegrees,
            BearingOnRoute(route, destination.X, destination.Y),
            0.5f);
    }

    [Fact]
    public void Roaming_closes_a_full_loop_around_its_route()
    {
        var route = NpcRules.RouteFor(98UL);
        var start = OnRoute(route, 0f);
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = start.X,
            Y = start.Y,
            DecisionSeed = 98UL,
        };
        var bearings = new List<float>();
        for (var leg = 0; leg < 6; leg++)
        {
            var destination = NpcRules.RoamDestination(snapshot);
            bearings.Add(BearingOnRoute(route, destination.X, destination.Y));
            snapshot = snapshot with { X = destination.X, Y = destination.Y };
        }

        Assert.Equal([60f, 120f, 180f, 240f, 300f, 0f], bearings.Select(bearing => MathF.Round(bearing)));
    }
}
