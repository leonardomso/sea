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

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        if (ctx.Db.WorldState.Id.Find(1) is not null)
        {
            return;
        }

        ctx.Db.WorldState.Insert(new WorldState
        {
            Id = 1,
            Tick = 0,
            TickRateHz = WorldRules.TickRateHz,
            NextEntityId = 1000,
            ContentVersion = 2,
        });
        SeedContent(ctx);
        SeedWorld(ctx);
        SeedEnvironment(ctx);
        ctx.Db.SimulationTimer.Insert(new SimulationTimer
        {
            ScheduledAt = new ScheduleAt.Interval(
                TimeSpan.FromMilliseconds(1000d / WorldRules.TickRateHz)),
        });
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        SetConnectionStateIfLoaded(ctx, ctx.Sender, true);
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        SetConnectionStateIfLoaded(ctx, ctx.Sender, false);
    }

    [SpacetimeDB.Reducer]
    public static void LoadPlayer(ReducerContext ctx)
    {
        if (ctx.Db.PlayerOwnership.Owner.Find(ctx.Sender) is PlayerOwnership ownership)
        {
            ownership.IsConnected = true;
            ctx.Db.PlayerOwnership.Owner.Update(ownership);
            EnsureProgression(ctx, ctx.Sender);
            return;
        }

        var entityId = AllocateEntityId(ctx);
        var spawn = FindSafeSpawn(ctx, IdentitySeed(ctx.Sender));
        var ship = CreateShip(entityId, "player_sloop", "player", spawn.X, spawn.Y);
        ctx.Db.Ship.Insert(ship);
        ctx.Db.PlayerOwnership.Insert(new PlayerOwnership
        {
            Owner = ctx.Sender,
            ShipEntityId = entityId,
            IsConnected = true,
        });
        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = ctx.Sender,
            Level = 1,
            Experience = 0,
            Gold = 0,
        });
        SeedPlayerInventory(ctx, entityId);
        AppendEvent(ctx, entityId, "player_loaded", $"entity_id={entityId}");
    }

    [SpacetimeDB.Reducer]
    public static void SetCourse(ReducerContext ctx, float x, float y)
    {
        if (!WorldRules.IsValidMove(x, y))
        {
            throw new Exception("The requested position is outside the map.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        var blockers = NavigationBlockers(ctx);
        if (NavigationRules.IsDestinationBlocked(x, y, blockers))
        {
            AppendEvent(ctx, ship.EntityId, "course_ignored", "destination_is_land");
            return;
        }

        ship.DestinationX = x;
        ship.DestinationY = y;
        ConfigureNavigationWaypoint(ref ship, blockers);
        ship.HasCourse = ship.PositionX != x || ship.PositionY != y;
        ship.IsStopping = false;
        ship.IsMoving = ship.PositionX != x || ship.PositionY != y;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "set_course", $"x={x:0.###},y={y:0.###}");
    }

    [SpacetimeDB.Reducer]
    public static void StopCourse(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.DestinationX = ship.PositionX;
        ship.DestinationY = ship.PositionY;
        ship.WaypointX = ship.PositionX;
        ship.WaypointY = ship.PositionY;
        ship.HasWaypoint = false;
        ship.HasCourse = false;
        ship.IsStopping = ship.Speed > 0f;
        ship.IsMoving = ship.Speed > 0f;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "stop_course", "");
    }

    [SpacetimeDB.Reducer]
    public static void SelectTarget(ReducerContext ctx, ulong entityId)
    {
        var target = FindShip(ctx, entityId);
        if (!target.IsActive || !target.IsAlive || target.Faction == "player")
        {
            throw new Exception("The selected ship cannot be targeted.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var distance = CombatRules.Distance(
            ship.PositionX,
            ship.PositionY,
            target.PositionX,
            target.PositionY);
        if (!TacticalRules.CanAcquireTarget(
                HasActiveStatus(ctx, target.EntityId, "smoke_screen", world.Tick),
                distance))
        {
            throw new Exception("Smoke conceals that ship at long range.");
        }

        ship.TargetEntityId = entityId;
        ship.IsEngaged = false;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "select_target", $"entity_id={entityId}");
    }

    [SpacetimeDB.Reducer]
    public static void ClearTarget(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.TargetEntityId = 0;
        ship.IsEngaged = false;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "clear_target", "");
    }

    [SpacetimeDB.Reducer]
    public static void SetAmmo(ReducerContext ctx, string ammoId)
    {
        if (ctx.Db.AmmoDefinition.AmmoId.Find(ammoId) is null)
        {
            throw new Exception("The selected ammunition does not exist.");
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        if (FindInventory(ctx, ship.EntityId, ammoId) is null)
        {
            throw new Exception("The selected ammunition is not in this ship's inventory.");
        }

        ship.SelectedAmmoId = ammoId;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "set_ammo", $"ammo={ammoId}");
    }

    [SpacetimeDB.Reducer]
    public static void FireBroadside(ReducerContext ctx, string side, string weakPoint)
    {
        if (!Enum.TryParse<BroadsideSide>(side, ignoreCase: true, out var parsedSide) ||
            !Enum.IsDefined(parsedSide))
        {
            throw new Exception("Broadside side must be port or starboard.");
        }

        if (!CombatRules.TryParseWeakPoint(weakPoint, out var parsedWeakPoint))
        {
            throw new Exception("Weak point must be hull, sails, or cannons.");
        }

        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var source = FindPlayerShip(ctx, ctx.Sender);
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var ammunition = ctx.Db.AmmoDefinition.AmmoId.Find(source.SelectedAmmoId) ??
            throw new Exception("The selected ammunition definition is missing.");
        var inventory = FindInventory(ctx, source.EntityId, source.SelectedAmmoId);
        var readyAtTick = parsedSide == BroadsideSide.Port
            ? source.NextPortFireTick
            : source.NextStarboardFireTick;
        var rejection = CombatRules.ValidateFire(new FireRequest
        {
            SourceAlive = source.IsActive && source.IsAlive,
            TargetSelected = target.HasValue,
            TargetAlive = target is Ship selected && selected.IsActive && selected.IsAlive,
            Cannons = source.Cannons,
            Ammunition = inventory?.Quantity ?? 0,
            CurrentTick = world.Tick,
            ReadyAtTick = readyAtTick,
            SourceX = source.PositionX,
            SourceY = source.PositionY,
            SourceHeadingDegrees = source.HeadingDegrees,
            TargetX = target?.PositionX ?? source.PositionX,
            TargetY = target?.PositionY ?? source.PositionY,
            MaximumRange = WorldRules.CannonRange,
            RangeMultiplier = ammunition.RangeMultiplier,
            Side = parsedSide,
            IsChanneling = FindActiveChannel(ctx, source.EntityId) is not null,
        });
        if (rejection != FireRejection.None)
        {
            throw new Exception(FireRejectionMessage(rejection));
        }

        var selectedTarget = target!.Value;
        var selectedInventory = inventory!.Value;
        var damage = CombatRules.DamageProfile(
            new AmmunitionContent
            {
                Id = ammunition.AmmoId,
                HullDamage = ammunition.HullDamage,
                SailDamage = ammunition.SailDamage,
                CannonDamage = ammunition.CannonDamage,
                CrewDamage = ammunition.CrewDamage,
                RangeMultiplier = ammunition.RangeMultiplier,
                AppliedStatus = ammunition.AppliedStatus,
            },
            parsedWeakPoint,
            source.CannonDamage,
            source.Cannons,
            source.MaxCannons);
        var hazards = HazardsAt(ctx, source.PositionX, source.PositionY);
        if (hazards.InStorm)
        {
            damage = ScaleCombatDamage(damage, hazards.Modifiers.WeaponEffectiveness);
        }
        var distance = CombatRules.Distance(
            source.PositionX,
            source.PositionY,
            selectedTarget.PositionX,
            selectedTarget.PositionY);
        var impactAtTick = world.Tick + CombatRules.VolleyTravelTicks(
            distance,
            CombatRules.ProjectileSpeed,
            world.TickRateHz);

        selectedInventory.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(selectedInventory);
        source.SelectedWeakPoint = weakPoint.ToLowerInvariant();
        source.IsEngaged = true;
        var reloadTicks = TacticalRules.AdjustedReloadTicks(
            source.CannonCooldownTicks,
            source.Cannons,
            source.MaxCannons);
        if (parsedSide == BroadsideSide.Port)
        {
            source.NextPortFireTick = world.Tick + reloadTicks;
        }
        else
        {
            source.NextStarboardFireTick = world.Tick + reloadTicks;
        }

        ctx.Db.Ship.EntityId.Update(source);
        ctx.Db.Volley.Insert(new Volley
        {
            SourceEntityId = source.EntityId,
            TargetEntityId = selectedTarget.EntityId,
            Side = side.ToLowerInvariant(),
            AmmoId = ammunition.AmmoId,
            WeakPoint = weakPoint.ToLowerInvariant(),
            OriginX = source.PositionX,
            OriginY = source.PositionY,
            ChunkX = source.ChunkX,
            ChunkY = source.ChunkY,
            FiredAtTick = world.Tick,
            ImpactAtTick = impactAtTick,
            HullDamage = damage.Hull,
            SailDamage = damage.Sails,
            CannonDamage = damage.Cannons,
            CrewDamage = damage.Crew,
            IsActive = true,
        });
        AppendEvent(
            ctx,
            source.EntityId,
            "broadside_fired",
            $"target={selectedTarget.EntityId},side={side},ammo={ammunition.AmmoId},impact_tick={impactAtTick}");
    }

    [SpacetimeDB.Reducer]
    public static void ActivateAbility(ReducerContext ctx, string abilityId)
    {
        var ability = ctx.Db.AbilityDefinition.AbilityId.Find(abilityId);
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var ship = FindPlayerShip(ctx, ctx.Sender);
        var cooldown = FindCooldown(ctx, ship.EntityId, abilityId);
        var rejection = TacticalRules.ValidateAbility(new AbilityRequest(
            ship.IsActive && ship.IsAlive,
            ability is not null,
            FindActiveChannel(ctx, ship.EntityId) is null,
            world.Tick,
            cooldown?.ReadyAtTick ?? 0));
        if (rejection != AbilityRejection.None)
        {
            throw new Exception(AbilityRejectionMessage(rejection));
        }

        var selectedAbility = ability!.Value;
        if (abilityId == "emergency_pump")
        {
            DeactivateStatus(ctx, ship.EntityId, "flooding", world.Tick);
        }

        ApplyStatus(
            ctx,
            ship.EntityId,
            abilityId,
            world.Tick,
            selectedAbility.DurationTicks,
            maximumStacks: 1);
        SetCooldown(
            ctx,
            ship.EntityId,
            abilityId,
            world.Tick + selectedAbility.CooldownTicks);
        AppendEvent(ctx, ship.EntityId, "ability_activated", $"ability={abilityId}");
    }

    [SpacetimeDB.Reducer]
    public static void StartRepair(ReducerContext ctx)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var ship = FindPlayerShip(ctx, ctx.Sender);
        var kit = FindInventory(ctx, ship.EntityId, "repair_kit");
        var rejection = TacticalRules.ValidateRepair(new RepairRequest(
            ship.IsActive && ship.IsAlive,
            FindActiveChannel(ctx, ship.EntityId) is null,
            kit is Inventory item && item.Quantity > 0,
            ship.Hull < ship.MaxHull || ship.Sails < ship.MaxSails ||
                ship.Cannons < ship.MaxCannons || ship.Crew < ship.MaxCrew));
        if (rejection != RepairRejection.None)
        {
            throw new Exception(RepairRejectionMessage(rejection));
        }

        var repairKit = kit!.Value;
        repairKit.Quantity--;
        ctx.Db.Inventory.InventoryId.Update(repairKit);
        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = ship.EntityId,
            ChannelType = "repair",
            TargetEntityId = ship.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.RepairDurationTicks,
            InitialHull = ship.Hull,
            InitialSails = ship.Sails,
            InitialCannons = ship.Cannons,
            InitialCrew = ship.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, ship.EntityId, "repair_started", "");
    }

    [SpacetimeDB.Reducer]
    public static void CancelRepair(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        CancelChannel(ctx, ship.EntityId, "repair", "repair_cancelled");
    }

    [SpacetimeDB.Reducer]
    public static void StartBoarding(ReducerContext ctx)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var source = FindPlayerShip(ctx, ctx.Sender);
        var target = source.TargetEntityId == 0
            ? default(Ship?)
            : ctx.Db.Ship.EntityId.Find(source.TargetEntityId);
        var cooldown = FindCooldown(ctx, source.EntityId, "boarding");
        var rejection = TacticalRules.ValidateBoarding(new BoardingRequest(
            source.IsActive && source.IsAlive,
            target is Ship selected && selected.IsActive && selected.IsAlive,
            FindActiveChannel(ctx, source.EntityId) is null,
            target?.Hull ?? 0,
            target?.MaxHull ?? 0,
            target is Ship boardingTarget
                ? CombatRules.Distance(
                    source.PositionX,
                    source.PositionY,
                    boardingTarget.PositionX,
                    boardingTarget.PositionY)
                : float.PositiveInfinity,
            world.Tick,
            cooldown?.ReadyAtTick ?? 0));
        if (rejection != BoardingRejection.None)
        {
            throw new Exception(BoardingRejectionMessage(rejection));
        }

        ctx.Db.ShipChannel.Insert(new ShipChannel
        {
            ShipEntityId = source.EntityId,
            ChannelType = "boarding",
            TargetEntityId = target!.Value.EntityId,
            StartedAtTick = world.Tick,
            CompletesAtTick = world.Tick + TacticalRules.BoardingDurationTicks,
            InitialHull = source.Hull,
            InitialSails = source.Sails,
            InitialCannons = source.Cannons,
            InitialCrew = source.Crew,
            IsActive = true,
        });
        AppendEvent(ctx, source.EntityId, "boarding_started", $"target={target.Value.EntityId}");
    }

    [SpacetimeDB.Reducer]
    public static void CancelBoarding(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        CancelChannel(ctx, ship.EntityId, "boarding", "boarding_cancelled");
    }

    [SpacetimeDB.Reducer]
    public static void MoveTo(ReducerContext ctx, float x, float y) => SetCourse(ctx, x, y);

    [SpacetimeDB.Reducer]
    public static void UpgradeCannon(ReducerContext ctx)
    {
        var progression = FindProgression(ctx, ctx.Sender);
        var cost = checked(100u * progression.Level);
        if (progression.Gold < cost)
        {
            throw new Exception("The player cannot afford this cannon upgrade.");
        }

        progression.Gold -= cost;
        ctx.Db.PlayerProgression.Owner.Update(progression);
        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.CannonDamage += WorldRules.CannonDamagePerUpgrade;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "cannon_upgraded", $"cost={cost}");
    }

    [SpacetimeDB.Reducer]
    public static void RunSimulationTick(ReducerContext ctx, SimulationTimer _timer)
    {
        if (ctx.Db.WorldState.Id.Find(1) is not WorldState world)
        {
            return;
        }

        world.Tick++;
        ctx.Db.WorldState.Id.Update(world);
        UpdateWind(ctx, world.Tick);
        MoveStorms(ctx);
        ProcessStatuses(ctx, world.Tick);
        ProcessChannels(ctx, world.Tick);
        AdvanceMovingShips(ctx);
        ApplyEnvironmentalHazards(ctx, world.Tick);
        ResolveVolleys(ctx, world.Tick);
        ExpireTransientRows(ctx, world.Tick);
    }

    private static void SetConnectionStateIfLoaded(
        ReducerContext ctx,
        Identity owner,
        bool connected)
    {
        if (ctx.Db.PlayerOwnership.Owner.Find(owner) is not PlayerOwnership ownership)
        {
            return;
        }

        ownership.IsConnected = connected;
        ctx.Db.PlayerOwnership.Owner.Update(ownership);
    }

    private static ulong AllocateEntityId(ReducerContext ctx)
    {
        var world = ctx.Db.WorldState.Id.Find(1) ??
            throw new Exception("World state is missing.");
        var entityId = world.NextEntityId;
        world.NextEntityId++;
        ctx.Db.WorldState.Id.Update(world);
        return entityId;
    }

    private static Ship CreateShip(
        ulong entityId,
        string archetypeId,
        string faction,
        float x,
        float y)
    {
        return new Ship
        {
            EntityId = entityId,
            ArchetypeId = archetypeId,
            Faction = faction,
            PositionX = x,
            PositionY = y,
            DestinationX = x,
            DestinationY = y,
            WaypointX = x,
            WaypointY = y,
            HasWaypoint = false,
            HeadingDegrees = 0f,
            Speed = 0f,
            MaximumSpeed = WorldRules.PlayerShipSpeed,
            Acceleration = 3f,
            Deceleration = 4f,
            TurnRateDegrees = WorldRules.PlayerShipTurnRateDegrees,
            HasCourse = false,
            IsStopping = false,
            IsMoving = false,
            IsActive = true,
            IsAlive = true,
            IsEngaged = false,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            TargetEntityId = 0,
            SelectedAmmoId = "round",
            SelectedWeakPoint = "hull",
            Hull = WorldRules.InitialHealth,
            MaxHull = WorldRules.InitialHealth,
            Sails = 100,
            MaxSails = 100,
            Cannons = 100,
            MaxCannons = 100,
            Crew = 100,
            MaxCrew = 100,
            CannonDamage = WorldRules.InitialCannonDamage,
            CannonCooldownTicks = WorldRules.InitialCannonCooldownTicks / 2,
            NextPortFireTick = 0,
            NextStarboardFireTick = 0,
            RespawnAtTick = 0,
            InvulnerableUntilTick = 0,
        };
    }

    private static Ship FindPlayerShip(ReducerContext ctx, Identity owner)
    {
        var ownership = ctx.Db.PlayerOwnership.Owner.Find(owner) ??
            throw new Exception("Player has not been loaded.");
        return FindShip(ctx, ownership.ShipEntityId);
    }

    private static Ship FindShip(ReducerContext ctx, ulong entityId) =>
        ctx.Db.Ship.EntityId.Find(entityId) ??
        throw new Exception("The requested ship does not exist.");

    private static PlayerProgression FindProgression(ReducerContext ctx, Identity owner) =>
        ctx.Db.PlayerProgression.Owner.Find(owner) ??
        throw new Exception("Player progression is missing.");

    private static void EnsureProgression(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.PlayerProgression.Owner.Find(owner) is not null)
        {
            return;
        }

        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = owner,
            Level = 1,
            Experience = 0,
            Gold = 0,
        });
    }

    private static void AdvanceMovingShips(ReducerContext ctx)
    {
        var deltaSeconds = 1f / WorldRules.TickRateHz;
        var worldTick = ctx.Db.WorldState.Id.Find(1)?.Tick ?? 0;
        var environment = ctx.Db.EnvironmentState.Id.Find(1);
        var navigationBlockers = NavigationBlockers(ctx);
        foreach (var ship in ctx.Db.Ship.ByMoving.Filter(true))
        {
            if (!ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var routedShip = ship;
            if (routedShip.HasWaypoint && NavigationRules.Distance(
                    routedShip.PositionX,
                    routedShip.PositionY,
                    routedShip.WaypointX,
                    routedShip.WaypointY) <= NavigationRules.WaypointArrivalRadius)
            {
                routedShip.HasWaypoint = false;
                ConfigureNavigationWaypoint(ref routedShip, navigationBlockers);
            }

            var navigationX = routedShip.HasWaypoint
                ? routedShip.WaypointX
                : routedShip.DestinationX;
            var navigationY = routedShip.HasWaypoint
                ? routedShip.WaypointY
                : routedShip.DestinationY;
            var windMultiplier = environment is EnvironmentState wind
                ? EnvironmentRules.WindSpeedMultiplier(
                    routedShip.HeadingDegrees,
                    wind.WindDirectionDegrees,
                    wind.WindStrength)
                : 1f;
            var hazards = HazardsAt(ctx, routedShip.PositionX, routedShip.PositionY);
            var movementModifiers = TacticalRules.MovementModifiers(
                HasActiveStatus(ctx, routedShip.EntityId, "full_sail", worldTick),
                ActiveStatusStacks(ctx, routedShip.EntityId, "slowed", worldTick),
                routedShip.Sails == 0,
                routedShip.MaxSails == 0
                    ? 0f
                    : (float)routedShip.Sails / routedShip.MaxSails,
                hazards.InShoal,
                hazards.InStorm,
                FindActiveChannel(ctx, routedShip.EntityId) is ShipChannel channel &&
                    channel.ChannelType == "repair");
            var step = SailingRules.Step(
                new SailingState(
                    routedShip.PositionX,
                    routedShip.PositionY,
                    routedShip.HeadingDegrees,
                    routedShip.Speed),
                navigationX,
                navigationY,
                routedShip.IsStopping,
                new SailingParameters(
                    routedShip.MaximumSpeed * windMultiplier * movementModifiers.MaximumSpeed,
                    routedShip.Acceleration * movementModifiers.Acceleration,
                    routedShip.Deceleration,
                    routedShip.TurnRateDegrees * movementModifiers.TurnRate),
                deltaSeconds);
            var current = CurrentVelocityAt(ctx, step.PositionX, step.PositionY);
            var nextX = step.PositionX + current.X * deltaSeconds;
            var nextY = step.PositionY + current.Y * deltaSeconds;
            var moved = routedShip;
            moved.HeadingDegrees = step.HeadingDegrees;
            moved.Speed = step.Speed;
            moved.IsMoving = step.IsMoving;
            moved.HasCourse = routedShip.HasCourse && (!step.Arrived || routedShip.HasWaypoint);
            moved.IsStopping = routedShip.IsStopping && step.Speed > 0f;
            if (IsNavigablePosition(ctx, routedShip.EntityId, nextX, nextY))
            {
                moved.PositionX = Math.Clamp(nextX, WorldRules.MapMin, WorldRules.MapMax);
                moved.PositionY = Math.Clamp(nextY, WorldRules.MapMin, WorldRules.MapMax);
            }
            else
            {
                moved.HasCourse = false;
                moved.IsStopping = true;
                moved.Speed = MathF.Max(0f, routedShip.Speed - routedShip.Deceleration * deltaSeconds);
                moved.IsMoving = moved.Speed > 0f;
                moved.DestinationX = routedShip.PositionX;
                moved.DestinationY = routedShip.PositionY;
                moved.HasWaypoint = false;
            }

            moved.ChunkX = SpatialRules.ChunkCoordinate(moved.PositionX);
            moved.ChunkY = SpatialRules.ChunkCoordinate(moved.PositionY);
            ctx.Db.Ship.EntityId.Update(moved);
        }
    }

    private static void UpdateWind(ReducerContext ctx, ulong tick)
    {
        if (ctx.Db.EnvironmentState.Id.Find(1) is not EnvironmentState environment ||
            tick < environment.NextWindChangeTick)
        {
            return;
        }

        environment.WindEpoch++;
        var wind = EnvironmentRules.WindForEpoch(environment.Seed, environment.WindEpoch);
        environment.WindDirectionDegrees = wind.DirectionDegrees;
        environment.WindStrength = wind.Strength;
        environment.NextWindChangeTick = tick + EnvironmentRules.WindEpochTicks;
        ctx.Db.EnvironmentState.Id.Update(environment);
    }

    private static (float X, float Y) CurrentVelocityAt(
        ReducerContext ctx,
        float x,
        float y)
    {
        var velocityX = 0f;
        var velocityY = 0f;
        foreach (var zone in ctx.Db.CurrentZone.Iter())
        {
            if (!zone.IsActive ||
                !WorldRules.IsInRange(x, y, zone.PositionX, zone.PositionY, zone.Radius))
            {
                continue;
            }

            var velocity = EnvironmentRules.DirectionalVelocity(
                zone.DirectionDegrees,
                zone.Strength);
            velocityX += velocity.X;
            velocityY += velocity.Y;
        }

        return (velocityX, velocityY);
    }

    private static bool IsNavigablePosition(
        ReducerContext ctx,
        ulong movingEntityId,
        float x,
        float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            return false;
        }

        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (worldObject.IsActive && worldObject.BlocksMovement &&
                WorldRules.IsBlocked(
                    worldObject.Kind,
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius,
                    x,
                    y))
            {
                return false;
            }
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (ship.EntityId != movingEntityId && ship.IsAlive &&
                WorldRules.IsInRange(x, y, ship.PositionX, ship.PositionY, 4f))
            {
                return false;
            }
        }

        return true;
    }

    private static void MoveStorms(ReducerContext ctx)
    {
        var deltaSeconds = 1f / WorldRules.TickRateHz;
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (!worldObject.IsActive || worldObject.Kind != "storm" ||
                worldObject.MovementSpeed <= 0f)
            {
                continue;
            }

            var position = TacticalRules.MoveStorm(
                worldObject.PositionX,
                worldObject.PositionY,
                worldObject.DirectionDegrees,
                worldObject.MovementSpeed,
                deltaSeconds);
            var moved = worldObject;
            moved.PositionX = position.X;
            moved.PositionY = position.Y;
            moved.ChunkX = SpatialRules.ChunkCoordinate(position.X);
            moved.ChunkY = SpatialRules.ChunkCoordinate(position.Y);
            ctx.Db.WorldObject.EntityId.Update(moved);
        }
    }

    private static void ProcessStatuses(ReducerContext ctx, ulong tick)
    {
        foreach (var status in ctx.Db.ShipStatus.ByActive.Filter(true))
        {
            if (tick >= status.ExpiresAtTick)
            {
                var lifecycle = TacticalRules.ExpireStatus(
                    new TacticalStatusState(
                        status.IsActive,
                        status.Stacks,
                        status.ExpiresAtTick,
                        status.ImmunityUntilTick),
                    tick,
                    TacticalRules.StatusImmunityTicks);
                var expired = status;
                expired.IsActive = lifecycle.IsActive;
                expired.Stacks = lifecycle.Stacks;
                expired.ImmunityUntilTick = lifecycle.ImmunityUntilTick;
                ctx.Db.ShipStatus.StatusId.Update(expired);
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(status.ShipEntityId) is not Ship ship ||
                !ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var damage = TacticalRules.PeriodicStatusDamage(
                status.StatusType,
                status.Stacks,
                tick);
            if (damage > 0)
            {
                var damaged = ship;
                ApplyDamageToShip(
                    ctx,
                    sourceEntityId: 0,
                    ref damaged,
                    new CombatDamage(damage, 0, 0, 0),
                    tick,
                    status.StatusType);
                ctx.Db.Ship.EntityId.Update(damaged);
                continue;
            }

            if (status.StatusType == "emergency_pump" && tick % 5 == 0 &&
                ship.Hull < ship.MaxHull)
            {
                var restored = ship;
                restored.Hull = Math.Min(restored.MaxHull, restored.Hull + 2);
                ctx.Db.Ship.EntityId.Update(restored);
            }
        }
    }

    private static void ProcessChannels(ReducerContext ctx, ulong tick)
    {
        foreach (var channel in ctx.Db.ShipChannel.ByActive.Filter(true))
        {
            if (ctx.Db.Ship.EntityId.Find(channel.ShipEntityId) is not Ship source ||
                !source.IsActive || !source.IsAlive)
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                continue;
            }

            if (channel.ChannelType == "repair")
            {
                var elapsed = Math.Min(
                    (ulong)TacticalRules.RepairDurationTicks,
                    tick - channel.StartedAtTick);
                var repaired = source;
                repaired.Hull = TacticalRules.ProgressiveRestore(
                    channel.InitialHull,
                    source.MaxHull,
                    restoreAmount: 50,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Sails = TacticalRules.ProgressiveRestore(
                    channel.InitialSails,
                    source.MaxSails,
                    restoreAmount: 40,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Cannons = TacticalRules.ProgressiveRestore(
                    channel.InitialCannons,
                    source.MaxCannons,
                    restoreAmount: 40,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                repaired.Crew = TacticalRules.ProgressiveRestore(
                    channel.InitialCrew,
                    source.MaxCrew,
                    restoreAmount: 20,
                    elapsed,
                    TacticalRules.RepairDurationTicks);
                ctx.Db.Ship.EntityId.Update(repaired);
                SynchronizeDisabledSails(ctx, repaired, tick);
                if (tick >= channel.CompletesAtTick)
                {
                    ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                    AppendEvent(ctx, channel.ShipEntityId, "repair_completed", "");
                }

                continue;
            }

            if (channel.ChannelType != "boarding")
            {
                ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(channel.TargetEntityId) is not Ship target ||
                TacticalRules.ValidateBoarding(new BoardingRequest(
                    source.IsActive && source.IsAlive,
                    target.IsActive && target.IsAlive,
                    IsIdle: true,
                    target.Hull,
                    target.MaxHull,
                    CombatRules.Distance(
                        source.PositionX,
                        source.PositionY,
                        target.PositionX,
                        target.PositionY),
                    CurrentTick: tick,
                    ReadyAtTick: tick)) != BoardingRejection.None)
            {
                InterruptBoarding(ctx, channel.ShipEntityId, tick, "boarding_interrupted");
                continue;
            }

            if (tick < channel.CompletesAtTick)
            {
                continue;
            }

            var fatigued = HasActiveStatus(ctx, source.EntityId, "boarding_fatigue", tick);
            var succeeded = TacticalRules.BoardingSucceeds(source.Crew, target.Crew, fatigued);
            if (succeeded)
            {
                var boarded = target;
                boarded.Crew = WorldRules.ApplyDamage(boarded.Crew, 25);
                ctx.Db.Ship.EntityId.Update(boarded);
                AddInventory(ctx, source.EntityId, "boarding_cache", 1);
                AppendEvent(
                    ctx,
                    source.EntityId,
                    "boarding_succeeded",
                    $"target={target.EntityId}");
            }
            else
            {
                ApplyStatus(
                    ctx,
                    source.EntityId,
                    "boarding_fatigue",
                    tick,
                    TacticalRules.BoardingFatigueTicks,
                    maximumStacks: 1);
                AppendEvent(
                    ctx,
                    source.EntityId,
                    "boarding_failed",
                    $"target={target.EntityId}");
            }

            SetCooldown(
                ctx,
                source.EntityId,
                "boarding",
                tick + TacticalRules.BoardingCooldownTicks);
            ctx.Db.ShipChannel.ShipEntityId.Delete(channel.ShipEntityId);
        }
    }

    private static void ApplyEnvironmentalHazards(ReducerContext ctx, ulong tick)
    {
        if (tick % WorldRules.TickRateHz != 0)
        {
            return;
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (!ship.IsAlive)
            {
                continue;
            }

            var hazards = HazardsAt(ctx, ship.PositionX, ship.PositionY);
            var affected = ship;
            if (hazards.InStorm)
            {
                ApplyDamageToShip(
                    ctx,
                    sourceEntityId: 0,
                    ref affected,
                    new CombatDamage(2, 0, 0, 0),
                    tick,
                    "storm");
            }

            if (hazards.InShoal && TacticalRules.ShouldApplyStatus(
                    ship.EntityId ^ tick,
                    chancePercent: 35))
            {
                ApplyStatus(
                    ctx,
                    ship.EntityId,
                    "flooding",
                    tick,
                    TacticalRules.StatusDurationTicks,
                    maximumStacks: 3);
            }

            if (!affected.Equals(ship))
            {
                ctx.Db.Ship.EntityId.Update(affected);
            }
        }
    }

    private static void ResolveVolleys(ReducerContext ctx, ulong tick)
    {
        foreach (var volley in ctx.Db.Volley.ByActive.Filter(true))
        {
            if (tick < volley.ImpactAtTick)
            {
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(volley.TargetEntityId) is not Ship target ||
                CombatRules.ResolveVolley(volley.ImpactAtTick, tick, target.IsActive && target.IsAlive) ==
                VolleyResolution.Harmless)
            {
                ctx.Db.Volley.VolleyId.Delete(volley.VolleyId);
                continue;
            }

            var defender = target;
            var appliedDamage = ApplyDamageToShip(
                ctx,
                volley.SourceEntityId,
                ref defender,
                new CombatDamage(
                    volley.HullDamage,
                    volley.SailDamage,
                    volley.CannonDamage,
                    volley.CrewDamage),
                tick,
                "broadside");
            if (defender.Hull == 0)
            {
                AppendEvent(ctx, volley.SourceEntityId, "enemy_sunk", $"entity_id={defender.EntityId}");
            }
            else
            {
                ApplyVolleyStatus(ctx, volley, defender, tick);
                AppendEvent(
                    ctx,
                    volley.SourceEntityId,
                    "broadside_impact",
                    $"entity_id={defender.EntityId},hull={appliedDamage.Hull},sails={appliedDamage.Sails},cannons={appliedDamage.Cannons},crew={appliedDamage.Crew}");
            }

            ctx.Db.Ship.EntityId.Update(defender);
            ctx.Db.Volley.VolleyId.Delete(volley.VolleyId);
        }
    }

    private static CombatDamage ApplyDamageToShip(
        ReducerContext ctx,
        ulong sourceEntityId,
        ref Ship defender,
        CombatDamage incoming,
        ulong tick,
        string cause)
    {
        if (tick < defender.InvulnerableUntilTick)
        {
            return new CombatDamage(0, 0, 0, 0);
        }

        var brace = HasActiveStatus(ctx, defender.EntityId, "brace", tick);
        var damage = new CombatDamage(
            TacticalRules.ApplyIncomingDamage(incoming.Hull, brace),
            TacticalRules.ApplyIncomingDamage(incoming.Sails, brace),
            TacticalRules.ApplyIncomingDamage(incoming.Cannons, brace),
            TacticalRules.ApplyIncomingDamage(incoming.Crew, brace));
        if (damage.Hull == 0 && damage.Sails == 0 &&
            damage.Cannons == 0 && damage.Crew == 0)
        {
            return damage;
        }

        InterruptActiveChannel(ctx, defender.EntityId, tick, cause);
        defender.Hull = WorldRules.ApplyDamage(defender.Hull, damage.Hull);
        defender.Sails = WorldRules.ApplyDamage(defender.Sails, damage.Sails);
        defender.Cannons = WorldRules.ApplyDamage(defender.Cannons, damage.Cannons);
        defender.Crew = WorldRules.ApplyDamage(defender.Crew, damage.Crew);
        SynchronizeDisabledSails(ctx, defender, tick);
        if (defender.Hull == 0)
        {
            defender.IsAlive = false;
            defender.IsActive = false;
            defender.IsMoving = false;
            defender.HasCourse = false;
            defender.IsStopping = false;
            ClearTargetLocks(ctx, defender.EntityId);
            if (sourceEntityId != 0)
            {
                defender.TargetEntityId = 0;
            }
        }

        return damage;
    }

    private static void ApplyVolleyStatus(
        ReducerContext ctx,
        Volley volley,
        Ship defender,
        ulong tick)
    {
        if (ctx.Db.AmmoDefinition.AmmoId.Find(volley.AmmoId) is not AmmoDefinition ammo ||
            ammo.AppliedStatus == "none")
        {
            return;
        }

        var chance = ammo.AppliedStatus == "flooding" ? 35u : 100u;
        if (!TacticalRules.ShouldApplyStatus(volley.VolleyId ^ defender.EntityId, chance))
        {
            return;
        }

        ApplyStatus(
            ctx,
            defender.EntityId,
            ammo.AppliedStatus,
            tick,
            TacticalRules.StatusDurationTicks,
            maximumStacks: 3);
    }

    private static void ClearTargetLocks(ReducerContext ctx, ulong targetEntityId)
    {
        foreach (var source in ctx.Db.Ship.ByTarget.Filter(targetEntityId))
        {
            var cleared = source;
            cleared.TargetEntityId = 0;
            cleared.IsEngaged = false;
            ctx.Db.Ship.EntityId.Update(cleared);
        }
    }

    private static ShipStatus? FindStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        string statusType)
    {
        foreach (var status in ctx.Db.ShipStatus.ByShip.Filter(shipEntityId))
        {
            if (status.StatusType == statusType)
            {
                return status;
            }
        }

        return null;
    }

    private static bool HasActiveStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        string statusType,
        ulong tick) =>
        FindStatus(ctx, shipEntityId, statusType) is ShipStatus status &&
        status.IsActive && tick < status.ExpiresAtTick;

    private static uint ActiveStatusStacks(
        ReducerContext ctx,
        ulong shipEntityId,
        string statusType,
        ulong tick) =>
        FindStatus(ctx, shipEntityId, statusType) is ShipStatus status &&
        status.IsActive && tick < status.ExpiresAtTick
            ? status.Stacks
            : 0;

    private static bool ApplyStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        string statusType,
        ulong tick,
        uint durationTicks,
        uint maximumStacks)
    {
        var existing = FindStatus(ctx, shipEntityId, statusType);
        var application = TacticalRules.ApplyStatus(
            existing is ShipStatus row
                ? new TacticalStatusState(
                    row.IsActive,
                    row.Stacks,
                    row.ExpiresAtTick,
                    row.ImmunityUntilTick)
                : new TacticalStatusState(false, 0, 0, 0),
            tick,
            durationTicks,
            maximumStacks);
        if (!application.Applied)
        {
            return false;
        }

        if (existing is ShipStatus current)
        {
            current.Stacks = application.State.Stacks;
            current.ExpiresAtTick = application.State.ExpiresAtTick;
            current.ImmunityUntilTick = application.State.ImmunityUntilTick;
            current.IsActive = true;
            ctx.Db.ShipStatus.StatusId.Update(current);
        }
        else
        {
            ctx.Db.ShipStatus.Insert(new ShipStatus
            {
                ShipEntityId = shipEntityId,
                StatusType = statusType,
                Stacks = application.State.Stacks,
                ExpiresAtTick = application.State.ExpiresAtTick,
                ImmunityUntilTick = application.State.ImmunityUntilTick,
                IsActive = true,
            });
        }

        AppendEvent(ctx, shipEntityId, "status_applied", $"status={statusType}");
        return true;
    }

    private static void DeactivateStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        string statusType,
        ulong tick)
    {
        if (FindStatus(ctx, shipEntityId, statusType) is not ShipStatus status ||
            !status.IsActive)
        {
            return;
        }

        status.IsActive = false;
        status.Stacks = 0;
        status.ImmunityUntilTick = tick + TacticalRules.StatusImmunityTicks;
        ctx.Db.ShipStatus.StatusId.Update(status);
    }

    private static void SynchronizeDisabledSails(
        ReducerContext ctx,
        Ship ship,
        ulong tick)
    {
        if (ship.Sails == 0)
        {
            if (FindStatus(ctx, ship.EntityId, "disabled_sails") is ShipStatus existing)
            {
                existing.IsActive = true;
                existing.Stacks = 1;
                existing.ExpiresAtTick = ulong.MaxValue;
                existing.ImmunityUntilTick = 0;
                ctx.Db.ShipStatus.StatusId.Update(existing);
            }
            else
            {
                ctx.Db.ShipStatus.Insert(new ShipStatus
                {
                    ShipEntityId = ship.EntityId,
                    StatusType = "disabled_sails",
                    Stacks = 1,
                    ExpiresAtTick = ulong.MaxValue,
                    ImmunityUntilTick = 0,
                    IsActive = true,
                });
            }
        }
        else
        {
            DeactivateStatus(ctx, ship.EntityId, "disabled_sails", tick);
        }
    }

    private static ShipChannel? FindActiveChannel(ReducerContext ctx, ulong shipEntityId) =>
        ctx.Db.ShipChannel.ShipEntityId.Find(shipEntityId) is ShipChannel channel &&
        channel.IsActive
            ? channel
            : null;

    private static void CancelChannel(
        ReducerContext ctx,
        ulong shipEntityId,
        string expectedType,
        string eventType)
    {
        if (FindActiveChannel(ctx, shipEntityId) is not ShipChannel channel ||
            channel.ChannelType != expectedType)
        {
            return;
        }

        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        AppendEvent(ctx, shipEntityId, eventType, "");
    }

    private static void InterruptActiveChannel(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong tick,
        string cause)
    {
        if (FindActiveChannel(ctx, shipEntityId) is not ShipChannel channel)
        {
            return;
        }

        if (channel.ChannelType == "boarding")
        {
            SetCooldown(
                ctx,
                shipEntityId,
                "boarding",
                tick + TacticalRules.BoardingCooldownTicks);
        }

        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        AppendEvent(
            ctx,
            shipEntityId,
            $"{channel.ChannelType}_interrupted",
            $"cause={cause}");
    }

    private static void InterruptBoarding(
        ReducerContext ctx,
        ulong shipEntityId,
        ulong tick,
        string eventType)
    {
        SetCooldown(
            ctx,
            shipEntityId,
            "boarding",
            tick + TacticalRules.BoardingCooldownTicks);
        ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        AppendEvent(ctx, shipEntityId, eventType, "");
    }

    private static Cooldown? FindCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        string cooldownType)
    {
        foreach (var cooldown in ctx.Db.Cooldown.ByShip.Filter(shipEntityId))
        {
            if (cooldown.CooldownType == cooldownType)
            {
                return cooldown;
            }
        }

        return null;
    }

    private static void SetCooldown(
        ReducerContext ctx,
        ulong shipEntityId,
        string cooldownType,
        ulong readyAtTick)
    {
        if (FindCooldown(ctx, shipEntityId, cooldownType) is Cooldown cooldown)
        {
            cooldown.ReadyAtTick = readyAtTick;
            ctx.Db.Cooldown.CooldownId.Update(cooldown);
            return;
        }

        ctx.Db.Cooldown.Insert(new Cooldown
        {
            ShipEntityId = shipEntityId,
            CooldownType = cooldownType,
            ReadyAtTick = readyAtTick,
        });
    }

    private static void AddInventory(
        ReducerContext ctx,
        ulong shipEntityId,
        string itemId,
        uint quantity)
    {
        if (FindInventory(ctx, shipEntityId, itemId) is Inventory existing)
        {
            existing.Quantity = checked(existing.Quantity + quantity);
            ctx.Db.Inventory.InventoryId.Update(existing);
            return;
        }

        ctx.Db.Inventory.Insert(new Inventory
        {
            ShipEntityId = shipEntityId,
            ItemId = itemId,
            Quantity = quantity,
        });
    }

    private static (bool InStorm, bool InShoal, TacticalModifiers Modifiers) HazardsAt(
        ReducerContext ctx,
        float x,
        float y)
    {
        var inStorm = false;
        var inShoal = false;
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (!worldObject.IsActive ||
                !WorldRules.IsInRange(
                    x,
                    y,
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius))
            {
                continue;
            }

            inStorm |= worldObject.Kind == "storm";
            inShoal |= worldObject.Kind == "shoal";
        }

        return (
            inStorm,
            inShoal,
            TacticalRules.MovementModifiers(
                fullSail: false,
                slowedStacks: 0,
                sailsDisabled: false,
                sailIntegrity: 1f,
                inShoal,
                inStorm,
                repairing: false));
    }

    private static CombatDamage ScaleCombatDamage(CombatDamage damage, float multiplier) =>
        new(
            ScaleDamage(damage.Hull, multiplier),
            ScaleDamage(damage.Sails, multiplier),
            ScaleDamage(damage.Cannons, multiplier),
            ScaleDamage(damage.Crew, multiplier));

    private static uint ScaleDamage(uint damage, float multiplier) =>
        damage == 0
            ? 0
            : (uint)MathF.Round(damage * multiplier, MidpointRounding.AwayFromZero);

    private static Inventory? FindInventory(ReducerContext ctx, ulong shipEntityId, string itemId)
    {
        foreach (var item in ctx.Db.Inventory.ByShip.Filter(shipEntityId))
        {
            if (item.ItemId == itemId)
            {
                return item;
            }
        }

        return null;
    }

    private static string FireRejectionMessage(FireRejection rejection) => rejection switch
    {
        FireRejection.SourceSunk => "A sunk ship cannot fire.",
        FireRejection.NoTarget => "Select a target before firing.",
        FireRejection.TargetSunk => "The selected target has already sunk.",
        FireRejection.CannonsDisabled => "The ship's cannons are disabled.",
        FireRejection.NoAmmunition => "No selected ammunition remains.",
        FireRejection.Reloading => "That broadside is still reloading.",
        FireRejection.OutOfRange => "The selected target is out of range.",
        FireRejection.OutsideArc => "The selected target is outside that broadside arc.",
        FireRejection.Busy => "Repair or boarding must finish before firing.",
        _ => "The broadside cannot fire.",
    };

    private static string AbilityRejectionMessage(AbilityRejection rejection) => rejection switch
    {
        AbilityRejection.SourceSunk => "A sunk ship cannot use abilities.",
        AbilityRejection.UnknownAbility => "That ability does not exist.",
        AbilityRejection.Cooldown => "That ability is still cooling down.",
        AbilityRejection.Busy => "Finish the active channel before using an ability.",
        _ => "The ability cannot be activated.",
    };

    private static string RepairRejectionMessage(RepairRejection rejection) => rejection switch
    {
        RepairRejection.SourceSunk => "A sunk ship cannot be repaired.",
        RepairRejection.Busy => "Another channel is already active.",
        RepairRejection.NoRepairKit => "No repair kits remain.",
        RepairRejection.NothingToRepair => "The ship does not need repairs.",
        _ => "Repair cannot start.",
    };

    private static string BoardingRejectionMessage(BoardingRejection rejection) => rejection switch
    {
        BoardingRejection.SourceSunk => "A sunk ship cannot board.",
        BoardingRejection.TargetSunk => "Select a living target before boarding.",
        BoardingRejection.Busy => "Another channel is already active.",
        BoardingRejection.TargetTooStrong => "The target must be below 25% hull.",
        BoardingRejection.OutOfRange => "Move within boarding range.",
        BoardingRejection.Cooldown => "Boarding is still cooling down.",
        _ => "Boarding cannot start.",
    };

    private static void ExpireTransientRows(ReducerContext ctx, ulong tick)
    {
        foreach (var gameEvent in ctx.Db.CombatEvent.ByActive.Filter(true))
        {
            if (EventRetentionRules.IsExpired(gameEvent.ExpiresAtTick, tick))
            {
                ctx.Db.CombatEvent.EventId.Delete(gameEvent.EventId);
            }
        }

        foreach (var loot in ctx.Db.Loot.ByActive.Filter(true))
        {
            if (EventRetentionRules.IsExpired(loot.ExpiresAtTick, tick))
            {
                ctx.Db.Loot.LootId.Delete(loot.LootId);
            }
        }
    }

    private static void AppendEvent(
        ReducerContext ctx,
        ulong ownerEntityId,
        string eventType,
        string details)
    {
        var tick = ctx.Db.WorldState.Id.Find(1)?.Tick ?? 0;
        ctx.Db.CombatEvent.Insert(new CombatEvent
        {
            OwnerEntityId = ownerEntityId,
            EventType = eventType,
            Details = details,
            Tick = tick,
            ExpiresAtTick = tick + EventRetentionRules.LifetimeTicks,
            IsActive = true,
        });
    }

    private static void SeedPlayerInventory(ReducerContext ctx, ulong shipEntityId)
    {
        foreach (var ammunition in ContentCatalog.CreateDefault().Ammunition)
        {
            ctx.Db.Inventory.Insert(new Inventory
            {
                ShipEntityId = shipEntityId,
                ItemId = ammunition.Id,
                Quantity = 100,
            });
        }

        ctx.Db.Inventory.Insert(new Inventory
        {
            ShipEntityId = shipEntityId,
            ItemId = "repair_kit",
            Quantity = 10,
        });
    }

    private static void SeedContent(ReducerContext ctx)
    {
        var content = ContentCatalog.CreateDefault();
        var errors = ContentCatalog.Validate(content);
        if (errors.Count != 0)
        {
            throw new Exception(string.Join(" ", errors));
        }

        foreach (var ammunition in content.Ammunition)
        {
            ctx.Db.AmmoDefinition.Insert(new AmmoDefinition
            {
                AmmoId = ammunition.Id,
                HullDamage = ammunition.HullDamage,
                SailDamage = ammunition.SailDamage,
                CannonDamage = ammunition.CannonDamage,
                CrewDamage = ammunition.CrewDamage,
                RangeMultiplier = ammunition.RangeMultiplier,
                AppliedStatus = ammunition.AppliedStatus,
            });
        }

        foreach (var ability in content.Abilities)
        {
            ctx.Db.AbilityDefinition.Insert(new AbilityDefinition
            {
                AbilityId = ability.Id,
                CooldownTicks = ability.CooldownTicks,
                DurationTicks = ability.DurationTicks,
            });
        }

        foreach (var npc in content.Npcs)
        {
            ctx.Db.NpcDefinition.Insert(new NpcDefinition
            {
                NpcId = npc.Id,
                AggroRange = npc.AggroRange,
                DesiredRange = npc.DesiredRange,
                Hull = npc.Hull,
            });
        }

        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 1, RequiredExperience = 0 });
        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 2, RequiredExperience = 500 });
        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 3, RequiredExperience = 1_500 });
    }

    private static void SeedWorld(ReducerContext ctx)
    {
        InsertWorldObject(ctx, 1, "harbor", 0f, 0f, 8f, false);
        InsertWorldObject(ctx, 2, "island", 35f, 20f, 12f, true);
        InsertWorldObject(ctx, 3, "reef", -30f, -25f, 10f, true);
        InsertWorldObject(ctx, 4, "island", -46f, 43f, 16f, true);
        InsertWorldObject(ctx, 5, "island", 61f, -48f, 15f, true);
        InsertWorldObject(ctx, 6, "island", -63f, -58f, 11f, true);
        InsertWorldObject(ctx, 7, "island", 4f, 70f, 9f, true);
        InsertWorldObject(ctx, 8, "reef", 24f, -61f, 8f, true);
        InsertWorldObject(ctx, 9, "reef", 68f, 58f, 9f, true);
        InsertWorldObject(ctx, 11, "shoal", -4f, -42f, 15f, false, intensity: 0.7f);
        InsertWorldObject(ctx, 12, "shoal", 48f, 45f, 12f, false, intensity: 0.8f);
        InsertWorldObject(
            ctx,
            13,
            "storm",
            -72f,
            3f,
            14f,
            false,
            directionDegrees: 72f,
            movementSpeed: 1.5f,
            intensity: 1f);

        var trainingShip = CreateShip(10, "patrol", "npc", 45f, -10f);
        trainingShip.Hull = WorldRules.EnemyInitialHealth;
        trainingShip.MaxHull = WorldRules.EnemyInitialHealth;
        ctx.Db.Ship.Insert(trainingShip);
        ctx.Db.NpcAi.Insert(new NpcAi
        {
            ShipEntityId = trainingShip.EntityId,
            ArchetypeId = "patrol",
            IsActive = true,
            NextDecisionTick = 0,
            HomeSeed = 10,
        });
    }

    private static void SeedEnvironment(ReducerContext ctx)
    {
        const ulong seed = 0x5EA2026;
        var wind = EnvironmentRules.WindForEpoch(seed, 0);
        ctx.Db.EnvironmentState.Insert(new EnvironmentState
        {
            Id = 1,
            Seed = seed,
            WindEpoch = 0,
            WindDirectionDegrees = wind.DirectionDegrees,
            WindStrength = wind.Strength,
            NextWindChangeTick = EnvironmentRules.WindEpochTicks,
        });
        InsertCurrentZone(ctx, 1, -55f, 35f, 28f, 70f, 1.25f);
        InsertCurrentZone(ctx, 2, 55f, -45f, 24f, 235f, 1f);
    }

    private static void InsertCurrentZone(
        ReducerContext ctx,
        ulong zoneId,
        float x,
        float y,
        float radius,
        float directionDegrees,
        float strength)
    {
        ctx.Db.CurrentZone.Insert(new CurrentZone
        {
            ZoneId = zoneId,
            PositionX = x,
            PositionY = y,
            Radius = radius,
            DirectionDegrees = directionDegrees,
            Strength = strength,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            IsActive = true,
        });
    }

    private static SpawnPoint FindSafeSpawn(ReducerContext ctx, ulong seed)
    {
        var blockers = new List<SpawnBlocker>();
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (worldObject.IsActive && worldObject.BlocksMovement)
            {
                blockers.Add(new SpawnBlocker(
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius));
            }
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (ship.IsAlive)
            {
                blockers.Add(new SpawnBlocker(ship.PositionX, ship.PositionY, 4f));
            }
        }

        if (!SpawnRules.TryFindSafePosition(seed, blockers, out var point))
        {
            throw new Exception("No safe player spawn is available.");
        }

        return point;
    }

    private static List<NavigationBlocker> NavigationBlockers(ReducerContext ctx)
    {
        var blockers = new List<NavigationBlocker>();
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (worldObject.IsActive && worldObject.BlocksMovement)
            {
                blockers.Add(new NavigationBlocker(
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius));
            }
        }

        return blockers;
    }

    private static void ConfigureNavigationWaypoint(
        ref Ship ship,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        ship.HasWaypoint = NavigationRules.TryFindDetour(
            ship.PositionX,
            ship.PositionY,
            ship.DestinationX,
            ship.DestinationY,
            blockers,
            out var waypoint);
        ship.WaypointX = ship.HasWaypoint ? waypoint.X : ship.DestinationX;
        ship.WaypointY = ship.HasWaypoint ? waypoint.Y : ship.DestinationY;
    }

    private static ulong IdentitySeed(Identity identity)
    {
        var seed = 1469598103934665603UL;
        foreach (var character in identity.ToString())
        {
            seed ^= character;
            seed = unchecked(seed * 1099511628211UL);
        }

        return seed;
    }

    private static void InsertWorldObject(
        ReducerContext ctx,
        ulong entityId,
        string kind,
        float x,
        float y,
        float radius,
        bool blocksMovement,
        float directionDegrees = 0f,
        float movementSpeed = 0f,
        float intensity = 0f)
    {
        ctx.Db.WorldObject.Insert(new WorldObject
        {
            EntityId = entityId,
            Kind = kind,
            PositionX = x,
            PositionY = y,
            Radius = radius,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            IsActive = true,
            BlocksMovement = blocksMovement,
            DirectionDegrees = directionDegrees,
            MovementSpeed = movementSpeed,
            Intensity = intensity,
        });
    }
}
