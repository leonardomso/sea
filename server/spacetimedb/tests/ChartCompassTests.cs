using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// One compass for the whole simulation: 0 is north, north is up the chart, and up the
/// chart is the smaller y. Propulsion, drift and gunnery all have to answer to it on the
/// same tick, and until this file existed nothing held them to each other.
/// </summary>
public sealed class ChartCompassTests
{
    /// <summary>
    /// Way made good under sail, on the same compass as everything else on the tick. The
    /// integrator used to propel a hull by an unnegated cosine, so heading 0 carried her
    /// south while the current on the same bearing set her north, and the two were added
    /// together in <c>SailingSystem.ApplySailingStep</c>.
    /// </summary>
    [Theory]
    [InlineData(0f, 0f, -1f)]
    [InlineData(90f, 1f, 0f)]
    [InlineData(180f, 0f, 1f)]
    [InlineData(270f, -1f, 0f)]
    public void A_hull_makes_way_the_direction_her_heading_says(
        float headingDegrees,
        float expectedX,
        float expectedY)
    {
        var step = SailingRules.StepTowardHeading(
            new SailingState(0f, 0f, headingDegrees, 1f),
            destinationX: 1000f,
            destinationY: 1000f,
            desiredHeadingDegrees: headingDegrees,
            stopping: false,
            new SailingParameters(1f, 0f, 0f, 0f),
            deltaSeconds: 1f);

        Assert.Equal(expectedX, step.PositionX, 3);
        Assert.Equal(expectedY, step.PositionY, 3);
    }

    /// <summary>
    /// Propulsion and drift read the same compass. A hull sailing a bearing and a current
    /// setting that same bearing must push the same way, or a tick adds one to its opposite.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(37f)]
    [InlineData(145f)]
    [InlineData(292f)]
    public void Sail_and_current_on_one_bearing_push_the_same_way(float bearingDegrees)
    {
        var (currentX, currentY) = EnvironmentRules.DirectionalVelocity(bearingDegrees, 1f);
        var step = SailingRules.StepTowardHeading(
            new SailingState(0f, 0f, bearingDegrees, 1f),
            destinationX: currentX * 1000f,
            destinationY: currentY * 1000f,
            desiredHeadingDegrees: bearingDegrees,
            stopping: false,
            new SailingParameters(1f, 0f, 0f, 0f),
            deltaSeconds: 1f);

        Assert.Equal(currentX, step.PositionX, 3);
        Assert.Equal(currentY, step.PositionY, 3);
    }

    /// <summary>
    /// The bearing she steers is the inverse of the way she makes: steering the answer to
    /// <see cref="SailingRules.DesiredHeading"/> has to close the distance to the mark.
    /// Both halves used to be wrong together, which is why nothing went red.
    /// </summary>
    [Theory]
    [InlineData(0f, -10f)]
    [InlineData(10f, 0f)]
    [InlineData(0f, 10f)]
    [InlineData(-10f, 0f)]
    [InlineData(7f, -3f)]
    public void The_bearing_she_steers_is_the_way_she_sails(
        float destinationX,
        float destinationY)
    {
        var heading = SailingRules.DesiredHeading(0f, 0f, destinationX, destinationY);

        var (directionX, directionY) = GeometryRules.Direction(heading);

        // Compared as a bearing, not as a position. DesiredHeading answers off MathF.Atan2,
        // which is exact, but Direction reads a table sampled every quarter degree, so the way
        // she sails can sit up to half a step -- 0.125 degrees -- off the bearing she steers.
        // Multiplying that back out to a position and asserting decimal places would fail any
        // mark that does not happen to lie on the grid, which says something about the table
        // and nothing about whether the two halves agree.
        var sailed = GeometryRules.HeadingTo(
            0f, 0f, directionX, directionY, fallbackHeadingDegrees: heading);
        var error = GeometryRules.NormalizeSignedAngle(sailed - heading);

        Assert.True(
            MathF.Abs(error) <= 0.125f,
            $"steering {heading} sails {sailed}, which is {error} degrees off her mark");
    }
}
