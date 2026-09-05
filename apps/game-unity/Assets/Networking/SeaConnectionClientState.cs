using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaConnectionController
    {
        private ulong worldTickAnchor;
        private uint worldTickRate = 10;
        private double worldTickAnchorTime;

        public event Action<Ship> ShipChanged;
        public event Action<ShipMovement> ShipMovementChanged;
        public event Action<ulong> ShipMovementLeftInterest;

        /// <summary>
        /// One chunk's ships, packed (SEA_5 §12.1). Every hull but the player's own is placed
        /// off these rows: a chunk costs sixteen bytes a ship and one row change a tick however
        /// many of them are under way, where a movement row apiece cost a row change each.
        /// </summary>
        public event Action<ChunkMovement> ChunkMovementChanged;
        public event Action<WorldObject> WorldObjectChanged;
        public event Action<Volley> VolleyChanged;
        public event Action<ulong> VolleyLeftInterest;

        /// <summary>
        /// One volley's numbers, raised the moment the server settles it. What the presentation
        /// does with it waits on the flight time the row carries (SEA_5 8.3).
        /// </summary>
        public event Action<HitEvent> HitLanded;
        public event Action<Loot> LootChanged;
        public event Action<ulong> LootLeftInterest;
        public event Action<ulong> WorldTickChanged;
        public event Action HudStateChanged;
        public event Action PresentationReset;

        public ulong LocalShipEntityId => subscribedPlayerEntityId;
        public ulong CurrentWorldTick
        {
            get
            {
                return SeaWorldClock.Estimate(
                    worldTickAnchor,
                    worldTickRate,
                    worldTickAnchorTime,
                    Time.realtimeSinceStartupAsDouble);
            }
        }

        public uint WorldTickRate => worldTickRate;

        private void RegisterClientStateCallbacks(DbConnection connection)
        {
            connection.Db.WorldObject.OnInsert += HandleWorldObjectInserted;
            connection.Db.WorldObject.OnUpdate += HandleWorldObjectUpdated;
            connection.Db.WorldState.OnInsert += HandleWorldStateInserted;
            connection.Db.WorldState.OnUpdate += HandleWorldStateUpdated;
            connection.Db.PlayerClock.OnInsert += HandlePlayerClockInserted;
            connection.Db.PlayerClock.OnUpdate += HandlePlayerClockUpdated;
            connection.Db.ShipMovement.OnInsert += HandleShipMovementInserted;
            connection.Db.ShipMovement.OnUpdate += HandleShipMovementUpdated;
            connection.Db.ShipMovement.OnDelete += HandleShipMovementDeleted;
            connection.Db.ChunkMovement.OnInsert += HandleChunkMovementInserted;
            connection.Db.ChunkMovement.OnUpdate += HandleChunkMovementUpdated;
            connection.Db.PlayerProgression.OnInsert += HandleHudRowInserted;
            connection.Db.PlayerProgression.OnUpdate += HandleHudRowUpdated;
            connection.Db.EncounterReward.OnInsert += HandleHudRowInserted;
            connection.Db.Inventory.OnInsert += HandleHudRowInserted;
            connection.Db.Inventory.OnUpdate += HandleHudRowUpdated;
            connection.Db.Inventory.OnDelete += HandleHudRowDeleted;
            connection.Db.Effect.OnInsert += HandleHudRowInserted;
            connection.Db.Effect.OnUpdate += HandleHudRowUpdated;
            connection.Db.Effect.OnDelete += HandleHudRowDeleted;
            connection.Db.Cooldown.OnInsert += HandleHudRowInserted;
            connection.Db.Cooldown.OnUpdate += HandleHudRowUpdated;
            connection.Db.Cooldown.OnDelete += HandleHudRowDeleted;
            connection.Db.ShipChannel.OnInsert += HandleHudRowInserted;
            connection.Db.ShipChannel.OnUpdate += HandleHudRowUpdated;
            connection.Db.ShipChannel.OnDelete += HandleHudRowDeleted;
            connection.Db.RespawnWork.OnInsert += HandleHudRowInserted;
            connection.Db.RespawnWork.OnUpdate += HandleHudRowUpdated;
            connection.Db.RespawnWork.OnDelete += HandleHudRowDeleted;
            connection.Db.MapCrossingOffer.OnInsert += HandleHudRowInserted;
            connection.Db.MapCrossingOffer.OnUpdate += HandleHudRowUpdated;
            connection.Db.MapCrossingOffer.OnDelete += HandleHudRowDeleted;
            connection.Db.Loot.OnInsert += HandleLootInserted;
            connection.Db.Loot.OnUpdate += HandleLootUpdated;
            connection.Db.Loot.OnDelete += HandleLootDeleted;
        }

        private void HandleWorldObjectInserted(EventContext _context, WorldObject worldObject) =>
            NotifyWorldObjectChanged(worldObject);

        private void HandleWorldObjectUpdated(
            EventContext _context,
            WorldObject _oldWorldObject,
            WorldObject worldObject) => NotifyWorldObjectChanged(worldObject);

        private void HandleWorldStateInserted(EventContext _context, WorldState world) =>
            NotifyWorldStateChanged(world);

        private void HandleWorldStateUpdated(
            EventContext _context,
            WorldState _oldWorld,
            WorldState world) => NotifyWorldStateChanged(world);

        private void HandlePlayerClockInserted(EventContext _context, PlayerClock clock) =>
            SynchronizeWorldClock(clock);

        private void HandlePlayerClockUpdated(
            EventContext _context,
            PlayerClock _oldClock,
            PlayerClock clock) => SynchronizeWorldClock(clock);

        private void HandleHudRowInserted<TRow>(EventContext _context, TRow _row) =>
            NotifyHudStateChanged();

        private void HandleHudRowUpdated<TRow>(EventContext _context, TRow _oldRow, TRow _row) =>
            NotifyHudStateChanged();

        private void HandleHudRowDeleted<TRow>(EventContext _context, TRow _row) =>
            NotifyHudStateChanged();

        private void NotifyShipChanged(Ship ship)
        {
            ShipChanged?.Invoke(ship);
            if (SeaHudViewModel.DependsOnShip(
                    ship.EntityId,
                    subscribedPlayerEntityId,
                    selectedTargetEntityId))
            {
                NotifyHudStateChanged();
            }
        }

        private void HandleShipMovementInserted(EventContext _context, ShipMovement movement) =>
            NotifyShipMovementChanged(movement);

        private void HandleShipMovementUpdated(
            EventContext _context,
            ShipMovement _oldMovement,
            ShipMovement movement) => NotifyShipMovementChanged(movement);

        private void HandleShipMovementDeleted(EventContext _context, ShipMovement movement) =>
            ShipMovementLeftInterest?.Invoke(movement.EntityId);

        private void HandleChunkMovementInserted(EventContext _context, ChunkMovement chunk) =>
            ChunkMovementChanged?.Invoke(chunk);

        private void HandleChunkMovementUpdated(
            EventContext _context,
            ChunkMovement _oldChunk,
            ChunkMovement chunk) => ChunkMovementChanged?.Invoke(chunk);

        private void NotifyShipMovementChanged(ShipMovement movement)
        {
            RefreshSpatialScope(Connection, movement);
            ShipMovementChanged?.Invoke(movement);
        }

        private void NotifyWorldObjectChanged(WorldObject worldObject) =>
            WorldObjectChanged?.Invoke(worldObject);

        private void NotifyVolleyChanged(Volley volley) => VolleyChanged?.Invoke(volley);

        private void HandleHitInserted(EventContext _context, HitEvent hit) => HitLanded?.Invoke(hit);

        private void HandleLootInserted(EventContext _context, Loot loot) =>
            LootChanged?.Invoke(loot);

        private void HandleLootUpdated(EventContext _context, Loot _oldLoot, Loot loot) =>
            LootChanged?.Invoke(loot);

        private void HandleLootDeleted(EventContext _context, Loot loot) =>
            LootLeftInterest?.Invoke(loot.LootId);

        private void NotifyWorldStateChanged(WorldState world)
        {
            if (worldTickAnchorTime <= 0d)
            {
                SynchronizeWorldClock(world.Tick, world.TickRateHz);
            }

            NotifyHudStateChanged();
        }

        private void SynchronizeWorldClock(PlayerClock clock) =>
            SynchronizeWorldClock(clock.Tick, clock.TickRateHz);

        private void SynchronizeWorldClock(ulong tick, uint tickRate)
        {
            worldTickAnchor = tick;
            worldTickRate = Math.Max(1u, tickRate);
            worldTickAnchorTime = Time.realtimeSinceStartupAsDouble;
            WorldTickChanged?.Invoke(tick);
            NotifyHudStateChanged();
        }

        private void NotifyHudStateChanged() => HudStateChanged?.Invoke();

        private void NotifyPresentationReset()
        {
            worldTickAnchor = 0;
            worldTickRate = 10;
            worldTickAnchorTime = 0d;
            PresentationReset?.Invoke();
            NotifyHudStateChanged();
        }
    }
}
