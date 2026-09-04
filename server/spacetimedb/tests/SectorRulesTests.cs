using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SectorRulesTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    private static MapContent Havenmere() => Catalog.Maps[0];

    [Fact]
    public void Sector_id_packs_map_row_and_column()
    {
        Assert.Equal(0x01_000C_000DUL, SectorRules.SectorId(1, 13, 12));
        Assert.Equal(0x01_0000_0000UL, SectorRules.SectorId(1, 0, 0));
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(0.9f, 0.9f, 0, 0)]
    [InlineData(1f, 1f, 1, 1)]
    [InlineData(399.9f, 399.9f, 399, 399)]
    public void AChartSquareIsJustTheWholePartOfAPosition(float x, float y, int column, int row)
    {
        Assert.Equal(column, SectorRules.Column(x));
        Assert.Equal(row, SectorRules.Row(y));
    }

    [Fact]
    public void ThereIsNoConversionLeftToGetWrong()
    {
        var conversions = typeof(SectorRules)
            .GetMembers()
            .Select(member => member.Name)
            .Where(name => name.Contains("Units", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(conversions);
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(19.9f, 19.9f, 19, 19)]
    [InlineData(13.5f, 12.5f, 13, 12)]
    [InlineData(-5f, -5f, 0, 0)]
    [InlineData(25f, 25f, 19, 19)]
    public void World_positions_map_to_their_containing_sector(float x, float y, int column, int row)
    {
        Assert.Equal(new SectorCoordinate(column, row), SectorRules.SectorOf(Havenmere(), x, y));
    }

    [Theory]
    [InlineData(0f, 0f, true)]
    [InlineData(20f, 0f, false)]
    [InlineData(0f, -0.01f, false)]
    [InlineData(19.99f, 19.99f, true)]
    public void Contains_uses_a_half_open_map_extent(float x, float y, bool expected)
    {
        Assert.Equal(expected, SectorRules.Contains(Havenmere(), x, y));
    }

    [Theory]
    [InlineData('.', TerrainCode.Water)]
    [InlineData('~', TerrainCode.Shallow)]
    [InlineData('#', TerrainCode.Land)]
    public void Terrain_symbols_parse(char symbol, TerrainCode expected)
    {
        Assert.True(SectorRules.TryParseTerrain(symbol, out var terrain));
        Assert.Equal(expected, terrain);
    }

    [Fact]
    public void Unknown_terrain_symbol_is_rejected()
    {
        Assert.False(SectorRules.TryParseTerrain('x', out _));
    }

    [Fact]
    public void Havenmere_port_sits_on_water_and_the_first_island_on_land()
    {
        var map = Havenmere();
        Assert.Equal(TerrainCode.Water, SectorRules.TerrainAt(map, 10, 10));
        Assert.Equal(TerrainCode.Land, SectorRules.TerrainAt(map, 13, 12));
    }

    [Fact]
    public void Sector_id_rejects_coordinates_that_would_alias()
    {
        Assert.Throws<OverflowException>(() => SectorRules.SectorId(1, 65536, 0));
        Assert.Throws<OverflowException>(() => SectorRules.SectorId(1, 0, -1));
    }

    [Fact]
    public void Every_sector_of_the_default_map_gets_a_distinct_id()
    {
        var map = Havenmere();
        var ids = new HashSet<ulong>();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.True(ids.Add(SectorRules.SectorId(map.MapId, x, y)));
            }
        }

        Assert.Equal(map.Width * map.Height, ids.Count);
    }

    [Fact]
    public void Every_sector_of_the_default_map_has_a_defined_terrain()
    {
        var map = Havenmere();

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.True(Enum.IsDefined(SectorRules.TerrainAt(map, x, y)));
            }
        }
    }

    [Fact]
    public void Try_sector_of_rejects_positions_outside_the_map()
    {
        Assert.False(SectorRules.TrySectorOf(Havenmere(), 500f, 0f, out _));
        Assert.False(SectorRules.TrySectorOf(Havenmere(), 0f, float.NaN, out _));
        Assert.True(SectorRules.TrySectorOf(Havenmere(), 13.5f, 12.5f, out var sector));
        Assert.Equal(new SectorCoordinate(13, 12), sector);
    }
}
