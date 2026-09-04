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

        public TickWorld(ulong tick) => Tick = tick;

        public static TickWorld Open(ReducerContext ctx) => new(CurrentSimulationTick(ctx));

        public ulong Tick { get; }

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
