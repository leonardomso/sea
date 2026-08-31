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
            ContentVersion = 1,
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
                throw new Exception("The requested position is blocked by map geometry.");
            }
        }

        var ship = FindPlayerShip(ctx, ctx.Sender);
        ship.DestinationX = x;
        ship.DestinationY = y;
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
    public static void Engage(ReducerContext ctx)
    {
        var ship = FindPlayerShip(ctx, ctx.Sender);
        if (ship.TargetEntityId == 0)
        {
            throw new Exception("Select a target before engaging.");
        }

        var target = FindShip(ctx, ship.TargetEntityId);
        if (!target.IsActive || !target.IsAlive)
        {
            throw new Exception("The selected enemy is no longer active.");
        }

        ship.IsEngaged = true;
        ctx.Db.Ship.EntityId.Update(ship);
        AppendEvent(ctx, ship.EntityId, "engage", $"entity_id={target.EntityId}");
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
        AdvanceMovingShips(ctx);
        ResolvePrototypeCombat(ctx, world.Tick);
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
            HeadingDegrees = 0f,
            Speed = 0f,
            MaximumSpeed = WorldRules.PlayerShipSpeed,
            Acceleration = 3f,
            Deceleration = 4f,
            TurnRateDegrees = 55f,
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
        var environment = ctx.Db.EnvironmentState.Id.Find(1);
        foreach (var ship in ctx.Db.Ship.ByMoving.Filter(true))
        {
            if (!ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var windMultiplier = environment is EnvironmentState wind
                ? EnvironmentRules.WindSpeedMultiplier(
                    ship.HeadingDegrees,
                    wind.WindDirectionDegrees,
                    wind.WindStrength)
                : 1f;
            var step = SailingRules.Step(
                new SailingState(
                    ship.PositionX,
                    ship.PositionY,
                    ship.HeadingDegrees,
                    ship.Speed),
                ship.DestinationX,
                ship.DestinationY,
                ship.IsStopping,
                new SailingParameters(
                    ship.MaximumSpeed * windMultiplier,
                    ship.Acceleration,
                    ship.Deceleration,
                    ship.TurnRateDegrees),
                deltaSeconds);
            var current = CurrentVelocityAt(ctx, step.PositionX, step.PositionY);
            var nextX = step.PositionX + current.X * deltaSeconds;
            var nextY = step.PositionY + current.Y * deltaSeconds;
            var moved = ship;
            moved.HeadingDegrees = step.HeadingDegrees;
            moved.Speed = step.Speed;
            moved.IsMoving = step.IsMoving;
            moved.HasCourse = ship.HasCourse && !step.Arrived;
            moved.IsStopping = ship.IsStopping && step.Speed > 0f;
            if (IsNavigablePosition(ctx, ship.EntityId, nextX, nextY))
            {
                moved.PositionX = Math.Clamp(nextX, WorldRules.MapMin, WorldRules.MapMax);
                moved.PositionY = Math.Clamp(nextY, WorldRules.MapMin, WorldRules.MapMax);
            }
            else
            {
                moved.HasCourse = false;
                moved.IsStopping = true;
                moved.Speed = MathF.Max(0f, ship.Speed - ship.Deceleration * deltaSeconds);
                moved.IsMoving = moved.Speed > 0f;
                moved.DestinationX = ship.PositionX;
                moved.DestinationY = ship.PositionY;
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

    private static void ResolvePrototypeCombat(ReducerContext ctx, ulong tick)
    {
        foreach (var ship in ctx.Db.Ship.ByEngaged.Filter(true))
        {
            if (!ship.IsActive || !ship.IsAlive || ship.TargetEntityId == 0)
            {
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(ship.TargetEntityId) is not Ship target ||
                !target.IsActive || !target.IsAlive)
            {
                var disengaged = ship;
                disengaged.IsEngaged = false;
                disengaged.TargetEntityId = 0;
                ctx.Db.Ship.EntityId.Update(disengaged);
                continue;
            }

            if (!WorldRules.IsInRange(
                    ship.PositionX,
                    ship.PositionY,
                    target.PositionX,
                    target.PositionY,
                    WorldRules.CannonRange) ||
                tick < ship.NextPortFireTick)
            {
                continue;
            }

            var attacker = ship;
            attacker.NextPortFireTick = tick + ship.CannonCooldownTicks;
            var defender = target;
            defender.Hull = WorldRules.ApplyDamage(target.Hull, ship.CannonDamage);
            if (defender.Hull == 0)
            {
                defender.IsAlive = false;
                defender.IsActive = false;
                defender.IsMoving = false;
                attacker.IsEngaged = false;
                attacker.TargetEntityId = 0;
                AppendEvent(ctx, attacker.EntityId, "enemy_sunk", $"entity_id={defender.EntityId}");
            }
            else
            {
                AppendEvent(ctx, attacker.EntityId, "cannon_hit", $"entity_id={defender.EntityId},damage={ship.CannonDamage}");
            }

            ctx.Db.Ship.EntityId.Update(defender);
            ctx.Db.Ship.EntityId.Update(attacker);
        }
    }

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
        bool blocksMovement)
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
        });
    }
}
