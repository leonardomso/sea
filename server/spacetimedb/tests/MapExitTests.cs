using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Which chart lies beyond each border (SEA_5 §10.2). The three maps are a chain rather than a
/// grid: Havenmere in the west, then Gull Rocks, then the Brine Fields. Sailing north or south
/// off any of them leads nowhere, which is what makes the chain a chain.
/// </summary>
public sealed class MapExitTests
{
    [Theory]
    [InlineData(1, MapEdge.East, 2)]
    [InlineData(2, MapEdge.West, 1)]
    [InlineData(2, MapEdge.East, 3)]
    [InlineData(3, MapEdge.West, 2)]
    public void TheChartsAreAChainFromHavenmereOutwards(byte mapId, MapEdge edge, byte toMapId)
    {
        Assert.Equal(toMapId, ContentCatalog.ExitFor(mapId, edge));
    }

    [Theory]
    [InlineData(1, MapEdge.North)]
    [InlineData(1, MapEdge.South)]
    [InlineData(1, MapEdge.West)]
    [InlineData(2, MapEdge.North)]
    [InlineData(2, MapEdge.South)]
    [InlineData(3, MapEdge.North)]
    [InlineData(3, MapEdge.South)]
    [InlineData(3, MapEdge.East)]
    public void ABorderWithNothingBeyondItAnswersNothing(byte mapId, MapEdge edge)
    {
        Assert.Null(ContentCatalog.ExitFor(mapId, edge));
    }

    /// <summary>
    /// Open water is not a border, so it has no exit -- and asking for one is a caller's mistake
    /// rather than an answer of "nowhere", which would read the same as standing on a dead coast.
    /// </summary>
    [Fact]
    public void OpenWaterIsNotABorderAndHasNoExit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContentCatalog.ExitFor(1, MapEdge.None));
    }

    /// <summary>
    /// Every crossing can be sailed back the way it came. A one-way border would strand a captain
    /// on a chart she cannot leave, and nothing else in the game would tell her so beforehand.
    /// </summary>
    [Fact]
    public void EveryCrossingHasOneComingBack()
    {
        foreach (var map in ContentCatalog.CreateDefault().Maps)
        {
            foreach (var edge in new[] { MapEdge.North, MapEdge.East, MapEdge.South, MapEdge.West })
            {
                if (ContentCatalog.ExitFor(map.MapId, edge) is not byte neighbour)
                {
                    continue;
                }

                Assert.Equal(map.MapId, ContentCatalog.ExitFor(neighbour, MapEdgeRules.Opposite(edge)));
            }
        }
    }
}
