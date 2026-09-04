namespace Sea.Server;

public static partial class ContentCatalog
{
    private static void ValidateMaps(IReadOnlyList<MapContent> maps, List<string> errors)
    {
        if (maps.Count == 0)
        {
            errors.Add("At least one map is required.");
            return;
        }

        var ids = new HashSet<byte>();
        var codes = new IdSet("map", "code");
        foreach (var map in maps)
        {
            if (!ids.Add(map.MapId))
            {
                errors.Add($"Duplicate map id {map.MapId}.");
            }

            var named = codes.Add(map.Code, errors);
            var label = named ? $"Map {map.Code}" : $"Map {map.MapId}";

            Positive(label, "map rank", map.MapRank, errors);
            NotEmpty(label, "name", map.Name, errors);
            NotEmpty(label, "port name", map.PortName, errors);
            Positive(label, "port radius", map.PortRadius, errors);
            Positive(label, "width", map.Width, errors);
            Positive(label, "height", map.Height, errors);

            var sized = map.Width > 0 && map.Height > 0;
            if (sized)
            {
                ValidateWorldExtent(map, label, errors);
            }

            var terrainValid = sized && ValidateTerrain(map, label, errors);
            if (terrainValid)
            {
                ValidatePort(map, label, errors);
            }

            ValidateObjects(map, label, terrainValid, errors);
            ValidateHarbor(map, label, errors);
            ValidateCurrents(map, label, errors);
        }
    }

    private static void ValidateWorldExtent(MapContent map, string label, List<string> errors)
    {
        if (SectorRules.OriginX(map) != WorldRules.MapMin || SectorRules.OriginY(map) != WorldRules.MapMin)
        {
            errors.Add(
                $"{label}: the {map.Width}x{map.Height} sector grid does not cover the world extent [{Format(WorldRules.MapMin)}, {Format(WorldRules.MapMax)}].");
        }
    }

    private static void ValidatePort(MapContent map, string label, List<string> errors)
    {
        if (!SectorRules.TrySectorOf(map, map.PortX, map.PortY, out var port))
        {
            errors.Add($"{label}: the port lies outside the map.");
            return;
        }

        if (SectorRules.TerrainAt(map, port.X, port.Y) != TerrainCode.Water)
        {
            errors.Add($"{label}: the port sector ({port.X}, {port.Y}) must be water.");
        }
    }

    /// <summary>
    /// The harbor object is the port circle the simulation actually reads: it is what makes a
    /// ship invulnerable and what a cast-off is measured against. A map that describes the port
    /// twice, once in its port fields and once in its object list, has to describe it the same
    /// way both times or the two would drift apart.
    /// </summary>
    private static void ValidateHarbor(MapContent map, string label, List<string> errors)
    {
        var harbors = 0;
        foreach (var item in map.Objects)
        {
            if (!string.Equals(item.Kind, "harbor", StringComparison.Ordinal))
            {
                continue;
            }

            harbors++;
            if (item.X != map.PortX || item.Y != map.PortY)
            {
                errors.Add(
                    $"{label}: harbor {item.EntityId} sits at ({Format(item.X)}, {Format(item.Y)}) but the port is at ({Format(map.PortX)}, {Format(map.PortY)}).");
            }

            if (item.Radius != map.PortRadius)
            {
                errors.Add(
                    $"{label}: harbor {item.EntityId} has radius {Format(item.Radius)} but the port radius is {Format(map.PortRadius)}.");
            }
        }

        if (harbors != 1)
        {
            errors.Add($"{label}: expected exactly one harbor object, found {harbors}.");
        }
    }

    private static bool ValidateTerrain(MapContent map, string label, List<string> errors)
    {
        if (map.TerrainRows.Count != map.Height)
        {
            errors.Add($"{label}: expected {map.Height} terrain rows, found {map.TerrainRows.Count}.");
            return false;
        }

        var valid = true;
        for (var y = 0; y < map.Height; y++)
        {
            var row = map.TerrainRows[y];
            if (row.Length != map.Width)
            {
                errors.Add($"{label}: terrain row {y} has {row.Length} columns, expected {map.Width}.");
                valid = false;
                continue;
            }

            for (var x = 0; x < map.Width; x++)
            {
                if (!HotPathCodes.TryParseTerrain(row[x], out _))
                {
                    errors.Add($"{label}: unknown terrain symbol '{row[x]}' at ({x}, {y}).");
                    valid = false;
                }
            }
        }

        return valid;
    }

    private static void ValidateObjects(MapContent map, string label, bool terrainValid, List<string> errors)
    {
        var ids = new HashSet<ulong>();
        foreach (var item in map.Objects)
        {
            var subject = $"{label}: object {item.EntityId}";
            if (!ids.Add(item.EntityId))
            {
                errors.Add($"{label}: duplicate object entity id {item.EntityId}.");
            }

            PositiveAtMost(subject, "radius", item.Radius, SpatialRules.MaximumWorldInfluenceRadius, errors);
            Between(subject, "direction", item.DirectionDegrees, 0f, 360f, errors);
            NotNegative(subject, "movement speed", item.MovementSpeed, errors);
            NotNegative(subject, "intensity", item.Intensity, errors);

            if (!HotPathCodes.TryParseWorldObject(item.Kind, out var kind))
            {
                errors.Add($"{subject}: unknown kind '{item.Kind}'.");
            }
            else if (item.BlocksMovement != HotPathCodes.BlocksMovement(kind))
            {
                errors.Add($"{subject}: blocksMovement disagrees with kind '{item.Kind}'.");
            }

            if (!terrainValid)
            {
                continue;
            }

            if (!SectorRules.TrySectorOf(map, item.X, item.Y, out var sector))
            {
                errors.Add($"{label}: object {item.EntityId} lies outside the map.");
                continue;
            }

            if (item.BlocksMovement && SectorRules.TerrainAt(map, sector.X, sector.Y) != TerrainCode.Land)
            {
                errors.Add(
                    $"{label}: object {item.EntityId} blocks movement but its sector ({sector.X}, {sector.Y}) is not land.");
            }
        }
    }

    private static void ValidateCurrents(MapContent map, string label, List<string> errors)
    {
        var ids = new HashSet<ulong>();
        foreach (var current in map.Currents)
        {
            var subject = $"{label}: current zone {current.ZoneId}";
            if (!ids.Add(current.ZoneId))
            {
                errors.Add($"{label}: duplicate current zone id {current.ZoneId}.");
            }

            PositiveAtMost(subject, "radius", current.Radius, SpatialRules.MaximumCurrentRadius, errors);
            Between(subject, "direction", current.DirectionDegrees, 0f, 360f, errors);
            Positive(subject, "strength", current.Strength, errors);
        }
    }
}
