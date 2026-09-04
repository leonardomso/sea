using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SailingRulesTests
{
    [Fact]
    public void Sailing_accelerates_without_teleporting()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 0f, 0f),
            destinationX: 0f,
            destinationY: 100f,
            stopping: false,
            new SailingParameters(12f, 2f, 3f, 60f),
            deltaSeconds: 1f);

        Assert.Equal(2f, step.Speed, 3);
        Assert.InRange(step.PositionY, 0.9f, 1.1f);
        Assert.True(step.IsMoving);
    }

    [Fact]
    public void Sailing_turn_rate_is_limited()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 0f, 4f),
            destinationX: 100f,
            destinationY: 0f,
            stopping: false,
            new SailingParameters(12f, 2f, 3f, 30f),
            deltaSeconds: 1f);

        Assert.Equal(30f, step.HeadingDegrees, 3);
    }

    [Theory]
    [InlineData(0f, 100f, 0f)]
    [InlineData(100f, 0f, 90f)]
    [InlineData(0f, -100f, 180f)]
    [InlineData(-100f, 0f, 270f)]
    public void Desired_heading_uses_the_chart_compass(
        float destinationX,
        float destinationY,
        float expected)
    {
        Assert.Equal(
            expected,
            SailingRules.DesiredHeading(0f, 0f, destinationX, destinationY),
            3);
    }

    [Fact]
    public void Cached_heading_produces_the_same_sailing_step()
    {
        var state = new SailingState(-12f, 4f, 35f, 7f);
        var parameters = new SailingParameters(12f, 3f, 4f, 90f);
        var desired = SailingRules.DesiredHeading(-12f, 4f, 70f, -55f);

        var direct = SailingRules.Step(
            state,
            70f,
            -55f,
            stopping: false,
            parameters,
            deltaSeconds: 0.1f);
        var cached = SailingRules.StepTowardHeading(
            state,
            70f,
            -55f,
            desired,
            stopping: false,
            parameters,
            deltaSeconds: 0.1f);

        Assert.Equal(direct, cached);
    }

    [Theory]
    [InlineData(-720f)]
    [InlineData(-17.4f)]
    [InlineData(0f)]
    [InlineData(44.9f)]
    [InlineData(359.9f)]
    [InlineData(721f)]
    public void Trigonometry_lookup_stays_within_quarter_degree_accuracy(float degrees)
    {
        var radians = degrees * (MathF.PI / 180f);

        Assert.InRange(
            MathF.Abs(TrigonometryRules.SinDegrees(degrees) - MathF.Sin(radians)),
            0f,
            0.0022f);
        Assert.InRange(
            MathF.Abs(TrigonometryRules.CosDegrees(degrees) - MathF.Cos(radians)),
            0f,
            0.0022f);
    }

    [Fact]
    public void Sailing_to_a_destination_behind_the_ship_turns_before_applying_full_thrust()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 0f, 8f),
            destinationX: 0f,
            destinationY: -100f,
            stopping: false,
            new SailingParameters(12f, 2f, 3f, 30f),
            deltaSeconds: 1f);

        Assert.Equal(30f, step.HeadingDegrees, 3);
        Assert.Equal(5f, step.Speed, 3);
        Assert.True(step.PositionY > 0f,
            "A ship may coast through a turn, but it must never translate stern-first.");
    }

    [Fact]
    public void Arcade_handling_can_reverse_heading_in_half_a_second()
    {
        var state = new SailingState(0f, 0f, 0f, 8f);
        for (var tick = 0; tick < 5; tick++)
        {
            var step = SailingRules.Step(
                state,
                destinationX: 0f,
                destinationY: -100f,
                stopping: false,
                new SailingParameters(
                    12f,
                    3f,
                    4f,
                    360f),
                deltaSeconds: 0.1f);
            state = new SailingState(
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                step.Speed);
        }

        Assert.InRange(state.HeadingDegrees, 179.9f, 180.1f);
    }

    [Fact]
    public void Sailing_snaps_to_a_nearby_destination_instead_of_overshooting_and_orbiting()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 0f, 8f),
            destinationX: 0f,
            destinationY: 3f,
            stopping: false,
            new SailingParameters(12f, 2f, 3f, 60f),
            deltaSeconds: 1f);

        Assert.True(step.Arrived);
        Assert.False(step.IsMoving);
        Assert.Equal(0f, step.PositionX, 3);
        Assert.Equal(3f, step.PositionY, 3);
        Assert.Equal(0f, step.Speed, 3);
    }

    [Fact]
    public void Stop_course_decelerates_instead_of_zeroing_speed()
    {
        var step = SailingRules.Step(
            new SailingState(0f, 0f, 90f, 8f),
            destinationX: 0f,
            destinationY: 0f,
            stopping: true,
            new SailingParameters(12f, 2f, 3f, 60f),
            deltaSeconds: 1f);

        Assert.Equal(5f, step.Speed, 3);
        Assert.True(step.IsMoving);
    }

    [Fact]
    public void Collision_check_detects_a_course_through_a_reef()
    {
        Assert.True(SailingRules.SegmentIntersectsCircle(
            -10f, 0f, 10f, 0f, 0f, 0f, radius: 3f));
        Assert.False(SailingRules.SegmentIntersectsCircle(
            -10f, 10f, 10f, 10f, 0f, 0f, radius: 3f));
    }

    [Fact]
    public void Navigation_plots_a_clear_detour_around_an_island()
    {
        var blockers = new[] { new NavigationBlocker(0f, 0f, 10f) };

        Assert.True(NavigationRules.TryFindDetour(
            -40f, 0f, 40f, 0f, blockers, out var waypoint));
        Assert.False(SailingRules.SegmentIntersectsCircle(
            -40f, 0f, waypoint.X, waypoint.Y, 0f, 0f,
            10f + WorldRules.CollisionPadding));
        Assert.False(SailingRules.SegmentIntersectsCircle(
            waypoint.X, waypoint.Y, 40f, 0f, 0f, 0f,
            10f + WorldRules.CollisionPadding));
    }

    [Fact]
    public void Plotted_detour_sails_around_the_island_and_reaches_open_water_destination()
    {
        var blockers = new[] { new NavigationBlocker(0f, 0f, 10f) };
        var state = new SailingState(-40f, 0f, 90f, 0f);
        const float destinationX = 40f;
        const float destinationY = 0f;
        var arrived = false;

        for (var tick = 0; tick < 1_000 && !arrived; tick++)
        {
            var hasDetour = NavigationRules.TryFindDetour(
                state.PositionX,
                state.PositionY,
                destinationX,
                destinationY,
                blockers,
                out var waypoint);
            var targetX = hasDetour ? waypoint.X : destinationX;
            var targetY = hasDetour ? waypoint.Y : destinationY;
            var step = SailingRules.Step(
                state,
                targetX,
                targetY,
                stopping: false,
                new SailingParameters(12f, 3f, 4f, 55f),
                deltaSeconds: 0.1f);

            Assert.True(NavigationRules.Distance(
                    step.PositionX, step.PositionY, 0f, 0f) >
                10f + WorldRules.CollisionPadding);
            state = new SailingState(
                step.PositionX,
                step.PositionY,
                step.HeadingDegrees,
                step.Speed);
            arrived = !hasDetour && step.Arrived;
        }

        // She rests on the mark or inside the arrival radius of it, never short of the last
        // leg and never orbiting it, which is the whole of what the detour has to deliver.
        Assert.True(arrived);
        Assert.True(
            NavigationRules.Distance(
                state.PositionX, state.PositionY, destinationX, destinationY) <=
            SailingRules.ArrivalRadius + 0.01f);
    }

    [Fact]
    public void Navigation_does_not_add_a_waypoint_when_the_direct_course_is_clear()
    {
        var blockers = new[] { new NavigationBlocker(0f, 30f, 10f) };

        Assert.False(NavigationRules.TryFindDetour(
            -40f, 0f, 40f, 0f, blockers, out _));
    }

    [Fact]
    public void Island_centers_are_rejected_but_nearby_open_water_remains_navigable()
    {
        var blockers = new[] { new NavigationBlocker(0f, 0f, 10f) };

        Assert.True(NavigationRules.IsDestinationBlocked(0f, 0f, blockers));
        Assert.False(NavigationRules.IsDestinationBlocked(0f, 11f, blockers));
    }

    [Fact]
    public void Safe_spawn_is_deterministic_and_avoids_blockers()
    {
        var blockers = new[]
        {
            new SpawnBlocker(0f, 0f, 70f),
            new SpawnBlocker(-80f, -80f, 8f),
        };

        Assert.True(SpawnRules.TryFindSafePosition(42, blockers, out var first));
        Assert.True(SpawnRules.TryFindSafePosition(42, blockers, out var second));
        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.True(WorldRules.IsInsideMap(first.X, first.Y));
        Assert.All(blockers, blocker =>
            Assert.False(SpawnRules.Overlaps(first.X, first.Y, blocker)));
    }

    [Fact]
    public void Safe_spawn_skips_a_blocker_covering_the_first_candidate()
    {
        Assert.True(SpawnRules.TryFindSafePosition(7, [], out var firstCandidate));
        var blockers = new[] { new SpawnBlocker(firstCandidate.X, firstCandidate.Y, 1f) };

        Assert.True(SpawnRules.TryFindSafePosition(7, blockers, out var point));
        Assert.False(SpawnRules.Overlaps(point.X, point.Y, blockers[0]));
        Assert.True(WorldRules.IsInsideMap(point.X, point.Y));
        Assert.NotEqual((firstCandidate.X, firstCandidate.Y), (point.X, point.Y));
    }

    [Fact]
    public void Safe_spawn_gives_up_when_every_attempt_is_blocked()
    {
        // One blocker wider than the map leaves no candidate free for any of the attempts.
        var blockers = new[] { new SpawnBlocker(0f, 0f, 400f) };

        Assert.False(SpawnRules.TryFindSafePosition(7, blockers, out var point));
        Assert.Equal(0f, point.X);
        Assert.Equal(0f, point.Y);
    }

    [Fact]
    public void Safe_spawn_without_blockers_takes_the_first_candidate()
    {
        Assert.True(SpawnRules.TryFindSafePosition(7, [], out var point));
        Assert.True(WorldRules.IsInsideMap(point.X, point.Y));
    }

    [Fact]
    public void Safe_spawn_rejects_a_null_blocker_list()
    {
        Assert.Throws<ArgumentNullException>(
            () => SpawnRules.TryFindSafePosition(7, null!, out _));
    }

    [Fact]
    public void Wind_changes_deterministically_by_epoch()
    {
        var first = EnvironmentRules.WindForEpoch(8675309, 4);
        var repeated = EnvironmentRules.WindForEpoch(8675309, 4);
        var next = EnvironmentRules.WindForEpoch(8675309, 5);

        Assert.Equal(first.DirectionDegrees, repeated.DirectionDegrees);
        Assert.Equal(first.Strength, repeated.Strength);
        Assert.NotEqual(first.DirectionDegrees, next.DirectionDegrees);
        Assert.InRange(first.Strength, 0.2f, 0.8f);
    }

    [Fact]
    public void Tailwind_is_faster_than_headwind()
    {
        var tailwind = EnvironmentRules.WindSpeedMultiplier(45f, 45f, 0.8f);
        var headwind = EnvironmentRules.WindSpeedMultiplier(225f, 45f, 0.8f);

        Assert.True(tailwind > 1f);
        Assert.True(headwind < 1f);
    }

    [Theory]
    [InlineData(3f, 0f, 14.5f, 0f)]
    [InlineData(0f, 0f, 14.5f, 0f)]
    [InlineData(-30f, 0f, -30f, 0f)]
    public void Nearest_clear_point_leaves_a_blocker_on_its_near_side(
        float x,
        float y,
        float expectedX,
        float expectedY)
    {
        var blockers = new[] { new NavigationBlocker(0f, 0f, 10f) };

        var point = NavigationRules.NearestClearPoint(x, y, blockers);

        Assert.Equal(expectedX, point.X, 3);
        Assert.Equal(expectedY, point.Y, 3);
        Assert.False(NavigationRules.IsDestinationBlocked(point.X, point.Y, blockers));
    }

    [Fact]
    public void Nearest_clear_point_escapes_overlapping_blockers()
    {
        var blockers = new[]
        {
            new NavigationBlocker(0f, 0f, 10f),
            new NavigationBlocker(16f, 0f, 10f),
        };

        var point = NavigationRules.NearestClearPoint(2f, 0f, blockers);

        Assert.False(NavigationRules.IsDestinationBlocked(point.X, point.Y, blockers));
    }


    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(0UL)]
    public void Berths_are_sampled_inside_the_anchor_waters_and_clear_of_blockers(ulong seed)
    {
        var quay = new SpawnBlocker(0f, 0f, 8f);
        var shoal = new SpawnBlocker(-4f, -42f, 15f);
        SpawnBlocker[] blockers = [quay, shoal];

        Assert.True(SpawnRules.TryFindSafePositionNear(seed, 0f, 0f, 30f, blockers, out var berth));
        Assert.True(SpawnRules.TryFindSafePositionNear(seed, 0f, 0f, 30f, blockers, out var replay));

        Assert.Equal(berth, replay);
        Assert.InRange(CombatRules.Distance(0f, 0f, berth.X, berth.Y), 0f, 30f);
        Assert.All(blockers, blocker => Assert.False(SpawnRules.Overlaps(berth.X, berth.Y, blocker)));
    }

    [Fact]
    public void Berths_never_leave_the_chart_even_from_an_edge_anchor()
    {
        Assert.True(SpawnRules.TryFindSafePositionNear(
            9,
            WorldRules.MapMax,
            WorldRules.MapMax,
            30f,
            [],
            out var berth));

        Assert.InRange(berth.X, WorldRules.MapMin + SpawnRules.EdgeMargin, WorldRules.MapMax - SpawnRules.EdgeMargin);
        Assert.InRange(berth.Y, WorldRules.MapMin + SpawnRules.EdgeMargin, WorldRules.MapMax - SpawnRules.EdgeMargin);
    }

    [Fact]
    public void Berths_fail_cleanly_when_the_anchor_waters_are_fully_blocked()
    {
        Assert.False(SpawnRules.TryFindSafePositionNear(
            9,
            0f,
            0f,
            10f,
            [new SpawnBlocker(0f, 0f, 10f)],
            out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpawnRules.TryFindSafePositionNear(9, 0f, 0f, 0f, [], out _));
    }
}
