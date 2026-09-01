using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "WorldState", Public = true)]
    public partial struct WorldState
    {
        [PrimaryKey]
        public uint Id;
        public ulong Tick;
        public uint TickRateHz;
        public ulong NextEntityId;
        public uint ContentVersion;
    }

    [SpacetimeDB.Table(Accessor = "SimulationTimer", Scheduled = "RunSimulationTick", ScheduledAt = "ScheduledAt")]
    public partial struct SimulationTimer
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Table(Accessor = "Ship", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByMoving", Columns = new[] { nameof(IsMoving) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEngaged", Columns = new[] { nameof(IsEngaged) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByTarget", Columns = new[] { nameof(TargetEntityId) })]
    public partial struct Ship
    {
        [PrimaryKey]
        public ulong EntityId;
        public string ArchetypeId;
        public string Faction;
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
        public bool IsActive;
        public bool IsAlive;
        public bool IsEngaged;
        public byte ModeCode;
        public int ChunkX;
        public int ChunkY;
        public ulong TargetEntityId;
        public string SelectedAmmoId;
        public string SelectedWeakPoint;
        public uint Hull;
        public uint MaxHull;
        public uint Sails;
        public uint MaxSails;
        public uint Cannons;
        public uint MaxCannons;
        public uint Crew;
        public uint MaxCrew;
        public uint CannonDamage;
        public uint CannonCooldownTicks;
        public ulong NextPortFireTick;
        public ulong NextStarboardFireTick;
        public ulong RespawnAtTick;
        public ulong InvulnerableUntilTick;
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
        public uint Level;
        public ulong Experience;
        public uint Gold;
    }

    [SpacetimeDB.Table(Accessor = "NpcAi", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByDecisionDue", Columns = new[] { nameof(IsActive), nameof(NextDecisionTick) })]
    public partial struct NpcAi
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public string ArchetypeId;
        public bool IsActive;
        public ulong NextDecisionTick;
        public ulong HomeSeed;
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

    [SpacetimeDB.Table(Accessor = "ShipStatus", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByShip", Columns = new[] { nameof(ShipEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    public partial struct ShipStatus
    {
        [PrimaryKey]
        [AutoInc]
        public ulong StatusId;
        public ulong ShipEntityId;
        public string StatusType;
        public uint Stacks;
        public ulong ExpiresAtTick;
        public ulong ImmunityUntilTick;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "Volley", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByTarget", Columns = new[] { nameof(TargetEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    public partial struct Volley
    {
        [PrimaryKey]
        [AutoInc]
        public ulong VolleyId;
        public ulong SourceEntityId;
        public ulong TargetEntityId;
        public string Side;
        public string AmmoId;
        public string WeakPoint;
        public float OriginX;
        public float OriginY;
        public int ChunkX;
        public int ChunkY;
        public ulong FiredAtTick;
        public ulong ImpactAtTick;
        public uint HullDamage;
        public uint SailDamage;
        public uint CannonDamage;
        public uint CrewDamage;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "Loot", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
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
    public partial struct Cooldown
    {
        [PrimaryKey]
        [AutoInc]
        public ulong CooldownId;
        public ulong ShipEntityId;
        public string CooldownType;
        public ulong ReadyAtTick;
    }

    [SpacetimeDB.Table(Accessor = "ShipChannel", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    public partial struct ShipChannel
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public string ChannelType;
        public ulong TargetEntityId;
        public ulong StartedAtTick;
        public ulong CompletesAtTick;
        public uint InitialHull;
        public uint InitialSails;
        public uint InitialCannons;
        public uint InitialCrew;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "CombatContribution", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByEncounter", Columns = new[] { nameof(EncounterId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByContributor", Columns = new[] { nameof(ContributorEntityId) })]
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
        public bool Rewarded;
    }

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByOwner", Columns = new[] { nameof(OwnerEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    public partial struct CombatEvent
    {
        [PrimaryKey]
        [AutoInc]
        public ulong EventId;
        public ulong OwnerEntityId;
        public string EventType;
        public string Details;
        public ulong Tick;
        public ulong ExpiresAtTick;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "WorldObject", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    public partial struct WorldObject
    {
        [PrimaryKey]
        public ulong EntityId;
        public string Kind;
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

    [SpacetimeDB.Table(Accessor = "AmmoDefinition", Public = true)]
    public partial struct AmmoDefinition
    {
        [PrimaryKey]
        public string AmmoId;
        public uint HullDamage;
        public uint SailDamage;
        public uint CannonDamage;
        public uint CrewDamage;
        public float RangeMultiplier;
        public string AppliedStatus;
    }

    [SpacetimeDB.Table(Accessor = "AbilityDefinition", Public = true)]
    public partial struct AbilityDefinition
    {
        [PrimaryKey]
        public string AbilityId;
        public uint CooldownTicks;
        public uint DurationTicks;
    }

    [SpacetimeDB.Table(Accessor = "NpcDefinition", Public = true)]
    public partial struct NpcDefinition
    {
        [PrimaryKey]
        public string NpcId;
        public float AggroRange;
        public float DesiredRange;
        public uint Hull;
    }

    [SpacetimeDB.Table(Accessor = "LevelDefinition", Public = true)]
    public partial struct LevelDefinition
    {
        [PrimaryKey]
        public uint Level;
        public ulong RequiredExperience;
    }

}
