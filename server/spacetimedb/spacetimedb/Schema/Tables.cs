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

    [SpacetimeDB.Table(Accessor = "MovementShardTimer", Scheduled = "RunMovementShard", ScheduledAt = "ScheduledAt")]
    public partial struct MovementShardTimer
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
        public byte ShardId;
    }

    [SpacetimeDB.Table(Accessor = "Ship", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByMoving", Columns = new[] { nameof(IsMoving) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByMovingShard", Columns = new[] { nameof(IsMoving), nameof(MovementShard) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByActiveChunk", Columns = new[] { nameof(IsActive), nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByEnvironmentExposure", Columns = new[] { nameof(EnvironmentExposureCode) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByTarget", Columns = new[] { nameof(TargetEntityId) })]
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
        public byte EnvironmentExposureCode;
        public float CurrentVelocityX;
        public float CurrentVelocityY;
        public int ChunkX;
        public int ChunkY;
        public ulong TargetEntityId;
        public byte SelectedAmmoCode;
        public byte SelectedWeakPointCode;
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
        public ulong EncounterId;
    }

    [SpacetimeDB.Table(Accessor = "RespawnWork", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByRespawnDue", Columns = new[] { nameof(IsPending), nameof(RespawnAtTick) })]
    public partial struct RespawnWork
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public bool IsPending;
        public ulong RespawnAtTick;
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
    [SpacetimeDB.Index.BTree(Accessor = "ByShipStatus", Columns = new[] { nameof(ShipEntityId), nameof(StatusCode) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByStatusDue", Columns = new[] { nameof(IsActive), nameof(NextProcessTick) })]
    public partial struct ShipStatus
    {
        [PrimaryKey]
        [AutoInc]
        public ulong StatusId;
        public ulong ShipEntityId;
        public string StatusType;
        public byte StatusCode;
        public uint Stacks;
        public ulong ExpiresAtTick;
        public ulong ImmunityUntilTick;
        public ulong NextProcessTick;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "Volley", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByTarget", Columns = new[] { nameof(TargetEntityId) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByChunk", Columns = new[] { nameof(ChunkX), nameof(ChunkY) })]
    [SpacetimeDB.Index.BTree(Accessor = "ByImpactDue", Columns = new[] { nameof(IsActive), nameof(ImpactAtTick) })]
    public partial struct Volley
    {
        [PrimaryKey]
        [AutoInc]
        public ulong VolleyId;
        public ulong SourceEntityId;
        public ulong TargetEntityId;
        public string Side;
        public byte SideCode;
        public string AmmoId;
        public byte AmmoCode;
        public string WeakPoint;
        public byte WeakPointCode;
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
    [SpacetimeDB.Index.BTree(Accessor = "ByActive", Columns = new[] { nameof(IsActive) })]
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

    [SpacetimeDB.Table(Accessor = "CombatEvent", Public = true, Event = true)]
    public partial struct CombatEvent
    {
        public ulong OwnerEntityId;
        public string EventType;
        public string Details;
        public ulong Tick;
    }

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

    [SpacetimeDB.Table(Accessor = "AmmoDefinition", Public = true)]
    public partial struct AmmoDefinition
    {
        [PrimaryKey]
        public string AmmoId;
        [Unique]
        public byte AmmoCode;
        public uint HullDamage;
        public uint SailDamage;
        public uint CannonDamage;
        public uint CrewDamage;
        public float RangeMultiplier;
        public string AppliedStatus;
        public byte AppliedStatusCode;
    }

    [SpacetimeDB.Table(Accessor = "AbilityDefinition", Public = true)]
    public partial struct AbilityDefinition
    {
        [PrimaryKey]
        public string AbilityId;
        [Unique]
        public byte AbilityCode;
        public uint CooldownTicks;
        public uint DurationTicks;
    }

    [SpacetimeDB.Table(Accessor = "NpcDefinition", Public = true)]
    public partial struct NpcDefinition
    {
        [PrimaryKey]
        public string NpcId;
        [Unique]
        public byte ArchetypeCode;
        public float AggroRange;
        public float DesiredRange;
        public float MaximumSpeed;
        public uint Hull;
        public uint CannonDamage;
        public byte PreferredAmmoCode;
        public byte PreferredWeakPointCode;
        public uint GoldReward;
        public ulong ExperienceReward;
    }

    [SpacetimeDB.Table(Accessor = "LevelDefinition", Public = true)]
    public partial struct LevelDefinition
    {
        [PrimaryKey]
        public uint Level;
        public ulong RequiredExperience;
    }

}
