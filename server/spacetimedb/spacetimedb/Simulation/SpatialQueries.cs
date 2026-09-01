using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static IEnumerable<WorldObject> WorldObjectsIn(
        ReducerContext ctx,
        ChunkBounds bounds)
    {
        for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
        {
            for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
            {
                foreach (var worldObject in ctx.Db.WorldObject.ByChunk.Filter((chunkX, chunkY)))
                {
                    yield return worldObject;
                }
            }
        }
    }

    private static IEnumerable<CurrentZone> CurrentZonesNear(
        ReducerContext ctx,
        float x,
        float y)
    {
        var bounds = SpatialRules.BoundsAround(
            x,
            y,
            SpatialRules.MaximumWorldInfluenceRadius);
        for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
        {
            for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
            {
                foreach (var zone in ctx.Db.CurrentZone.ByChunk.Filter((chunkX, chunkY)))
                {
                    yield return zone;
                }
            }
        }
    }

    private static IEnumerable<Ship> ActiveShipsIn(
        ReducerContext ctx,
        ChunkBounds bounds)
    {
        for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
        {
            for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
            {
                foreach (var ship in ctx.Db.Ship.ByActiveChunk.Filter((true, chunkX, chunkY)))
                {
                    yield return ship;
                }
            }
        }
    }
}
