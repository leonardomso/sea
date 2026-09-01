using System;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace Sea.Client
{
    public sealed partial class SeaConnectionController
    {
        private readonly Dictionary<uint, ulong> levelThresholds = new();

        public event Action<Ship> ShipChanged;
        public event Action<WorldObject> WorldObjectChanged;
        public event Action<Volley> VolleyChanged;
        public event Action<ulong> VolleyLeftInterest;
        public event Action<ulong> WorldTickChanged;
        public event Action HudStateChanged;
        public event Action PresentationReset;

        public ulong LocalShipEntityId => subscribedPlayerEntityId;

        public bool TryGetLevelThreshold(uint level, out ulong experience) =>
            levelThresholds.TryGetValue(level, out experience);

        private void RegisterClientStateCallbacks(DbConnection connection)
        {
            connection.Db.WorldObject.OnInsert += HandleWorldObjectInserted;
            connection.Db.WorldObject.OnUpdate += HandleWorldObjectUpdated;
            connection.Db.WorldState.OnInsert += HandleWorldStateInserted;
            connection.Db.WorldState.OnUpdate += HandleWorldStateUpdated;
            connection.Db.LevelDefinition.OnInsert += HandleLevelDefinitionInserted;
            connection.Db.LevelDefinition.OnUpdate += HandleLevelDefinitionUpdated;
            connection.Db.LevelDefinition.OnDelete += HandleLevelDefinitionDeleted;
            connection.Db.PlayerProgression.OnInsert += HandleHudRowInserted;
            connection.Db.PlayerProgression.OnUpdate += HandleHudRowUpdated;
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

        private void NotifyWorldObjectChanged(WorldObject worldObject) =>
            WorldObjectChanged?.Invoke(worldObject);

        private void NotifyVolleyChanged(Volley volley) => VolleyChanged?.Invoke(volley);

        private void NotifyWorldStateChanged(WorldState world)
        {
            WorldTickChanged?.Invoke(world.Tick);
            NotifyHudStateChanged();
        }

        private void NotifyHudStateChanged() => HudStateChanged?.Invoke();

        private void NotifyPresentationReset()
        {
            levelThresholds.Clear();
            PresentationReset?.Invoke();
            NotifyHudStateChanged();
        }
    }
}
