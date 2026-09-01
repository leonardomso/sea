using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SailingRulesTests
{
    [Theory]
    [InlineData(0, "AA")]
    [InlineData(23, "AX")]
    [InlineData(25, "AZ")]
    [InlineData(26, "BA")]
    [InlineData(51, "BZ")]
    [InlineData(52, "CA")]
    [InlineData(77, "CZ")]
    public void Y_axis_labels_start_at_AA_and_continue_after_AZ(int column, string expected)
    {
        Assert.Equal(expected, ChartCoordinates.ColumnLabel(column));
        Assert.True(ChartCoordinates.TryColumnIndex(expected, out var parsed));
        Assert.Equal(column, parsed);
    }

    [Fact]
    public void AX_59_resolves_to_the_center_of_its_chart_cell()
    {
        Assert.True(ChartCoordinates.TryCellCenter("AX 59", out var center));

        Assert.Equal(23, center.Column);
        Assert.Equal(59, center.Row);
        Assert.Equal("AX 59", ChartCoordinates.LabelAt(center.X, center.Y));
    }

    [Fact]
    public void Chart_axes_run_AA_to_CZ_top_to_bottom_and_zero_to_sixty_left_to_right()
    {
        Assert.Equal("AA 0", ChartCoordinates.LabelAt(-99.9f, 99.9f));
        Assert.Equal("CZ 60", ChartCoordinates.LabelAt(99.9f, -99.9f));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A -1")]
    [InlineData("DA 2")]
    [InlineData("AX 61")]
    [InlineData("59 AX")]
    public void Invalid_chart_coordinates_are_rejected(string value)
    {
        Assert.False(ChartCoordinates.TryCellCenter(value, out _));
    }

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
                    WorldRules.PlayerShipTurnRateDegrees),
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

        Assert.True(arrived);
        Assert.InRange(state.PositionX, destinationX - 0.01f, destinationX + 0.01f);
        Assert.InRange(state.PositionY, destinationY - 0.01f, destinationY + 0.01f);
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
}
