using System;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaConnectionController
    {
        private readonly Dictionary<uint, ulong> levelThresholds = new();
        private ulong worldTickAnchor;
        private uint worldTickRate = 10;
        private double worldTickAnchorTime;

        public event Action<Ship> ShipChanged;
        public event Action<ShipMovement> ShipMovementChanged;
        public event Action<ulong> ShipMovementLeftInterest;
        public event Action<WorldObject> WorldObjectChanged;
        public event Action<Volley> VolleyChanged;
        public event Action<ulong> VolleyLeftInterest;
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

        public bool TryGetLevelThreshold(uint level, out ulong experience) =>
            levelThresholds.TryGetValue(level, out experience);

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
            connection.Db.LevelDefinition.OnInsert += HandleLevelDefinitionInserted;
            connection.Db.LevelDefinition.OnUpdate += HandleLevelDefinitionUpdated;
            connection.Db.LevelDefinition.OnDelete += HandleLevelDefinitionDeleted;
            connection.Db.PlayerProgression.OnInsert += HandleHudRowInserted;
            connection.Db.PlayerProgression.OnUpdate += HandleHudRowUpdated;
            connection.Db.EncounterReward.OnInsert += HandleHudRowInserted;
            connection.Db.Inventory.OnInsert += HandleHudRowInserted;
            connection.Db.Inventory.OnUpdate += HandleHudRowUpdated;
            connection.Db.Inventory.OnDelete += HandleHudRowDeleted;
            connection.Db.ShipStatus.OnInsert += HandleHudRowInserted;
            connection.Db.ShipStatus.OnUpdate += HandleHudRowUpdated;
            connection.Db.ShipStatus.OnDelete += HandleHudRowDeleted;
            connection.Db.Cooldown.OnInsert += HandleHudRowInserted;
            connection.Db.Cooldown.OnUpdate += HandleHudRowUpdated;
            connection.Db.Cooldown.OnDelete += HandleHudRowDeleted;
            connection.Db.ShipChannel.OnInsert += HandleHudRowInserted;
            connection.Db.ShipChannel.OnUpdate += HandleHudRowUpdated;
            connection.Db.ShipChannel.OnDelete += HandleHudRowDeleted;
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

        private void HandleLevelDefinitionInserted(EventContext _context, LevelDefinition definition) =>
            StoreLevelDefinition(definition);

        private void HandleLevelDefinitionUpdated(
            EventContext _context,
            LevelDefinition _oldDefinition,
            LevelDefinition definition) => StoreLevelDefinition(definition);

        private void HandleLevelDefinitionDeleted(EventContext _context, LevelDefinition definition)
        {
            levelThresholds.Remove(definition.Level);
            NotifyHudStateChanged();
        }

        private void StoreLevelDefinition(LevelDefinition definition)
        {
            levelThresholds[definition.Level] = definition.RequiredExperience;
            NotifyHudStateChanged();
        }

        private void HandleHudRowInserted<TRow>(EventContext _context, TRow _row) =>
            NotifyHudStateChanged();

        private void HandleHudRowUpdated<TRow>(EventContext _context, TRow _oldRow, TRow _row) =>
            NotifyHudStateChanged();

        private void HandleHudRowDeleted<TRow>(EventContext _context, TRow _row) =>
            NotifyHudStateChanged();

        private void NotifyShipChanged(Ship ship)
        {
            ShipChanged?.Invoke(ship);
            NotifyHudStateChanged();
        }

        private void HandleShipMovementInserted(EventContext _context, ShipMovement movement) =>
            NotifyShipMovementChanged(movement);

        private void HandleShipMovementUpdated(
            EventContext _context,
            ShipMovement _oldMovement,
            ShipMovement movement) => NotifyShipMovementChanged(movement);

        private void HandleShipMovementDeleted(EventContext _context, ShipMovement movement) =>
            ShipMovementLeftInterest?.Invoke(movement.EntityId);

        private void NotifyShipMovementChanged(ShipMovement movement)
        {
            RefreshSpatialScope(Connection, movement);
            ShipMovementChanged?.Invoke(movement);
        }

        private void NotifyWorldObjectChanged(WorldObject worldObject) =>
            WorldObjectChanged?.Invoke(worldObject);

        private void NotifyVolleyChanged(Volley volley) => VolleyChanged?.Invoke(volley);

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
            levelThresholds.Clear();
            worldTickAnchor = 0;
            worldTickRate = 10;
            worldTickAnchorTime = 0d;
            PresentationReset?.Invoke();
            NotifyHudStateChanged();
        }
    }
}
