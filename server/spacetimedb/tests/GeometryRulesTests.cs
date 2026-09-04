using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class GeometryRulesTests
{
    [Theory]
    [InlineData(0f, -10f, 0f)]     // straight up the screen is north
    [InlineData(10f, 0f, 90f)]     // to the right is east
    [InlineData(0f, 10f, 180f)]    // down the screen is south
    [InlineData(-10f, 0f, 270f)]   // to the left is west
    public void HeadingToIsACompassBearing(float deltaX, float deltaY, float expected)
    {
        var heading = GeometryRules.HeadingTo(100f, 100f, 100f + deltaX, 100f + deltaY);

        Assert.Equal(expected, heading, 3);
    }

    [Fact]
    public void HeadingToHoldsTheOldBearingWhenThereIsNowhereToGo()
    {
        Assert.Equal(41f, GeometryRules.HeadingTo(5f, 5f, 5f, 5f, 41f), 3);
    }

    [Fact]
    public void DirectionRoundTripsThroughHeading()
    {
        var (x, y) = GeometryRules.Direction(90f);

        Assert.Equal(1f, x, 3);
        Assert.Equal(0f, y, 3);
    }

    [Theory]
    [InlineData(370f, 10f)]
    [InlineData(-10f, 350f)]
    [InlineData(720f, 0f)]
    public void NormalizeAngleLandsInZeroToThreeSixty(float input, float expected)
    {
        Assert.Equal(expected, GeometryRules.NormalizeAngle(input), 3);
    }

    [Theory]
    [InlineData(350f, -10f)]
    [InlineData(190f, -170f)]
    [InlineData(180f, 180f)]
    public void NormalizeSignedAngleLandsInMinusOneEightyToOneEighty(float input, float expected)
    {
        Assert.Equal(expected, GeometryRules.NormalizeSignedAngle(input), 3);
    }

    [Fact]
    public void DistanceIsPlainPythagoras()
    {
        Assert.Equal(5f, GeometryRules.Distance(0f, 0f, 3f, 4f), 4);
        Assert.Equal(25f, GeometryRules.DistanceSquared(0f, 0f, 3f, 4f), 4);
    }

    [Fact]
    public void SegmentIntersectsCircleSeesTheReefOnlyWhenTheCourseCrossesIt()
    {
        Assert.True(GeometryRules.SegmentIntersectsCircle(
            -10f, 0f, 10f, 0f, 0f, 0f, radius: 3f));
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            -10f, 10f, 10f, 10f, 0f, 0f, radius: 3f));
    }

    [Fact]
    public void SegmentIntersectsCircleStopsAtTheEndsOfTheCourse()
    {
        // The circle sits on the line the segment lies along, but past its end.
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            -10f, 0f, -5f, 0f, 0f, 0f, radius: 3f));
    }

    [Fact]
    public void SegmentIntersectsCircleTreatsAShipAtRestAsAPoint()
    {
        Assert.True(GeometryRules.SegmentIntersectsCircle(
            1f, 1f, 1f, 1f, 0f, 0f, radius: 3f));
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            1f, 1f, 1f, 1f, 0f, 0f, radius: 1f));
    }
}
