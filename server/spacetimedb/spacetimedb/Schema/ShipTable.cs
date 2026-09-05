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

        /// <summary>
        /// The rate of her hull, which is how much water she draws: a fourth rate cannot cross
        /// a shoal, so her course is plotted on a chart with the shallows filled in. On the row
        /// for the same reason the combat sheet is -- plotting a course must not join the dock
        /// tables to find out what she is.
        /// </summary>
        public byte HullTier;

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

        /// <summary>
        /// Her fighting sailors and the complement she was built with (SEA_2 §5.7). Hands are
        /// spent boarding and come back one a minute at sea, which is worked out from
        /// <see cref="HandsRecoveredAtTick"/> when somebody asks rather than added on every tick
        /// to every hull afloat.
        /// </summary>
        public uint Hands;

        /// <inheritdoc cref="Hands"/>
        public uint MaxHands;

        /// <inheritdoc cref="Hands"/>
        public ulong HandsRecoveredAtTick;

        /// <summary>The tick she may throw hooks again: SEA_5 §9.3, 60 s after a player, 15 s after a hostile.</summary>
        public ulong BoardCooldownUntilTick;

        /// <summary>The tick she may be boarded again. Five minutes, and it is the victim's clock.</summary>
        public ulong BoardImmuneUntilTick;

        /// <summary>The tick her guns come back after boarders spiked them (SEA_3 §4.3).</summary>
        public ulong WeaponSilencedUntilTick;
    }
}
