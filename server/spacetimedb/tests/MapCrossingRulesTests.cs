using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class MapCrossingRulesTests
{
    [Fact]
    public void TheEastBorderOfHavenmereOffersTheNextChartAlong()
    {
        var offer = MapCrossingRules.Offer(mapId: 1, MapEdge.East, heldX: 394f, heldY: 200f);

        Assert.NotNull(offer);
        Assert.Equal(2, offer!.Value.ToMapId);
        Assert.Equal(MapEdge.East, offer.Value.Edge);
        Assert.Equal(WorldRules.MapMin + MapEdgeRules.SpawnInsetSquares, offer.Value.SpawnX, 4);
    }

    [Fact]
    public void SheComesOutWhereSheWentIn()
    {
        // Crossing east is a step to the right, so her place up and down the border is the
        // one thing she keeps: 137 squares down the east edge, 137 squares down the west one.
        var offer = MapCrossingRules.Offer(1, MapEdge.East, 394f, 137f);

        Assert.Equal(137f, offer!.Value.SpawnY, 4);
    }

    [Fact]
    public void CrossingNorthCarriesHerPlaceAlongTheTopEdgeInstead()
    {
        var offer = MapCrossingRules.Offer(2, MapEdge.West, 6f, 40f);

        Assert.Equal(1, offer!.Value.ToMapId);
        Assert.Equal(WorldRules.MapMax - MapEdgeRules.SpawnInsetSquares, offer.Value.SpawnX, 4);
        Assert.Equal(40f, offer.Value.SpawnY, 4);
    }

    [Theory]
    [InlineData((byte)1, MapEdge.North)]
    [InlineData((byte)1, MapEdge.South)]
    [InlineData((byte)1, MapEdge.West)]
    [InlineData((byte)3, MapEdge.East)]
    public void ABorderWithNothingBeyondItOnlyHoldsHer(byte mapId, MapEdge edge)
    {
        Assert.Null(MapCrossingRules.Offer(mapId, edge, 200f, 6f));
    }

    [Fact]
    public void OpenWaterIsNotABorderAndAsksNothing()
    {
        Assert.Null(MapCrossingRules.Offer(1, MapEdge.None, 200f, 200f));
    }

    [Fact]
    public void AHullOnTheHoldLineIsStillAgainstTheBorderSheWasStoppedBy()
    {
        var (x, y) = MapEdgeRules.HoldInside(399f, 200f);

        Assert.True(MapEdgeRules.IsHeldAgainst(x, y, MapEdge.East));
        Assert.False(MapEdgeRules.IsHeldAgainst(x - 0.5f, y, MapEdge.East));
        Assert.False(MapEdgeRules.IsHeldAgainst(x, y, MapEdge.None));
    }

    [Fact]
    public void EachBorderHasItsOwnHoldLine()
    {
        Assert.True(MapEdgeRules.IsHeldAgainst(200f, MapEdgeRules.BandSquares, MapEdge.North));
        Assert.True(MapEdgeRules.IsHeldAgainst(
            200f, WorldRules.MapMax - MapEdgeRules.BandSquares, MapEdge.South));
        Assert.True(MapEdgeRules.IsHeldAgainst(MapEdgeRules.BandSquares, 200f, MapEdge.West));
        Assert.False(MapEdgeRules.IsHeldAgainst(200f, MapEdgeRules.BandSquares, MapEdge.South));
    }
}
