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
            SpatialRules.MaximumWorldInfluenceRadiusSquares);
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

    // The thin published kinematics row: the live position of every active ship
    // without deserialising the full Ship record.
    private static IEnumerable<ShipMovement> ActiveMovementIn(
        ReducerContext ctx,
        ChunkBounds bounds)
    {
        for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
        {
            for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
            {
                foreach (var movement in ctx.Db.ShipMovement.ByActiveChunk.Filter(
                             (true, chunkX, chunkY)))
                {
                    yield return movement;
                }
            }
        }
    }

    private static IEnumerable<Loot> ActiveLootIn(
        ReducerContext ctx,
        ChunkBounds bounds)
    {
        for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
        {
            for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
            {
                foreach (var loot in ctx.Db.Loot.ByActiveChunk.Filter(
                             (true, chunkX, chunkY)))
                {
                    yield return loot;
                }
            }
        }
    }
}
