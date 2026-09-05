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

        /// <summary>The chart she is on. The land mask and the route are read per map.</summary>
        public byte MapId;

        /// <summary>The last corner of her route, which is where the click landed.</summary>
        public float DestinationX;
        public float DestinationY;

        /// <summary>How far down her route she is; an index into the ShipRoute points.</summary>
        public int RouteIndex;

        /// <summary>Whether she has a course at all. IsMoving is this and not frozen.</summary>
        public bool HasRoute;

        /// <summary>Bumped every time a new course is laid, so a client can tell them apart.</summary>
        public uint RouteVersion;

        /// <summary>The tick the current one-second MoveTo window opened on.</summary>
        public ulong MoveWindowStartTick;

        /// <summary>How many courses she has been given inside that window.</summary>
        public uint MovesInWindow;

        /// <summary>
        /// How many of her commands the server has thrown away. Phase 12 turns this into
        /// a trust score of its own; for now it is the raw feed and nothing reads it.
        /// </summary>
        public uint DroppedCommandCount;

        public float HeadingDegrees;
        public float Speed;

        /// <summary>
        /// Her rating in squares per second: the hull's figure with the bonuses her fit
        /// earns already capped and worked in by <see cref="ShipStatRules"/>. It changes
        /// when the fit changes and at no other time -- nothing about the water she is in
        /// is in here.
        /// </summary>
        public float BaseSpeedSquaresPerSecond;

        /// <summary>Her speed this tick in squares per second, wind and debuffs included.</summary>
        public float EffectiveSpeedSquaresPerSecond;
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
        public float RangeSquares;
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

    /// <summary>
    /// A ship's course, corner by corner. Public because every client that can see
    /// her draws the same line (SEA_5 4.3).
    /// </summary>
    /// <remarks>
    /// The points live in their own row rather than on Ship because a course is
    /// written once, when it is ordered, while a Ship row is written on every tick
    /// she sails. Keeping the two apart means following a course does not rewrite
    /// the course. The two lists are parallel and always the same length.
    /// </remarks>
    [SpacetimeDB.Table(Accessor = "ShipRoute", Public = true)]
    public partial struct ShipRoute
    {
        [PrimaryKey]
        public ulong EntityId;
        public uint Version;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<float> PointsX;
        public List<float> PointsY;
#pragma warning restore MA0016
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

        /// <summary>
        /// The captain this hull sails under, or zero for a ship that answers to nobody. An
        /// escort lies at its mooring until its captain calls, and then takes her fight as its
        /// own -- which is how two more hulls join a named fight without any being conjured.
        /// </summary>
        public ulong LeaderEntityId;

        /// <summary>Latched for the life of the ship, so the call goes out exactly once.</summary>
        public bool HasCalledHelp;

        /// <summary>
        /// The earliest tick she may plot a new course. Working out where to go is
        /// arithmetic and happens five times a second; laying the way there is A*
        /// across a four-hundred-square grid and happens twice (SEA_5 11).
        /// </summary>
        public ulong NextReplanTick;

        /// <summary>
        /// The earliest tick an idle ship picks her next patrol leg, and how many she
        /// has already sailed. The count is what gives each wait a different length
        /// without a roll, so a replay of the same log loiters the same way.
        /// </summary>
        public ulong NextWanderTick;
        public ulong WanderIndex;
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

        /// <summary>
        /// Which eight-hour band the weather is currently showing. It is derived
        /// from the tick counter, so nothing schedules the next change and there
        /// is no strength to store either: SEA_5 5.1 fixes the wind at a flat
        /// ten per cent on every map and every band.
        /// </summary>
        public ulong WindBand;
        public float WindDirectionDegrees;
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
