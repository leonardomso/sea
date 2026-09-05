using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class MapEdgeRulesTests
{
    [Theory]
    [InlineData(200f, 3f, MapEdge.North)]
    [InlineData(397f, 200f, MapEdge.East)]
    [InlineData(200f, 397f, MapEdge.South)]
    [InlineData(2f, 200f, MapEdge.West)]
    [InlineData(200f, 200f, MapEdge.None)]
    [InlineData(200f, 7f, MapEdge.None)]
    public void TheOuterSixSquaresAreTheCrossing(float x, float y, MapEdge expected)
    {
        Assert.Equal(expected, MapEdgeRules.EdgeAt(x, y));
    }

    [Fact]
    public void ACornerBelongsToWhicheverEdgeIsNearer()
    {
        // Two squares from the north edge and four from the west: she goes north.
        Assert.Equal(MapEdge.North, MapEdgeRules.EdgeAt(4f, 2f));
        Assert.Equal(MapEdge.West, MapEdgeRules.EdgeAt(2f, 4f));
    }

    [Fact]
    public void ArrivingPutsHerEightSquaresInFromTheOppositeEdge()
    {
        var (x, y) = MapEdgeRules.ArrivalPoint(MapEdge.North, alongAxis: 150f);

        Assert.Equal(150f, x, 4);
        Assert.Equal(WorldRules.MapMax - MapEdgeRules.SpawnInsetSquares, y, 4);
    }

    [Fact]
    public void SheArrivesWhereSheLeftAlongTheEdgeSoACrossingIsNotATeleport()
    {
        var (x, _) = MapEdgeRules.ArrivalPoint(MapEdge.North, alongAxis: 37f);

        Assert.Equal(37f, x, 4);
    }

    // North is -Y, so sailing north off the top of the chart lands her against the
    // bottom of the chart above. Getting this pair the wrong way round is the defect
    // this table exists to catch.
    [Theory]
    [InlineData(MapEdge.North, 150f, 150f, 392f)]
    [InlineData(MapEdge.South, 150f, 150f, 8f)]
    [InlineData(MapEdge.West, 150f, 392f, 150f)]
    [InlineData(MapEdge.East, 150f, 8f, 150f)]
    public void EveryCrossingLandsHerAgainstTheOppositeEdgeOfTheNextChart(
        MapEdge crossed,
        float alongAxis,
        float expectedX,
        float expectedY)
    {
        var (x, y) = MapEdgeRules.ArrivalPoint(crossed, alongAxis);

        Assert.Equal(expectedX, x, 4);
        Assert.Equal(expectedY, y, 4);
    }

    [Fact]
    public void AnArrivalPointIsNeverItselfInsideACrossing()
    {
        Assert.Equal(MapEdge.None, EdgeAtArrival(MapEdge.North));
        Assert.Equal(MapEdge.None, EdgeAtArrival(MapEdge.South));
        Assert.Equal(MapEdge.None, EdgeAtArrival(MapEdge.West));
        Assert.Equal(MapEdge.None, EdgeAtArrival(MapEdge.East));
    }

    [Fact]
    public void OpenWaterIsNotACrossingAndHasNoArrivalPoint()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MapEdgeRules.ArrivalPoint(MapEdge.None, alongAxis: 150f));
    }

    [Fact]
    public void ABorderWithNothingBeyondItHoldsHerJustInsideTheBand()
    {
        var (x, y) = MapEdgeRules.HoldInside(399f, 200f);

        Assert.Equal(WorldRules.MapMax - MapEdgeRules.BandSquares, x, 4);
        Assert.Equal(200f, y, 4);
    }

    [Fact]
    public void TheNearBordersHoldHerJustInsideTheBandToo()
    {
        var (x, y) = MapEdgeRules.HoldInside(0f, 1f);

        Assert.Equal(WorldRules.MapMin + MapEdgeRules.BandSquares, x, 4);
        Assert.Equal(WorldRules.MapMin + MapEdgeRules.BandSquares, y, 4);
    }

    [Fact]
    public void HoldingInsideLeavesAShipInOpenWaterAlone()
    {
        var (x, y) = MapEdgeRules.HoldInside(200f, 200f);

        Assert.Equal(200f, x, 4);
        Assert.Equal(200f, y, 4);
    }

    [Fact]
    public void AHeldHullIsOutOfTheCrossingSoTheBorderNeverFiresAgain()
    {
        var (x, y) = MapEdgeRules.HoldInside(400f, 400f);

        Assert.Equal(MapEdge.None, MapEdgeRules.EdgeAt(x, y));
    }

    private static MapEdge EdgeAtArrival(MapEdge crossed)
    {
        var (x, y) = MapEdgeRules.ArrivalPoint(crossed, alongAxis: 150f);
        return MapEdgeRules.EdgeAt(x, y);
    }
}
