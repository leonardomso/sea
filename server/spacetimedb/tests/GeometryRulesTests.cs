using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public readonly record struct GeneratedAngle(float Degrees);
public readonly record struct GeneratedBearing(float Degrees);

public static class GeometryArbitraries
{
    public static Arbitrary<GeneratedAngle> Angles() => Arb.From(
        from thousandths in Gen.Choose(-4_000_000, 4_000_000)
        select new GeneratedAngle(thousandths / 1_000f));

    // Bearings land on the quarter degree TrigonometryRules samples at. Off the grid the sine
    // and the cosine can come from samples a quarter degree apart, which stretches the vector by
    // up to 0.002; on the grid they always agree, so a unit length is a real contract there.
    public static Arbitrary<GeneratedBearing> Bearings() => Arb.From(
        from quarters in Gen.Choose(-1_440, 2_879)
        select new GeneratedBearing(quarters * 0.25f));
}

public sealed class GeometryRulesTests
{
    [Theory]
    [InlineData(0f, -10f, 0f)]     // straight up the screen is north
    [InlineData(10f, 0f, 90f)]     // to the right is east
    [InlineData(0f, 10f, 180f)]    // down the screen is south
    [InlineData(-10f, 0f, 270f)]   // to the left is west
    public void HeadingToIsACompassBearing(float deltaX, float deltaY, float expected)
    {
        var heading = GeometryRules.HeadingTo(
            100f, 100f, 100f + deltaX, 100f + deltaY, fallbackHeadingDegrees: 0f);

        Assert.Equal(expected, heading, 3);
    }

    [Fact]
    public void HeadingToHoldsTheOldBearingWhenThereIsNowhereToGo()
    {
        Assert.Equal(41f, GeometryRules.HeadingTo(5f, 5f, 5f, 5f, 41f), 3);
    }

    [Fact]
    public void HeadingToKeepsTheFallbackOnlyForACourseShorterThanAThousandthOfASquare()
    {
        // The cut-off is a squared distance of 0.000001, so a thousandth of a square.
        Assert.Equal(41f, GeometryRules.HeadingTo(5f, 5f, 5.0009f, 5f, 41f), 3);
        Assert.Equal(90f, GeometryRules.HeadingTo(5f, 5f, 5.0011f, 5f, 41f), 3);
    }

    [Theory]
    [InlineData(0f, 0f, -1f)]      // north is up the screen
    [InlineData(90f, 1f, 0f)]      // east is to the right
    [InlineData(180f, 0f, 1f)]     // south is down the screen
    [InlineData(270f, -1f, 0f)]    // west is to the left
    public void DirectionPointsWhereTheBearingSays(
        float headingDegrees,
        float expectedX,
        float expectedY)
    {
        var (x, y) = GeometryRules.Direction(headingDegrees);

        Assert.Equal(expectedX, x, 3);
        Assert.Equal(expectedY, y, 3);
    }

    // This is a consistency check, not an orientation one: atan2(sin h, cos h) recovers h under
    // the old south-pointing convention too, as long as both halves share it. What it does catch
    // is a half-migrated pair, where one of the two is flipped and the other is not, which is the
    // likeliest bug in the phases that follow. DirectionPointsWhereTheBearingSays above is what
    // pins the sign, so do not delete that on the grounds that this covers it.
    //
    // The two are inverses, but only to the sharpness of the table Direction reads. It samples
    // every quarter degree and rounds to the nearest, so a bearing off that grid comes back up to
    // half a step -- 0.125 degrees -- from where it went in. Asserting on decimal places instead
    // would fail on a bearing like 210.25 that sits exactly on a rounding boundary, which says
    // something about xUnit's rounding and nothing about the geometry.
    [Theory]
    [InlineData(30f)]
    [InlineData(45f)]
    [InlineData(137.3f)]     // off the quarter degree grid
    [InlineData(137.5f)]
    [InlineData(210.25f)]
    [InlineData(300.75f)]
    [InlineData(359.9f)]     // just short of the wrap, where HeadingTo answers just past 0
    public void DirectionAndHeadingToAgreeOffTheAxes(float headingDegrees)
    {
        var (x, y) = GeometryRules.Direction(headingDegrees);
        var recovered = GeometryRules.HeadingTo(
            0f, 0f, x * 10f, y * 10f, fallbackHeadingDegrees: 0f);

        // Signed, not a plain subtraction: HeadingTo answers in [0, 360), so a bearing just
        // short of north comes back just past it and a subtraction would call that 359.9 out.
        var error = GeometryRules.NormalizeSignedAngle(recovered - headingDegrees);

        Assert.True(
            MathF.Abs(error) <= 0.125f,
            $"expected a bearing within a quarter degree step of {headingDegrees}, got {recovered}");
    }

    [Fact]
    public void DirectionNeverHandsBackANegativeZero()
    {
        // Due west reads a sine table entry of exactly +0 for its Y, and negating that would give
        // -0. Nothing hashes a direction vector today; this holds the door shut before it does.
        foreach (var headingDegrees in new[] { 0f, 90f, 180f, 270f, 360f })
        {
            var (x, y) = GeometryRules.Direction(headingDegrees);

            Assert.NotEqual(BitConverter.SingleToUInt32Bits(-0f), BitConverter.SingleToUInt32Bits(x));
            Assert.NotEqual(BitConverter.SingleToUInt32Bits(-0f), BitConverter.SingleToUInt32Bits(y));
        }
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
    [InlineData(-0f)]
    [InlineData(-360f)]
    [InlineData(-0.000001f)]
    public void NormalizeAngleGivesTheSameZeroEveryTime(float input)
    {
        // The replay hash reads heading through SingleToUInt32Bits, so -0f and +0f are different
        // worlds to it, and Assert.Equal(0f, -0f, 3) would not notice.
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(0f),
            BitConverter.SingleToUInt32Bits(GeometryRules.NormalizeAngle(input)));
    }

    [Theory]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(float.Epsilon)]
    [InlineData(-float.Epsilon)]
    [InlineData(1e30f)]
    [InlineData(-1e30f)]
    public void NormalizeAngleHoldsItsRangeAtTheEdgesOfTheType(float input)
    {
        var normalized = GeometryRules.NormalizeAngle(input);

        Assert.True(
            normalized >= 0f && normalized < 360f,
            $"expected a bearing in [0, 360), got {normalized}");
    }

    [Theory]
    [InlineData(350f, -10f)]
    [InlineData(190f, -170f)]
    [InlineData(180f, 180f)]
    [InlineData(-180f, 180f)]   // and NOT -180: CombatRules rounds instead and answers -180 here
    [InlineData(0f, 0f)]
    [InlineData(360f, 0f)]
    [InlineData(-360f, 0f)]
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
    public void SegmentIntersectsCircleStopsAtBothEndsOfTheCourse()
    {
        // The reef sits on the line the course lies along, but past its end.
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            -10f, 0f, -5f, 0f, 0f, 0f, radius: 3f));

        // And here it sits behind the start.
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            5f, 0f, 10f, 0f, 0f, 0f, radius: 3f));
    }

    [Fact]
    public void SegmentIntersectsCircleCountsATouchAsAMiss()
    {
        // The course passes exactly 3 squares from the centre of a 3 square reef.
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            -10f, 0f, 10f, 0f, 0f, 3f, radius: 3f));
        Assert.True(GeometryRules.SegmentIntersectsCircle(
            -10f, 0f, 10f, 0f, 0f, 3f, radius: 3.01f));
    }

    [Fact]
    public void SegmentIntersectsCircleTreatsAShipAtRestAsAPoint()
    {
        Assert.True(GeometryRules.SegmentIntersectsCircle(
            1f, 1f, 1f, 1f, 0f, 0f, radius: 3f));
        Assert.False(GeometryRules.SegmentIntersectsCircle(
            1f, 1f, 1f, 1f, 0f, 0f, radius: 1f));
    }

    [Property(MaxTest = 250, Arbitrary = new[] { typeof(GeometryArbitraries) })]
    public bool NormalizeAngleAlwaysLandsInZeroToThreeSixty(GeneratedAngle angle)
    {
        var normalized = GeometryRules.NormalizeAngle(angle.Degrees);
        return normalized >= 0f && normalized < 360f &&
            BitConverter.SingleToUInt32Bits(normalized) != BitConverter.SingleToUInt32Bits(-0f);
    }

    [Property(MaxTest = 250, Arbitrary = new[] { typeof(GeometryArbitraries) })]
    public bool DirectionIsAlwaysAUnitVector(GeneratedBearing bearing)
    {
        var (x, y) = GeometryRules.Direction(bearing.Degrees);
        return MathF.Abs(MathF.Sqrt((x * x) + (y * y)) - 1f) <= 1e-6f;
    }
}
