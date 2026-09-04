using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
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
