using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private sealed class SpatialTickCache
    {
        private readonly Dictionary<int, List<CurrentZone>> currentZones = new();
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

        public IEnumerable<CurrentZone> CurrentZonesNear(ReducerContext ctx, float x, float y)
        {
            var bounds = SpatialRules.BoundsAround(
                x,
                y,
                SpatialRules.MaximumCurrentRadius);
            for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
            {
                for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
                {
                    foreach (var zone in CurrentZonesInChunk(ctx, chunkX, chunkY))
                    {
                        yield return zone;
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

        private List<CurrentZone> CurrentZonesInChunk(
            ReducerContext ctx,
            int chunkX,
            int chunkY)
        {
            var key = ChunkKey(chunkX, chunkY);
            if (!currentZones.TryGetValue(key, out var values))
            {
                values = [.. ctx.Db.CurrentZone.ByActiveChunk.Filter((true, chunkX, chunkY))];
                currentZones.Add(key, values);
            }

            return values;
        }

        private static int ChunkKey(int chunkX, int chunkY) =>
            chunkY * SpatialRules.ChunkCountPerAxis + chunkX;
    }
}
