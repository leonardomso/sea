using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    // Everything a transaction reads about the world at large: the tick it runs at,
    // the navigation field, the harbor, wind and currents, and whether any loot is
    // afloat. Each is read at most once per transaction. Nothing here outlives the
    // transaction; module statics are undefined across reducer calls.
    private sealed class TickWorld
    {
        private readonly Dictionary<byte, MovementShardState> movementShards = new();
        private Dictionary<uint, ChunkBlob>? chunks;
        private readonly Dictionary<ulong, (RouteWaypoint[] Points, int Count)> routes = new();
        private List<ShipMovement>? activePlayers;
        private List<NavigationBlocker>? blockers;
        private List<NavigationBlocker>? patrolBlockers;
        private WorldObject? harbor;
        private bool harborRead;
        private EnvironmentState? environment;
        private bool environmentRead;
        private CurrentFieldState? currentField;
        private bool currentFieldRead;
        private bool? hasActiveLoot;
        private List<BorderBand>? borderBands;

        public TickWorld(ulong tick) => Tick = tick;

        public static TickWorld Open(ReducerContext ctx) => new(CurrentSimulationTick(ctx));

        public ulong Tick { get; }

        /// <summary>
        /// The ships found lying against a border on this tick's movement. The movement loop
        /// records them and the crossing phase reads them, so the offer rows are written once,
        /// after every hull has finished sailing, rather than inside the loop.
        /// </summary>
        public List<BorderBand> BorderBands => borderBands ??= [];

        public void RecordBorderBand(BorderBand band) => BorderBands.Add(band);

        public List<NavigationBlocker> Blockers(ReducerContext ctx) =>
            blockers ??= NavigationBlockers(ctx);

        /// <summary>
        /// The same water, plus Port Lowell's sheltered circle, for the ships that have no
        /// business in it.
        /// </summary>
        /// <remarks>
        /// A hostile inside the harbour's safe water can neither shoot nor be shot at, so a
        /// patrol ring that crosses it leaves a ship parked there being neither a threat nor a
        /// target. Their routes are seeded anywhere on the chart and the harbour was never
        /// excluded, so this is where it is excluded: a hostile plots no leg that ends in
        /// sheltered water, and one that finds itself in it is given a mark outside.
        /// </remarks>
        public List<NavigationBlocker> PatrolBlockers(ReducerContext ctx)
        {
            if (patrolBlockers is not null)
            {
                return patrolBlockers;
            }

            patrolBlockers = new List<NavigationBlocker>(Blockers(ctx));
            if (Harbor(ctx) is WorldObject harbor)
            {
                patrolBlockers.Add(new NavigationBlocker(
                    harbor.PositionX, harbor.PositionY, WorldRules.HarborSafeRadiusSquares));
            }

            return patrolBlockers;
        }

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

        /// <summary>
        /// One chunk's packed ships, read once per transaction and edited in place.
        /// </summary>
        /// <remarks>
        /// The hulls in a chunk are spread across all eight movement shards, so a chunk is
        /// touched again and again through the movement phase. Writing the row on each touch
        /// would copy the whole blob per ship; holding it here and writing the dirty ones at the
        /// end costs one write a chunk however many ships sailed through it.
        /// </remarks>
        public ChunkBlob Chunk(ReducerContext ctx, byte mapId, int chunkX, int chunkY)
        {
            var id = ChunkBlobRules.RowId(mapId, chunkX, chunkY);
            chunks ??= new Dictionary<uint, ChunkBlob>();
            if (!chunks.TryGetValue(id, out var blob))
            {
                blob = ReadChunkBlob(ctx, id);
                chunks.Add(id, blob);
            }

            return blob;
        }

        /// <summary>
        /// Writes back every chunk whose packed bytes actually changed. A chunk of ships holding
        /// station packs to what it packed last tick and costs nothing.
        /// </summary>
        public void PublishDirtyChunks(ReducerContext ctx)
        {
            if (chunks is null)
            {
                return;
            }

            foreach (var (id, blob) in chunks)
            {
                if (blob.IsDirty)
                {
                    WriteChunkBlob(ctx, id, blob, Tick);
                }
            }
        }

        /// <summary>
        /// The course a ship is sailing, read once per transaction and then held. The
        /// span is over a buffer this world owns, so it is only good until the next
        /// call for the same ship; a caller that wants to keep it copies it.
        /// </summary>
        /// <remarks>
        /// Courses are read on the movement phase and written on the command phase
        /// before it, so the cache has to answer with what was just ordered rather
        /// than what the row said at the start of the tick. That is what
        /// <see cref="StoreRoute"/> is for: an NPC given a course this tick sails it
        /// on the same tick, which is what the dispatcher's ordering promises.
        /// </remarks>
        public ReadOnlySpan<RouteWaypoint> RouteFor(ReducerContext ctx, ulong entityId)
        {
            if (!routes.TryGetValue(entityId, out var cached))
            {
                cached = ReadRoute(ctx, entityId);
                routes[entityId] = cached;
            }

            return cached.Points.AsSpan(0, cached.Count);
        }

        public void StoreRoute(ulong entityId, ReadOnlySpan<RouteWaypoint> route)
        {
            if (!routes.TryGetValue(entityId, out var cached) ||
                cached.Points.Length < route.Length)
            {
                cached = (new RouteWaypoint[RouteRules.MaximumWaypoints], 0);
            }

            route.CopyTo(cached.Points);
            routes[entityId] = (cached.Points, route.Length);
        }

        private static (RouteWaypoint[] Points, int Count) ReadRoute(
            ReducerContext ctx,
            ulong entityId)
        {
            if (ctx.Db.ShipRoute.EntityId.Find(entityId) is not ShipRoute row)
            {
                return (Array.Empty<RouteWaypoint>(), 0);
            }

            var count = Math.Min(
                Math.Min(row.PointsX.Count, row.PointsY.Count),
                RouteRules.MaximumWaypoints);
            var points = new RouteWaypoint[RouteRules.MaximumWaypoints];
            for (var index = 0; index < count; index++)
            {
                points[index] = new RouteWaypoint(row.PointsX[index], row.PointsY[index]);
            }

            return (points, count);
        }

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

    }
}
