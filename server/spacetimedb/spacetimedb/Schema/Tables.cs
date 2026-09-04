using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "Ship", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActiveChunk", Columns = new[] { nameof(IsActive), nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEnvironmentExposure", Columns = new[] { nameof(EnvironmentExposureCode) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByTarget", Columns = new[] { nameof(TargetEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByReloading", Columns = new[] { nameof(IsReloading) })]
    public partial struct Ship
    {
        [PrimaryKey]
        public ulong EntityId;
        public byte ArchetypeCode;
        public byte FactionCode;
        public float PositionX;
        public float PositionY;
        public float DestinationX;
        public float DestinationY;
        public float WaypointX;
        public float WaypointY;
        public bool HasWaypoint;
        public float HeadingDegrees;
        public float Speed;
        public float MaximumSpeed;
        public float Acceleration;
        public float Deceleration;
        public float TurnRateDegrees;
        public bool HasCourse;
        public bool IsStopping;
        public bool IsMoving;
        public byte MovementShard;
        public bool IsActive;
        public bool IsAlive;
        public bool IsEngaged;
        public byte ModeCode;
        public byte MovementStatusMask;

        // The live slow magnitude behind the mask bit, so the sailing shard reads one float
        // instead of joining the effect table every tick.
        public float MovementSlowMagnitude;
        public byte EnvironmentExposureCode;
        public float CurrentVelocityX;
        public float CurrentVelocityY;
        public int ChunkX;
        public int ChunkY;
        public ulong TargetEntityId;
        public byte SelectedAmmoCode;
        public uint Hull;
        public uint MaxHull;

        // The live combat sheet. RecomputeShipStats writes these for players and NPC spawn writes
        // them for NPCs, so firing reads one row instead of joining the dock-facing projection.
        public uint VolleyDamage;
        public uint ReloadTicks;
        public uint MagazineSize;
        public float RangeUnits;
        public float ArmorFront;
        public float ArmorSides;
        public float ArmorBack;

        // The magazine itself. Reload advances every tick whether or not the ship is firing.
        public uint ReadyVolleys;
        public uint ReloadProgressTicks;

        // The repair sheet, alongside the combat one: how much of the hull one channel mends and
        // how long the crew has to hold station for it.
        public float RepairAmount;
        public uint RepairChannelTicks;

        // Indexed so the tick advances reloads for the handful of ships mid-magazine instead
        // of walking every hull afloat.
        public bool IsReloading;
        public bool HasFired;
        public ulong LastShotTick;
        public ulong LastCombatTick;

        public ulong RespawnAtTick;
        public ulong InvulnerableUntilTick;
        public ulong EncounterId;

        // Stored rather than derived: entering the port is an edge that clears effects, and the
        // damage paths that must honour it read rows the movement shard may not have republished.
        public bool IsInPort;
    }

    [SpacetimeDB.Table(Accessor = "ShipMovement", Public = true)]
    [SpacetimeDB.Index.BTree(
        Accessor = "ByActiveChunk",
        Columns = new[] { nameof(IsActive), nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(
        Accessor = "ByActiveFaction",
        Columns = new[] { nameof(IsActive), nameof(FactionCode) })]
    public partial struct ShipMovement
    {
        [PrimaryKey]
        public ulong EntityId;
        public byte FactionCode;
        public float PositionX;
        public float PositionY;
        public float HeadingDegrees;
        public float Speed;
        public bool IsMoving;
        public bool IsActive;
        public bool IsAlive;
        public byte MovementShard;
        public int ChunkX;
        public int ChunkY;
        public ulong SnapshotTick;
    }

    [SpacetimeDB.Table(Accessor = "RespawnWork", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByRespawnDue", Columns = new[] { nameof(IsPending), nameof(RespawnAtTick) })]
    public partial struct RespawnWork
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public bool IsPending;
        public ulong RespawnAtTick;

        // Zero until the wreck's owner picks a berth. A player who has not chosen stays sunk
        // however long the timer has run; NPCs are given their home the moment they go down.
        public byte OptionCode;
    }

    [SpacetimeDB.Table(Accessor = "PlayerOwnership", Public = true)]
    public partial struct PlayerOwnership
    {
        [PrimaryKey]
        public Identity Owner;
        [Unique]
        public ulong ShipEntityId;
        public bool IsConnected;
    }

    [SpacetimeDB.Table(Accessor = "PlayerProgression", Public = true)]
    public partial struct PlayerProgression
    {
        [PrimaryKey]
        public Identity Owner;
        public byte MapRank;
        public uint Gold;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter PlayerProgressionOwnerFilter = new Filter.Sql(
        "SELECT * FROM player_progression WHERE player_progression.owner = :sender");
#pragma warning restore STDB_UNSTABLE

    [SpacetimeDB.Table(Accessor = "NpcAi", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByDecisionDueShard", Columns = new[] { nameof(IsActive), nameof(DecisionShard), nameof(NextDecisionTick) })]
    public partial struct NpcAi
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public bool IsActive;
        public byte DecisionShard;
        public ulong NextDecisionTick;
        public ulong HomeSeed;
        public float HomeX;
        public float HomeY;
    }

    [SpacetimeDB.Table(Accessor = "Inventory", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByShip", Columns = new[] { nameof(ShipEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByShipItem", Columns = new[] { nameof(ShipEntityId), nameof(ItemId) })]
    public partial struct Inventory
    {
        [PrimaryKey]
        [AutoInc]
        public ulong InventoryId;
        public ulong ShipEntityId;
        public string ItemId;
        public uint Quantity;
    }

    /// <summary>
    /// One live effect on one ship. The same code refreshes its own row's expiry, different codes
    /// take a row each, so there is no stack count to keep.
    /// </summary>
    [SpacetimeDB.Table(Accessor = "Effect", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByShip", Columns = new[] { nameof(ShipEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByShipEffect", Columns = new[] { nameof(ShipEntityId), nameof(EffectCode) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEffectDue", Columns = new[] { nameof(IsActive), nameof(NextProcessTick) })]
    public partial struct Effect
    {
        [PrimaryKey]
        [AutoInc]
        public ulong EffectId;
        public ulong ShipEntityId;
        public ulong SourceEntityId;
        public string EffectType;
        public byte EffectCode;
        public float Magnitude;
        public ulong AppliedAtTick;
        public ulong ExpiresAtTick;
        public ulong NextProcessTick;
        public bool IsActive;
    }

    /// <summary>
    /// A shot the client animates. Damage resolves on the tick the volley is fired, so the row
    /// carries no damage state and exists only until its animation window closes.
    /// </summary>
    [SpacetimeDB.Table(Accessor = "Volley", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByVolleyExpiry", Columns = new[] { nameof(IsActive), nameof(ExpiresAtTick) })]
    public partial struct Volley
    {
        [PrimaryKey]
        [AutoInc]
        public ulong VolleyId;
        public ulong SourceEntityId;
        public ulong TargetEntityId;
        public string AmmoId;
        public byte AmmoCode;
        public float OriginX;
        public float OriginY;
        public float TargetX;
        public float TargetY;
        public int ChunkX;
        public int ChunkY;
        public ulong FiredAtTick;
        public ulong ExpiresAtTick;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "Loot", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActiveChunk", Columns = new[] { nameof(IsActive), nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByLootExpiryDue", Columns = new[] { nameof(IsActive), nameof(ExpiresAtTick) })]
    public partial struct Loot
    {
        [PrimaryKey]
        [AutoInc]
        public ulong LootId;
        public float PositionX;
        public float PositionY;
        public int ChunkX;
        public int ChunkY;
        public string LootType;
        public uint Quantity;
        public bool IsActive;
        public ulong ExpiresAtTick;
        public ulong ClaimedByEntityId;
    }

    [SpacetimeDB.Table(Accessor = "Cooldown", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByShip", Columns = new[] { nameof(ShipEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByShipCooldown", Columns = new[] { nameof(ShipEntityId), nameof(CooldownTypeCode) })]
    public partial struct Cooldown
    {
        [PrimaryKey]
        [AutoInc]
        public ulong CooldownId;
        public ulong ShipEntityId;
        public string CooldownType;
        public byte CooldownTypeCode;
        public ulong ReadyAtTick;
    }

    [SpacetimeDB.Table(Accessor = "ShipChannel", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByChannelDue", Columns = new[] { nameof(IsActive), nameof(NextProcessTick) })]
    public partial struct ShipChannel
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public string ChannelType;
        public byte ChannelTypeCode;
        public ulong TargetEntityId;
        public ulong StartedAtTick;
        public ulong CompletesAtTick;
        public ulong NextProcessTick;
        public uint InitialHull;

        // Damage suffered since the channel opened. Enough of it breaks the channel; a little
        // does not, so a single stray shot no longer costs a full repair.
        public uint DamageTaken;
        public bool IsActive;
    }

    /// <summary>
    /// The heals a ship has completed recently, newest last. Fatigue is a rolling window, so it
    /// cannot be collapsed into a counter; the list is pruned every time it is read.
    /// </summary>
    [SpacetimeDB.Table(Accessor = "ShipHealLog")]
    public partial struct ShipHealLog
    {
        [PrimaryKey]
        public ulong ShipEntityId;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<ulong> CompletedTicks;
#pragma warning restore MA0016
    }

    [SpacetimeDB.Table(Accessor = "CombatContribution")]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounter", Columns = new[] { nameof(EncounterId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByContributor", Columns = new[] { nameof(ContributorEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounterContributor", Columns = new[] { nameof(EncounterId), nameof(ContributorEntityId) })]
    public partial struct CombatContribution
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ContributionId;
        public ulong EncounterId;
        public ulong ContributorEntityId;
        public ulong Damage;
        public ulong Boarding;
        public ulong Support;
    }

    [SpacetimeDB.Table(Accessor = "CombatEncounter")]
    [SpacetimeDB.Index.BTree(Accessor = "ByNpc", Columns = new[] { nameof(NpcEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByState", Columns = new[] { nameof(StateCode) })]
    public partial struct CombatEncounter
    {
        [PrimaryKey]
        public ulong EncounterId;
        public ulong NpcEntityId;
        public byte StateCode;
        public uint GoldPool;
        public ulong ExperiencePool;
        public ulong OpenedAtTick;
        public ulong SettledAtTick;
    }

    [SpacetimeDB.Table(Accessor = "EncounterReward", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(Owner) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounter", Columns = new[] { nameof(EncounterId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounterContributor", Columns = new[] { nameof(EncounterId), nameof(ContributorEntityId) })]
    public partial struct EncounterReward
    {
        [PrimaryKey]
        [AutoInc]
        public ulong RewardId;
        public ulong EncounterId;
        public Identity Owner;
        public ulong ContributorEntityId;
        public uint Gold;
        public ulong Experience;
        public ulong AwardedAtTick;
    }

    [SpacetimeDB.Table(Accessor = "EncounterRewardEvent", Public = true, Event = true)]
    public partial struct EncounterRewardEvent
    {
        public Identity Owner;
        public ulong EncounterId;
        public ulong ContributorEntityId;
        public uint Gold;
        public ulong Experience;
    }

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true, Event = true)]
    public partial struct CombatEvent
    {
        public ulong OwnerEntityId;
        public string EventType;
        public string Details;
        public ulong Tick;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter EncounterRewardOwnerFilter = new Filter.Sql(
        "SELECT * FROM encounter_reward WHERE encounter_reward.owner = :sender");

    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter EncounterRewardEventOwnerFilter = new Filter.Sql(
        "SELECT * FROM encounter_reward_event WHERE encounter_reward_event.owner = :sender");
#pragma warning restore STDB_UNSTABLE

    [SpacetimeDB.Table(Accessor = "WorldObject", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByActiveKind", Columns = new[] { nameof(IsActive), nameof(KindCode) })]
    public partial struct WorldObject
    {
        [PrimaryKey]
        public ulong EntityId;
        public string Kind;
        public byte KindCode;
        public float PositionX;
        public float PositionY;
        public float Radius;
        public int ChunkX;
        public int ChunkY;
        public bool IsActive;
        public bool BlocksMovement;
        public float DirectionDegrees;
        public float MovementSpeed;
        public float Intensity;
    }

    [SpacetimeDB.Table(Accessor = "EnvironmentState", Public = true)]
    public partial struct EnvironmentState
    {
        [PrimaryKey]
        public uint Id;
        public ulong Seed;
        public ulong WindEpoch;
        public float WindDirectionDegrees;
        public float WindStrength;
        public ulong NextWindChangeTick;
    }

    [SpacetimeDB.Table(Accessor = "CurrentZone", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByActiveChunk", Columns = new[] { nameof(IsActive), nameof(ChunkX), nameof(ChunkY) })]
    public partial struct CurrentZone
    {
        [PrimaryKey]
        public ulong ZoneId;
        public float PositionX;
        public float PositionY;
        public float Radius;
        public float DirectionDegrees;
        public float Strength;
        public int ChunkX;
        public int ChunkY;
        public bool IsActive;
    }

}
