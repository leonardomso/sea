namespace Sea.Server;

public readonly record struct SectorCoordinate(int X, int Y);

public static class SectorRules
{
    /// <summary>
    /// Packs a sector into one key: map id in bits 32-39, row in bits 16-31, column in bits
    /// 0-15. Sixteen bits per axis is far more than the 400 x 400 world needs (nine bits would
    /// do), but the packing costs the same three shifts and one or, and the headroom means a
    /// bigger map never has to touch this again. <c>checked</c> still guards the cast, so a
    /// coordinate that would alias throws instead of silently wrapping.
    /// </summary>
    public static ulong SectorId(byte mapId, SectorCoordinate sector) =>
        ((ulong)mapId << 32) | ((ulong)checked((ushort)sector.Y) << 16) | checked((ushort)sector.X);

    public static ulong SectorId(byte mapId, int x, int y) => SectorId(mapId, new SectorCoordinate(x, y));

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

        var x = Column(worldX);
        var y = Row(worldY);
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

        // TrySectorOf refuses a non-finite position; without this the clamp below would
        // turn one into square (0,0), a real sector, one line after it was refused.
        if (map.Width == 0 || map.Height == 0 ||
            !float.IsFinite(worldX) || !float.IsFinite(worldY))
        {
            return default;
        }

        return new SectorCoordinate(
            Math.Clamp(Column(worldX), 0, map.Width - 1),
            Math.Clamp(Row(worldY), 0, map.Height - 1));
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

    /// <summary>
    /// The chart square a position falls in. A position is already in squares,
    /// so this is only the whole part of it.
    /// </summary>
    /// <remarks>
    /// This class used to be the one documented crossing between world units and
    /// squares. There is no crossing any more: SEA_5 §3.3 stores positions in
    /// squares on the server and on the wire, so the conversion has been deleted
    /// rather than set to 1.0, to stop anyone reintroducing it.
    /// </remarks>
    public static int Column(float x) => (int)MathF.Floor(x);

    public static int Row(float y) => (int)MathF.Floor(y);
}
