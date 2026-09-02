namespace Sea.Server;

public readonly record struct SectorCoordinate(int X, int Y);

public static class SectorRules
{
    public const float SquareSizeUnits = 10f;

    public static ulong SectorId(byte mapId, SectorCoordinate sector) =>
        ((ulong)mapId << 16) | ((ulong)checked((byte)sector.Y) << 8) | checked((byte)sector.X);

    public static ulong SectorId(byte mapId, int x, int y) => SectorId(mapId, new SectorCoordinate(x, y));

    public static float OriginX(MapContent map) => -map.Width * SquareSizeUnits / 2f;

    public static float OriginY(MapContent map) => -map.Height * SquareSizeUnits / 2f;

    /// <summary>
    /// Half-open sector extent: the far edge belongs to the next map, unlike
    /// <see cref="WorldRules.IsInsideMap"/>, which is closed on both edges.
    /// </summary>
    public static bool Contains(MapContent map, float worldX, float worldY) =>
        TrySectorOf(map, worldX, worldY, out _);

    public static bool TrySectorOf(MapContent map, float worldX, float worldY, out SectorCoordinate sector)
    {
        sector = default;
        if (map.Width == 0 || map.Height == 0 || !float.IsFinite(worldX) || !float.IsFinite(worldY))
        {
            return false;
        }

        var x = Column(map, worldX);
        var y = Row(map, worldY);
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
        {
            return false;
        }

        sector = new SectorCoordinate(x, y);
        return true;
    }

    /// <summary>
    /// Clamps positions that fall outside the map onto the nearest edge sector; prefer
    /// <see cref="TrySectorOf"/> whenever the position may be off-map.
    /// </summary>
    public static SectorCoordinate SectorOf(MapContent map, float worldX, float worldY)
    {
        if (TrySectorOf(map, worldX, worldY, out var sector))
        {
            return sector;
        }

        if (map.Width == 0 || map.Height == 0)
        {
            return default;
        }

        return new SectorCoordinate(
            Math.Clamp(Column(map, worldX), 0, map.Width - 1),
            Math.Clamp(Row(map, worldY), 0, map.Height - 1));
    }

    public static bool TryParseTerrain(char symbol, out TerrainCode terrain) =>
        HotPathCodes.TryParseTerrain(symbol, out terrain);

    public static TerrainCode TerrainAt(MapContent map, int x, int y)
    {
        var symbol = map.TerrainRows[y][x];
        return HotPathCodes.TryParseTerrain(symbol, out var terrain)
            ? terrain
            : throw new InvalidOperationException(
                $"Unknown terrain symbol '{symbol}' at ({x}, {y}) on map {map.Code}.");
    }

    private static int Column(MapContent map, float worldX) =>
        (int)Math.Floor((worldX - OriginX(map)) / SquareSizeUnits);

    private static int Row(MapContent map, float worldY) =>
        (int)Math.Floor((worldY - OriginY(map)) / SquareSizeUnits);
}
