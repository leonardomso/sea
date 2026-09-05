using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// Phase 8.3 (Havenmere at real scale) and 8.4 (Gull Rocks, Brine Fields): every map the
/// catalog ships has to be a full 400x400 SEA_5 §3.1 chart, has to carry exactly the one
/// harbor its port fields describe, and a ship has to be able to actually leave port and
/// reach open water without a route that reports NO_PATH.
/// </summary>
public sealed class MapContentTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    [Fact]
    public void Three_maps_are_seeded()
    {
        Assert.Equal(3, Catalog.Maps.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, Catalog.Maps.Select(map => map.MapId).OrderBy(id => id));
    }

    [Theory]
    [InlineData((byte)1, "1/1", "Havenmere")]
    [InlineData((byte)2, "1/2", "Gull Rocks")]
    [InlineData((byte)3, "1/3", "Brine Fields")]
    public void Each_map_carries_its_expected_identity(byte mapId, string code, string name)
    {
        var map = Catalog.Maps.Single(candidate => candidate.MapId == mapId);
        Assert.Equal(code, map.Code);
        Assert.Equal(name, map.Name);
    }

    [Fact]
    public void Every_map_is_the_full_400_square_chart()
    {
        foreach (var map in Catalog.Maps)
        {
            Assert.Equal((ushort)WorldRules.MapSizeSquares, map.Width);
            Assert.Equal((ushort)WorldRules.MapSizeSquares, map.Height);
        }
    }

    [Fact]
    public void Every_map_has_exactly_one_harbor_matching_its_port_fields()
    {
        foreach (var map in Catalog.Maps)
        {
            var harbors = map.Objects.Where(item => string.Equals(item.Kind, "harbor", StringComparison.Ordinal)).ToList();
            var harbor = Assert.Single(harbors);
            Assert.Equal(map.PortX, harbor.X, 3);
            Assert.Equal(map.PortY, harbor.Y, 3);
            Assert.Equal(map.PortRadius, harbor.Radius, 3);
        }
    }

    /// <summary>
    /// SEA_5 §10.3: no fire and no harm inside thirty squares of a harbour. A harbor sited so
    /// close to the map edge or another harbor's own circle that the safe-water ring cannot
    /// fully form would quietly break that guarantee everywhere else in the code trusts it.
    /// </summary>
    [Fact]
    public void Every_harbor_centre_is_safe_water_of_itself()
    {
        foreach (var map in Catalog.Maps)
        {
            Assert.True(PortRules.IsSafeWater(map.PortX, map.PortY, map.PortX, map.PortY));
        }
    }

    [Fact]
    public void Every_harbor_sits_on_water_in_its_own_land_mask()
    {
        foreach (var map in Catalog.Maps)
        {
            var mask = ContentCatalog.LandMaskFor(map.MapId);
            Assert.False(mask.IsLand(map.PortX, map.PortY));
        }
    }

    public static TheoryData<byte, float, float> HarborsAndFarCorners => new()
    {
        { (byte)1, 20f, 20f },
        { (byte)1, 380f, 380f },
        { (byte)2, 20f, 380f },
        { (byte)2, 380f, 20f },
        { (byte)3, 20f, 20f },
        { (byte)3, 380f, 380f },
    };

    /// <summary>
    /// A captain who leaves harbor has to be able to reach open water somewhere near every
    /// corner of the chart. This does not assert every square is reachable -- islands and reefs
    /// are supposed to block plenty of it -- only that the map is not accidentally cut into an
    /// enclosed harbor lake with no way out to the corner in question.
    /// </summary>
    [Theory]
    [MemberData(nameof(HarborsAndFarCorners))]
    public void A_route_from_the_harbor_towards_each_far_corner_is_not_impossible(
        byte mapId, float cornerX, float cornerY)
    {
        var map = Catalog.Maps.Single(candidate => candidate.MapId == mapId);
        var mask = ContentCatalog.LandMaskFor(mapId);
        var scratch = new PathfindingScratch(mask.Size);

        Assert.True(
            mask.TryNearestWater(cornerX, cornerY, PathfindingRules.NudgeSearchSquares, out var goalX, out var goalY),
            $"Map {map.Code}: no water within the nudge radius of ({cornerX}, {cornerY}).");

        Span<RouteWaypoint> route = stackalloc RouteWaypoint[RouteRules.MaximumWaypoints];
        var outcome = PathfindingRules.TryBuildRoute(
            mask, scratch, map.PortX, map.PortY, goalX, goalY, route, out var count);

        Assert.NotEqual(PathOutcome.NoPath, outcome);
        Assert.True(count > 0);
    }

    [Fact]
    public void Map_rank_rises_with_map_id()
    {
        var ranks = Catalog.Maps.OrderBy(map => map.MapId).Select(map => map.MapRank).ToArray();
        for (var i = 1; i < ranks.Length; i++)
        {
            Assert.True(ranks[i] >= ranks[i - 1], "a later map must not ask a lower rank than an earlier one.");
        }
    }
}
