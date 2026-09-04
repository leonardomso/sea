using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    // Everything a transaction reads about the world at large: the tick it runs at,
    // the navigation field, the harbor, wind and currents, whether any loot is afloat,
    // and world objects by chunk. Each is read at most once per transaction. Nothing
    // here outlives the transaction; module statics are undefined across reducer calls.
    private sealed class TickWorld
    {
        private readonly Dictionary<int, List<WorldObject>> worldObjects = new();
        private readonly Dictionary<byte, MovementShardState> movementShards = new();
        private List<ShipMovement>? activePlayers;
        private List<NavigationBlocker>? blockers;
        private WorldObject? harbor;
        private bool harborRead;
        private EnvironmentState? environment;
        private bool environmentRead;
        private CurrentFieldState? currentField;
        private bool currentFieldRead;
        private bool? hasActiveLoot;

        public TickWorld(ulong tick) => Tick = tick;

        public static TickWorld Open(ReducerContext ctx) => new(CurrentSimulationTick(ctx));

        public ulong Tick { get; }

        public List<NavigationBlocker> Blockers(ReducerContext ctx) =>
            blockers ??= NavigationBlockers(ctx);

        public WorldObject? Harbor(ReducerContext ctx)
        {
            if (!harborRead)
            {
                harbor = FindHarbor(ctx);
                harborRead = true;
            }

            return harbor;
        }

        public EnvironmentState? Environment(ReducerContext ctx)
        {
            if (!environmentRead)
            {
                environment = ctx.Db.EnvironmentState.Id.Find(1);
                environmentRead = true;
            }

            return environment;
        }

        public CurrentFieldState? CurrentField(ReducerContext ctx)
        {
            if (!currentFieldRead)
            {
                currentField = ctx.Db.CurrentFieldState.Id.Find(1);
                currentFieldRead = true;
            }

            return currentField;
        }

        // Loot rows are deleted when claimed or expired, so any row at all means a
        // moving player may have something to pick up.
        public bool HasActiveLoot(ReducerContext ctx) =>
            hasActiveLoot ??= ctx.Db.Loot.Count > 0;

        // A shard row carries every ship it is sailing in one blob, which makes it the
        // dearest read in the tick. NPC hydration and the movement phase both want the same
        // eight of them, so they share one copy each and movement writes its edits back.
        public MovementShardState MovementShard(ReducerContext ctx, byte shardId)
        {
            if (!movementShards.TryGetValue(shardId, out var shard))
            {
                shard = FindMovementShard(ctx, shardId);
                movementShards[shardId] = shard;
            }

            return shard;
        }

        public void StoreMovementShard(MovementShardState shard) =>
            movementShards[shard.ShardId] = shard;

        // Every NPC hunts through the same handful of player rows, so the scan is paid once
        // for the tick rather than once per hull. Players hold still inside this transaction:
        // every decision is made before the movement phase sails anyone.
        public List<ShipMovement> ActivePlayers(ReducerContext ctx) =>
            activePlayers ??= [.. ctx.Db.ShipMovement.ByActiveFaction.Filter(
                (true, (byte)FactionCode.Player))];

        public bool IsAttackablePlayer(ReducerContext ctx, Ship ship) =>
            ship.IsActive && ship.IsAlive &&
            ship.FactionCode == (byte)FactionCode.Player &&
            !NpcRules.IsProtectedFromNpcs(
                ship.InvulnerableUntilTick,
                Tick,
                Harbor(ctx) is WorldObject harbor
                    ? CombatRules.Distance(
                        ship.PositionX,
                        ship.PositionY,
                        harbor.PositionX,
                        harbor.PositionY)
                    : float.PositiveInfinity);

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

        private List<WorldObject> WorldObjectsInChunk(ReducerContext ctx, int chunkX, int chunkY)
        {
            var key = chunkY * SpatialRules.ChunkCountPerAxis + chunkX;
            if (!worldObjects.TryGetValue(key, out var cached))
            {
                cached = [.. ctx.Db.WorldObject.ByChunk.Filter((chunkX, chunkY))];
                worldObjects[key] = cached;
            }

            return cached;
        }
    }
}
