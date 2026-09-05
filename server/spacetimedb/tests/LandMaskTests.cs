using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class LandMaskTests
{
    /// <summary>A 10 x 10 sea with a 2 x 2 island at cells (4,4)..(5,5).</summary>
    private static LandMask SmallSea()
    {
        var bits = new ulong[LandMask.WordCount(10)];
        var mask = new LandMask(10, bits);
        foreach (var cellY in new[] { 4, 5 })
        {
            foreach (var cellX in new[] { 4, 5 })
            {
                var index = (cellY * 10) + cellX;
                bits[index >> 6] |= 1UL << (index & 63);
            }
        }

        return mask;
    }

    [Fact]
    public void WaterIsWaterAndLandIsLand()
    {
        var mask = SmallSea();

        Assert.False(mask.IsLand(0.5f, 0.5f));
        Assert.True(mask.IsLand(4.5f, 4.5f));
        Assert.True(mask.IsLand(5.9f, 5.9f));
        Assert.False(mask.IsLand(6.1f, 6.1f));
    }

    [Fact]
    public void OutsideTheMapCountsAsLand()
    {
        var mask = SmallSea();

        Assert.True(mask.IsLand(-0.5f, 5f));
        Assert.True(mask.IsLand(5f, 10.5f));
    }

    /// <summary>
    /// Off the map is land on every side and however far out, which is the whole
    /// reason nothing downstream carries its own map-edge check: A* cannot route
    /// off the chart and drift cannot push a hull over the border.
    /// </summary>
    [Fact]
    public void EveryEdgeAndEverythingBeyondItCountsAsLand()
    {
        var mask = SmallSea();

        Assert.True(mask.IsLandCell(-1, 5));
        Assert.True(mask.IsLandCell(10, 5));
        Assert.True(mask.IsLandCell(5, -1));
        Assert.True(mask.IsLandCell(5, 10));

        Assert.True(mask.IsLand(-0.5f, 5.5f));
        Assert.True(mask.IsLand(10.5f, 5.5f));
        Assert.True(mask.IsLand(5.5f, -0.5f));
        Assert.True(mask.IsLand(5.5f, 10.5f));

        Assert.True(mask.IsLand(10_000f, 5.5f));
        Assert.True(mask.IsLand(-10_000f, 5.5f));
        Assert.True(mask.IsLand(5.5f, 10_000f));
        Assert.True(mask.IsLand(5.5f, -10_000f));
        Assert.True(mask.IsLand(-10_000f, -10_000f));
    }

    [Fact]
    public void ASegmentClearOfTheIslandIsClear()
    {
        Assert.True(SmallSea().SegmentIsClear(0.5f, 0.5f, 9.5f, 0.5f));
    }

    [Fact]
    public void ASegmentThroughTheIslandIsNot()
    {
        Assert.False(SmallSea().SegmentIsClear(0.5f, 4.5f, 9.5f, 4.5f));
    }

    [Fact]
    public void ASegmentThatOnlyClipsTheIslandDiagonallyIsNot()
    {
        Assert.False(SmallSea().SegmentIsClear(0.5f, 0.5f, 9.5f, 9.5f));
    }

    /// <summary>A course off the chart is blocked by the same rule, on every side.</summary>
    [Fact]
    public void ASegmentThatLeavesTheMapIsNot()
    {
        var mask = SmallSea();

        Assert.False(mask.SegmentIsClear(0.5f, 0.5f, -5f, 0.5f));
        Assert.False(mask.SegmentIsClear(9.5f, 0.5f, 15f, 0.5f));
        Assert.False(mask.SegmentIsClear(0.5f, 0.5f, 0.5f, -5f));
        Assert.False(mask.SegmentIsClear(0.5f, 9.5f, 0.5f, 15f));
        Assert.False(mask.SegmentIsClear(-5f, 0.5f, 0.5f, 0.5f));
    }

    [Fact]
    public void NearestWaterLeavesAPointThatIsAlreadyWaterAlone()
    {
        Assert.True(SmallSea().TryNearestWater(2f, 2f, 3f, out var x, out var y));
        Assert.Equal(2f, x, 4);
        Assert.Equal(2f, y, 4);
    }

    [Fact]
    public void NearestWaterMovesAPointOffTheIsland()
    {
        Assert.True(SmallSea().TryNearestWater(4.5f, 4.5f, 3f, out var x, out var y));

        Assert.False(SmallSea().IsLand(x, y));
        Assert.True(GeometryRules.Distance(4.5f, 4.5f, x, y) <= 3f);
    }

    [Fact]
    public void NearestWaterGivesUpWhenTheSearchIsTooSmall()
    {
        var bits = new ulong[LandMask.WordCount(4)];
        for (var index = 0; index < bits.Length; index++)
        {
            bits[index] = ulong.MaxValue;
        }

        Assert.False(new LandMask(4, bits).TryNearestWater(2f, 2f, 3f, out _, out _));
    }

    /// <summary>
    /// Nearest water never answers with a square off the chart: a hull nudged out
    /// of an island on the border has to come back inside the sea.
    /// </summary>
    [Fact]
    public void NearestWaterStaysOnTheMap()
    {
        var bits = new ulong[LandMask.WordCount(10)];
        var mask = new LandMask(10, bits);
        var index = (0 * 10) + 0;
        bits[index >> 6] |= 1UL << (index & 63);

        Assert.True(mask.TryNearestWater(0.5f, 0.5f, 3f, out var x, out var y));

        Assert.InRange(x, 0f, 10f);
        Assert.InRange(y, 0f, 10f);
        Assert.False(mask.IsLand(x, y));
    }

    [Fact]
    public void AMaskRejectsAWordArrayThatIsTheWrongSize()
    {
        Assert.Throws<ArgumentException>(() => new LandMask(10, new ulong[1]));
    }

    [Fact]
    public void AMaskRejectsANonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LandMask(0, []));
    }

    [Fact]
    public void ASegmentThatStopsOnACellCornerDoesNotWalkOnPastIt()
    {
        // The end point is the exact corner of four cells and the ray arrives at
        // it diagonally, so both cell boundaries fall due at the same distance. A
        // walk that only stopped once it stood on the end cell would step over
        // that corner, miss the cell entirely and march off the map into the land
        // that surrounds it.
        Assert.True(SmallSea().SegmentIsClear(1.5f, 3.5f, 2f, 3f));
    }

    [Fact]
    public void AHullCannotSlipBetweenTwoRocksThatMeetAtAPoint()
    {
        var bits = new ulong[LandMask.WordCount(10)];
        var mask = new LandMask(10, bits);
        var open = mask.SegmentIsClear(3.5f, 3.5f, 4.5f, 4.5f);

        var first = (3 * 10) + 4;
        var second = (4 * 10) + 3;
        bits[first >> 6] |= 1UL << (first & 63);
        bits[second >> 6] |= 1UL << (second & 63);

        Assert.True(open);
        Assert.False(mask.SegmentIsClear(3.5f, 3.5f, 4.5f, 4.5f));
    }
}
