using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SeedContent(ReducerContext ctx)
    {
        var content = Catalog.Content;
        var errors = ContentCatalog.Validate(content);
        if (errors.Count != 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        foreach (var map in content.Maps)
        {
            SeedMap(ctx, map);
        }

        foreach (var hull in content.Hulls)
        {
            ctx.Db.HullDef.Insert(HullDef.From(hull));
        }

        foreach (var cannon in content.Cannons)
        {
            ctx.Db.CannonDef.Insert(CannonDef.From(cannon));
        }

        foreach (var ammunition in content.Ammunition)
        {
            ctx.Db.AmmoDef.Insert(AmmoDef.From(ammunition));
        }

        foreach (var npc in content.Npcs)
        {
            ctx.Db.NpcDef.Insert(NpcDef.From(npc, Catalog.NpcStatsByArchetypeCode[(byte)npc.Code]));
        }

        ctx.Db.StatCaps.Insert(StatCaps.From(content.StatCaps));
    }

    private static void SeedMap(ReducerContext ctx, MapContent map)
    {
        ctx.Db.MapDef.Insert(MapDef.From(map));

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                ctx.Db.Sector.Insert(new Sector
                {
                    SectorId = SectorRules.SectorId(map.MapId, x, y),
                    MapId = map.MapId,
                    X = (byte)x,
                    Y = (byte)y,
                    TerrainCode = (byte)SectorRules.TerrainAt(map, x, y),
                });
            }
        }
    }
}
