using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SailingRulesTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(49, "AX")]
    [InlineData(51, "AZ")]
    [InlineData(52, "BA")]
    [InlineData(77, "BZ")]
    public void Column_labels_cover_A_through_BZ(int column, string expected)
    {
        Assert.Equal(expected, ChartCoordinates.ColumnLabel(column));
        Assert.True(ChartCoordinates.TryColumnIndex(expected, out var parsed));
        Assert.Equal(column, parsed);
    }

    [Fact]
    public void AX_59_resolves_to_the_center_of_its_chart_cell()
    {
        Assert.True(ChartCoordinates.TryCellCenter("AX 59", out var center));

        Assert.Equal(49, center.Column);
        Assert.Equal(59, center.Row);
        Assert.Equal("AX 59", ChartCoordinates.LabelAt(center.X, center.Y));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A -1")]
    [InlineData("CA 2")]
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
