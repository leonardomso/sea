using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private sealed class SpatialTickCache
    {
        private readonly Dictionary<int, List<WorldObject>> worldObjects = new();

        public IEnumerable<WorldObject> WorldObjectsIn(ReducerContext ctx, ChunkBounds bounds)
        {
            for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
            {
                for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
                {
                    foreach (var worldObject in WorldObjectsInChunk(ctx, chunkX, chunkY))
                    {
                        yield return worldObject;
                    }
                }
            }
        }

        private List<WorldObject> WorldObjectsInChunk(
            ReducerContext ctx,
            int chunkX,
            int chunkY)
        {
            var key = ChunkKey(chunkX, chunkY);
            if (!worldObjects.TryGetValue(key, out var values))
            {
                values = [.. ctx.Db.WorldObject.ByChunk.Filter((chunkX, chunkY))];
                worldObjects.Add(key, values);
            }

            return values;
        }

        private static int ChunkKey(int chunkX, int chunkY) =>
            chunkY * SpatialRules.ChunkCountPerAxis + chunkX;
    }
}
