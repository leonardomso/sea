using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Task 9.3 step 4: the chart a hull that draws too much water is routed on. A shoal slows a
/// sloop and stops a fourth rate, so the two of them cannot plot a course against the same
/// grid -- one has to see the shallows as coast.
/// </summary>
public sealed class DeepDraftMaskTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void AShoalIsWaterToASloopAndCoastToAFourthRate(byte mapId)
    {
        var map = Catalog.Maps.Single(candidate => candidate.MapId == mapId);
        var shallow = Squares(map, TerrainCode.Shallow).ToList();
        Assert.NotEmpty(shallow);

        var open = ContentCatalog.LandMaskFor(mapId);
        var deep = ContentCatalog.DeepDraftMaskFor(mapId);
        foreach (var (x, y) in shallow)
        {
            Assert.False(open.IsLandCell(x, y));
            Assert.True(deep.IsLandCell(x, y));
        }
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void TheDeepDraftChartIsTheSameChartWithTheShallowsFilledIn(byte mapId)
    {
        var map = Catalog.Maps.Single(candidate => candidate.MapId == mapId);
        var open = ContentCatalog.LandMaskFor(mapId);
        var deep = ContentCatalog.DeepDraftMaskFor(mapId);
        Assert.Equal(open.Size, deep.Size);

        // Nothing else moves: every island stays an island, and open water stays open. A mask
        // that quietly closed a channel would strand a hull without anything saying why.
        for (var y = 0; y < deep.Size; y++)
        {
            for (var x = 0; x < deep.Size; x++)
            {
                var terrain = SectorRules.TerrainAt(map, x, y);
                Assert.Equal(terrain == TerrainCode.Land, open.IsLandCell(x, y));
                Assert.Equal(terrain != TerrainCode.Water, deep.IsLandCell(x, y));
            }
        }
    }

    [Fact]
    public void ThereIsNoChartBeyondTheOnesTheCatalogShips()
    {
        Assert.Throws<KeyNotFoundException>(() => ContentCatalog.DeepDraftMaskFor(9));
    }

    /// <summary>
    /// A hull that can cross a shoal is routed on the open chart; one that cannot is routed on
    /// the deep-draft one. This is the pairing SetCourse makes, kept here because the reducer
    /// itself needs a live database to call.
    /// </summary>
    [Theory]
    [InlineData((byte)1, true)]
    [InlineData((byte)3, true)]
    [InlineData((byte)4, false)]
    [InlineData((byte)5, false)]
    public void TheTierPicksTheChart(byte tier, bool crossesShoals)
    {
        Assert.Equal(crossesShoals, PortRules.CanCrossShoal(tier));
        Assert.Same(
            crossesShoals ? ContentCatalog.LandMaskFor(1) : ContentCatalog.DeepDraftMaskFor(1),
            ContentCatalog.NavigableMaskFor(1, tier));
    }

    /// <summary>
    /// A current can carry a fourth rate into shallow water and a crossing can put her out in
    /// it, and the search refuses a course that starts on what its chart calls coast. Routed on
    /// the deep-draft chart from there she would be refused every order she gave for ever, so a
    /// hull already in water she should not be in is routed on the open chart until she is out
    /// of it: she may leave the shallows, she is never sent into them.
    /// </summary>
    [Fact]
    public void AHullAlreadyInTheShallowsIsRoutedOutOfThemRatherThanStranded()
    {
        var map = Catalog.Maps.Single(candidate => candidate.MapId == 1);
        var (shoalX, shoalY) = Squares(map, TerrainCode.Shallow).First();
        var (openX, openY) = Squares(map, TerrainCode.Water).First();

        Assert.Same(
            ContentCatalog.LandMaskFor(1),
            ContentCatalog.RoutingMaskFor(1, tier: 5, shoalX + 0.5f, shoalY + 0.5f));
        Assert.Same(
            ContentCatalog.DeepDraftMaskFor(1),
            ContentCatalog.RoutingMaskFor(1, tier: 5, openX + 0.5f, openY + 0.5f));

        // A hull that crosses shoals reads the same chart wherever she is standing.
        Assert.Same(
            ContentCatalog.LandMaskFor(1),
            ContentCatalog.RoutingMaskFor(1, tier: 1, shoalX + 0.5f, shoalY + 0.5f));
    }

    private static IEnumerable<(int X, int Y)> Squares(MapContent map, TerrainCode terrain)
    {
        for (var y = 0; y < map.TerrainRows.Count; y++)
        {
            for (var x = 0; x < map.TerrainRows[y].Length; x++)
            {
                if (SectorRules.TerrainAt(map, x, y) == terrain)
                {
                    yield return (x, y);
                }
            }
        }
    }
}
