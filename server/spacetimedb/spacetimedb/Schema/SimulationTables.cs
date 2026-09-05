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

    [SpacetimeDB.Table(Accessor = "SimulationClock")]
    public partial struct SimulationClock
    {
        [PrimaryKey]
        public uint Id;
        public ulong Tick;
        public ulong NextEntityId;
        public uint ConnectedPlayerCount;
    }

    [SpacetimeDB.Table(Accessor = "CurrentFieldState")]
    public partial struct CurrentFieldState
    {
        [PrimaryKey]
        public uint Id;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<CurrentFieldZone> Zones;
        public List<ulong> CellMasks;
#pragma warning restore MA0016
    }

    [SpacetimeDB.Table(Accessor = "NavigationFieldState")]
    public partial struct NavigationFieldState
    {
        [PrimaryKey]
        public uint Id;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<Sea.Server.NavigationBlockerState> Blockers;
#pragma warning restore MA0016
    }

    [SpacetimeDB.Type]
    public partial struct CurrentFieldZone
    {
        public float PositionX;
        public float PositionY;
        public float Radius;
        public float VelocityX;
        public float VelocityY;
    }

    [SpacetimeDB.Table(Accessor = "PlayerClock", Public = true)]
    public partial struct PlayerClock
    {
        [PrimaryKey]
        public Identity Owner;
        public ulong Tick;
        public uint TickRateHz;
    }

#pragma warning disable STDB_UNSTABLE
    [SpacetimeDB.ClientVisibilityFilter]
    public static readonly Filter PlayerClockOwnerFilter = new Filter.Sql(
        "SELECT * FROM player_clock WHERE player_clock.owner = :sender");
#pragma warning restore STDB_UNSTABLE

    [SpacetimeDB.Table(Accessor = "SimulationTelemetry", Public = true)]
    public partial struct SimulationTelemetry
    {
        [PrimaryKey]
        public uint Id;
        public ulong ObservedAtTick;
        public ulong SampledMovementRows;
        public ulong SampledNpcRows;
        public ulong DormantMovementRows;
        public ulong DormantNpcRows;
    }

    [SpacetimeDB.Table(
        Accessor = "SimulationDispatchTimer",
        Scheduled = "RunSimulationDispatch",
        ScheduledAt = "ScheduledAt")]
    public partial struct SimulationDispatchTimer
    {
        [PrimaryKey, AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Table(Accessor = "MovementShardState")]
    public partial struct MovementShardState
    {
        [PrimaryKey]
        public byte ShardId;
        public ulong LastSimulatedTick;
#pragma warning disable MA0016 // SpacetimeDB algebraic arrays require List<T> fields.
        public List<ShipKinematics> Ships;
#pragma warning restore MA0016
    }

    [SpacetimeDB.Type]
    public partial struct ShipKinematics
    {
        public ulong EntityId;
        public byte FactionCode;
        public byte MapId;
        public float PositionX;
        public float PositionY;
        public float DestinationX;
        public float DestinationY;
        public int RouteIndex;
        public bool HasRoute;
        public float HeadingDegrees;
        public float Speed;

        // Everything SpeedRules needs, carried on the shard so the tick can work her speed
        // out again from scratch without reading the fat row it is trying to avoid touching
        // (SEA_5 5.1). The fat row pushes a new copy of these through CopyTacticalParameters
        // whenever damage, a slow or the water she is in changes.
        public float BaseSpeedSquaresPerSecond;
        public uint Hull;
        public uint MaxHull;
        public byte MovementStatusMask;
        public float MovementSlowMagnitude;
        public byte EnvironmentExposureCode;
        public bool IsRepairing;

        // Worked out fresh every tick from the fields above.
        public float EffectiveSpeedSquaresPerSecond;
        public bool IsMoving;

        // Carried on the shard so crossing the harbour mouth is an edge the sailing step can see
        // without reading the fat row it is trying to avoid touching.
        public bool IsInPort;
        public float CurrentVelocityX;
        public float CurrentVelocityY;
        public int ChunkX;
        public int ChunkY;

        // What the client was last told, so the shard can tell whether its reckoning has
        // drifted far enough to be worth another row. Server-side only.
        public ulong PublishedTick;
        public float PublishedPositionX;
        public float PublishedPositionY;
        public float PublishedHeadingDegrees;
        public float PublishedVelocityX;
        public float PublishedVelocityY;
    }

    [SpacetimeDB.Table(Accessor = "MovementUpdate")]
    [SpacetimeDB.Index.BTree(Accessor = "ByShard", Columns = new[] { nameof(ShardId) })]
    public partial struct MovementUpdate
    {
        [PrimaryKey]
        public ulong ShipEntityId;
        public byte ShardId;
        public bool ReplaceKinematics;
        public bool Track;
        public ShipKinematics Kinematics;
    }

}
