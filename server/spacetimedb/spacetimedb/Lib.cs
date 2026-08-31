using SpacetimeDB;
using Sea.Server;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "WorldState", Public = true)]
    public partial struct WorldState
    {
        [PrimaryKey]
        public uint Id;
        public ulong Tick;
        public uint TickRateHz;
    }

    [SpacetimeDB.Table(Accessor = "SimulationTimer", Scheduled = "RunSimulationTick", ScheduledAt = "ScheduledAt")]
    public partial struct SimulationTimer
    {
        [PrimaryKey]
        [AutoInc]
        public ulong ScheduledId;
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Table(Accessor = "PlayerIdentity", Public = true)]
    public partial struct PlayerIdentity
    {
        [PrimaryKey]
        public Identity Owner;
        public bool IsConnected;
    }

    [SpacetimeDB.Table(Accessor = "PlayerShip", Public = true)]
    public partial struct PlayerShip
    {
        [PrimaryKey]
        public Identity Owner;
        public float PositionX;
        public float PositionY;
        public float DestinationX;
        public float DestinationY;
        public bool IsMoving;
        public uint Health;
        public ulong SelectedTargetId;
        public bool HasSelectedTarget;
        public bool IsEngaged;
        public uint CannonDamage;
        public uint CannonCooldownTicks;
        public ulong NextCannonAttackTick;
    }

    [SpacetimeDB.Table(Accessor = "NpcShip", Public = true)]
    public partial struct NpcShip
    {
        [PrimaryKey]
        public ulong EntityId;
        public float PositionX;
        public float PositionY;
        public uint Health;
        public uint MaxHealth;
        public uint CannonDamage;
        public uint CannonCooldownTicks;
        public ulong NextAttackTick;
        public uint GoldReward;
        public bool IsActive;
    }

    [SpacetimeDB.Table(Accessor = "MapEntity", Public = true)]
    public partial struct MapEntity
    {
        [PrimaryKey]
        public ulong EntityId;
        public string Kind;
        public float PositionX;
        public float PositionY;
        public float InteractionRadius;
        public bool IsTargetable;
        public bool IsActive;
        public bool BlocksMovement;
    }

    [SpacetimeDB.Table(Accessor = "ResourceBalance", Public = true)]
    public partial struct ResourceBalance
    {
        [PrimaryKey]
        public Identity Owner;
        public uint Gold;
    }

    [SpacetimeDB.Table(Accessor = "PlayerProgression", Public = true)]
    public partial struct PlayerProgression
    {
        [PrimaryKey]
        public Identity Owner;
        public uint Level;
        public uint CannonUpgradeLevel;
    }

    [SpacetimeDB.Table(Accessor = "GameEvent", Public = true)]
    public partial struct GameEvent
    {
        [PrimaryKey]
        [AutoInc]
        public ulong EventId;
        public Identity Owner;
        public string EventType;
        public string Details;
        public ulong Tick;
    }

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        if (!HasWorldState(ctx))
        {
            ctx.Db.WorldState.Insert(new WorldState
            {
                Id = 1,
                Tick = 0,
                TickRateHz = WorldRules.TickRateHz,
            });

            SeedMap(ctx);
            ctx.Db.SimulationTimer.Insert(new SimulationTimer
            {
                ScheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(1000d / WorldRules.TickRateHz)),
            });
        }
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx) => EnsurePlayer(ctx, ctx.Sender, true);

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        foreach (var player in ctx.Db.PlayerIdentity.Iter())
        {
            if (player.Owner == ctx.Sender)
            {
                var disconnected = player;
                disconnected.IsConnected = false;
                ctx.Db.PlayerIdentity.Owner.Update(disconnected);
                return;
            }
        }
    }

    [SpacetimeDB.Reducer]
    public static void LoadPlayer(ReducerContext ctx)
    {
        EnsurePlayer(ctx, ctx.Sender, true);
    }

    [SpacetimeDB.Reducer]
    public static void MoveTo(ReducerContext ctx, float x, float y)
    {
        if (!WorldRules.IsValidMove(x, y))
        {
            throw new Exception("The requested position is outside the map.");
        }

        foreach (var entity in ctx.Db.MapEntity.Iter())
        {
            if (entity.IsActive && entity.BlocksMovement && WorldRules.IsBlocked(entity.Kind, entity.PositionX, entity.PositionY, entity.InteractionRadius, x, y))
            {
                throw new Exception("The requested position is blocked by map geometry.");
            }
        }

        var ship = FindShip(ctx, ctx.Sender);
        ship.DestinationX = x;
        ship.DestinationY = y;
        ship.IsMoving = ship.PositionX != x || ship.PositionY != y;
        ctx.Db.PlayerShip.Owner.Update(ship);
        AppendEvent(ctx, ctx.Sender, "move_to", $"x={x:0.###},y={y:0.###}");
    }

    [SpacetimeDB.Reducer]
    public static void SelectTarget(ReducerContext ctx, ulong entityId)
    {
        var entity = FindEntity(ctx, entityId);
        if (!entity.IsActive || !entity.IsTargetable)
        {
            throw new Exception("The selected entity cannot be targeted.");
        }

        var ship = FindShip(ctx, ctx.Sender);
        ship.SelectedTargetId = entityId;
        ship.HasSelectedTarget = true;
        ship.IsEngaged = false;
        ctx.Db.PlayerShip.Owner.Update(ship);
        AppendEvent(ctx, ctx.Sender, "select_target", $"entity_id={entityId}");
    }

    [SpacetimeDB.Reducer]
    public static void Engage(ReducerContext ctx)
    {
        var ship = FindShip(ctx, ctx.Sender);
        if (!ship.HasSelectedTarget)
        {
            throw new Exception("Select a target before engaging.");
        }

        var entity = FindEntity(ctx, ship.SelectedTargetId);
        if (!entity.IsActive || !entity.IsTargetable)
        {
            throw new Exception("The selected entity cannot be engaged.");
        }

        var npc = FindNpcShip(ctx, entity.EntityId);
        if (!npc.IsActive)
        {
            throw new Exception("The selected enemy is no longer active.");
        }

        ship.IsEngaged = true;
        ctx.Db.PlayerShip.Owner.Update(ship);
        AppendEvent(ctx, ctx.Sender, "engage", $"entity_id={entity.EntityId}");
    }

    [SpacetimeDB.Reducer]
    public static void UpgradeCannon(ReducerContext ctx)
    {
        var progression = FindProgression(ctx, ctx.Sender);
        var cost = WorldRules.CannonUpgradeCost(progression.CannonUpgradeLevel);
        var balance = FindBalance(ctx, ctx.Sender);
        if (balance.Gold < cost)
        {
            throw new Exception("The player cannot afford this cannon upgrade.");
        }

        var updatedBalance = balance;
        updatedBalance.Gold -= cost;
        ctx.Db.ResourceBalance.Owner.Update(updatedBalance);

        var updatedProgression = progression;
        updatedProgression.CannonUpgradeLevel++;
        ctx.Db.PlayerProgression.Owner.Update(updatedProgression);

        var ship = FindShip(ctx, ctx.Sender);
        var upgradedShip = ship;
        upgradedShip.CannonDamage += WorldRules.CannonDamagePerUpgrade;
        ctx.Db.PlayerShip.Owner.Update(upgradedShip);

        AppendEvent(ctx, ctx.Sender, "cannon_upgraded", $"level={updatedProgression.CannonUpgradeLevel},cost={cost}");
    }

    [SpacetimeDB.Reducer]
    public static void RunSimulationTick(ReducerContext ctx, SimulationTimer timer)
    {
        foreach (var world in ctx.Db.WorldState.Iter())
        {
            var next = world;
            next.Tick++;
            ctx.Db.WorldState.Id.Update(next);
            AdvancePlayerShips(ctx);
            ResolveCombat(ctx, next.Tick);
            return;
        }
    }

    private static bool HasWorldState(ReducerContext ctx)
    {
        foreach (var _ in ctx.Db.WorldState.Iter())
        {
            return true;
        }

        return false;
    }

    private static void EnsurePlayer(ReducerContext ctx, Identity owner, bool connected)
    {
        foreach (var player in ctx.Db.PlayerIdentity.Iter())
        {
            if (player.Owner == owner)
            {
                var existing = player;
                existing.IsConnected = connected;
                ctx.Db.PlayerIdentity.Owner.Update(existing);
                EnsureProgression(ctx, owner);
                return;
            }
        }

        ctx.Db.PlayerIdentity.Insert(new PlayerIdentity
        {
            Owner = owner,
            IsConnected = connected,
        });
        ctx.Db.PlayerShip.Insert(new PlayerShip
        {
            Owner = owner,
            PositionX = 0,
            PositionY = 0,
            DestinationX = 0,
            DestinationY = 0,
            IsMoving = false,
            Health = WorldRules.InitialHealth,
            SelectedTargetId = 0,
            HasSelectedTarget = false,
            IsEngaged = false,
            CannonDamage = WorldRules.InitialCannonDamage,
            CannonCooldownTicks = WorldRules.InitialCannonCooldownTicks,
            NextCannonAttackTick = 0,
        });
        ctx.Db.ResourceBalance.Insert(new ResourceBalance
        {
            Owner = owner,
            Gold = WorldRules.InitialGold,
        });
        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = owner,
            Level = WorldRules.InitialProgressionLevel,
            CannonUpgradeLevel = WorldRules.InitialCannonUpgradeLevel,
        });
    }

    private static PlayerShip FindShip(ReducerContext ctx, Identity owner)
    {
        foreach (var ship in ctx.Db.PlayerShip.Iter())
        {
            if (ship.Owner == owner)
            {
                return ship;
            }
        }

        throw new Exception("Player has not been loaded.");
    }

    private static MapEntity FindEntity(ReducerContext ctx, ulong entityId)
    {
        foreach (var entity in ctx.Db.MapEntity.Iter())
        {
            if (entity.EntityId == entityId)
            {
                return entity;
            }
        }

        throw new Exception("The requested map entity does not exist.");
    }

    private static NpcShip FindNpcShip(ReducerContext ctx, ulong entityId)
    {
        foreach (var npc in ctx.Db.NpcShip.Iter())
        {
            if (npc.EntityId == entityId)
            {
                return npc;
            }
        }

        throw new Exception("The selected entity is not an enemy ship.");
    }

    private static ResourceBalance FindBalance(ReducerContext ctx, Identity owner)
    {
        foreach (var balance in ctx.Db.ResourceBalance.Iter())
        {
            if (balance.Owner == owner)
            {
                return balance;
            }
        }

        throw new Exception("Player resource balance is missing.");
    }

    private static PlayerProgression FindProgression(ReducerContext ctx, Identity owner)
    {
        foreach (var progression in ctx.Db.PlayerProgression.Iter())
        {
            if (progression.Owner == owner)
            {
                return progression;
            }
        }

        throw new Exception("Player progression is missing.");
    }

    private static void EnsureProgression(ReducerContext ctx, Identity owner)
    {
        foreach (var progression in ctx.Db.PlayerProgression.Iter())
        {
            if (progression.Owner == owner)
            {
                return;
            }
        }

        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = owner,
            Level = WorldRules.InitialProgressionLevel,
            CannonUpgradeLevel = WorldRules.InitialCannonUpgradeLevel,
        });
    }

    private static void ResolveCombat(ReducerContext ctx, ulong tick)
    {
        foreach (var ship in ctx.Db.PlayerShip.Iter())
        {
            if (!ship.IsEngaged || !ship.HasSelectedTarget)
            {
                continue;
            }

            var npc = FindNpcShip(ctx, ship.SelectedTargetId);
            if (!npc.IsActive)
            {
                var disengaged = ship;
                disengaged.IsEngaged = false;
                ctx.Db.PlayerShip.Owner.Update(disengaged);
                continue;
            }

            var updatedShip = ship;
            var shipChanged = false;
            if (WorldRules.IsInRange(ship.PositionX, ship.PositionY, npc.PositionX, npc.PositionY, WorldRules.CannonRange) && tick >= ship.NextCannonAttackTick)
            {
                updatedShip.NextCannonAttackTick = tick + ship.CannonCooldownTicks;
                shipChanged = true;

                var damagedNpc = npc;
                damagedNpc.Health = WorldRules.ApplyDamage(npc.Health, ship.CannonDamage);
                ctx.Db.NpcShip.EntityId.Update(damagedNpc);
                npc = damagedNpc;
                AppendEvent(ctx, ship.Owner, "cannon_hit", $"entity_id={npc.EntityId},damage={ship.CannonDamage}");

                if (damagedNpc.Health == 0)
                {
                    damagedNpc.IsActive = false;
                    ctx.Db.NpcShip.EntityId.Update(damagedNpc);

                    var entity = FindEntity(ctx, npc.EntityId);
                    var inactiveEntity = entity;
                    inactiveEntity.IsActive = false;
                    ctx.Db.MapEntity.EntityId.Update(inactiveEntity);

                    updatedShip.IsEngaged = false;
                    shipChanged = true;
                    AppendEvent(ctx, ship.Owner, "enemy_sunk", $"entity_id={npc.EntityId}");

                    var balance = FindBalance(ctx, ship.Owner);
                    var rewardedBalance = balance;
                    rewardedBalance.Gold += npc.GoldReward;
                    ctx.Db.ResourceBalance.Owner.Update(rewardedBalance);
                    AppendEvent(ctx, ship.Owner, "reward_granted", $"gold={npc.GoldReward}");
                }
            }

            if (npc.IsActive && WorldRules.IsInRange(ship.PositionX, ship.PositionY, npc.PositionX, npc.PositionY, WorldRules.CannonRange) && tick >= npc.NextAttackTick)
            {
                updatedShip.Health = WorldRules.ApplyDamage(ship.Health, npc.CannonDamage);
                shipChanged = true;

                var attackingNpc = npc;
                attackingNpc.NextAttackTick = tick + npc.CannonCooldownTicks;
                ctx.Db.NpcShip.EntityId.Update(attackingNpc);
                AppendEvent(ctx, ship.Owner, "enemy_cannon_hit", $"entity_id={npc.EntityId},damage={npc.CannonDamage}");

                if (updatedShip.Health == 0)
                {
                    updatedShip.IsEngaged = false;
                    AppendEvent(ctx, ship.Owner, "player_sunk", $"entity_id={npc.EntityId}");
                }
            }

            if (shipChanged)
            {
                ctx.Db.PlayerShip.Owner.Update(updatedShip);
            }
        }
    }

    private static void AdvancePlayerShips(ReducerContext ctx)
    {
        var distancePerTick = WorldRules.PlayerShipSpeed / WorldRules.TickRateHz;
        foreach (var ship in ctx.Db.PlayerShip.Iter())
        {
            if (!ship.IsMoving)
            {
                continue;
            }

            var step = WorldRules.AdvanceTowards(
                ship.PositionX,
                ship.PositionY,
                ship.DestinationX,
                ship.DestinationY,
                distancePerTick);
            var moved = ship;
            moved.PositionX = step.X;
            moved.PositionY = step.Y;
            moved.IsMoving = !step.Arrived;
            ctx.Db.PlayerShip.Owner.Update(moved);
        }
    }

    private static void SeedMap(ReducerContext ctx)
    {
        ctx.Db.MapEntity.Insert(new MapEntity
        {
            EntityId = 1,
            Kind = "harbor",
            PositionX = 0,
            PositionY = 0,
            InteractionRadius = 8,
            IsTargetable = false,
            IsActive = true,
            BlocksMovement = false,
        });
        ctx.Db.NpcShip.Insert(new NpcShip
        {
            EntityId = 10,
            PositionX = 45,
            PositionY = -10,
            Health = WorldRules.EnemyInitialHealth,
            MaxHealth = WorldRules.EnemyInitialHealth,
            CannonDamage = WorldRules.EnemyCannonDamage,
            CannonCooldownTicks = WorldRules.EnemyCannonCooldownTicks,
            NextAttackTick = 0,
            GoldReward = WorldRules.EnemyGoldReward,
            IsActive = true,
        });
        ctx.Db.MapEntity.Insert(new MapEntity
        {
            EntityId = 2,
            Kind = "island",
            PositionX = 35,
            PositionY = 20,
            InteractionRadius = 12,
            IsTargetable = false,
            IsActive = true,
            BlocksMovement = true,
        });
        ctx.Db.MapEntity.Insert(new MapEntity
        {
            EntityId = 3,
            Kind = "reef",
            PositionX = -30,
            PositionY = -25,
            InteractionRadius = 10,
            IsTargetable = false,
            IsActive = true,
            BlocksMovement = true,
        });
        ctx.Db.MapEntity.Insert(new MapEntity
        {
            EntityId = 10,
            Kind = "training_target",
            PositionX = 45,
            PositionY = -10,
            InteractionRadius = 15,
            IsTargetable = true,
            IsActive = true,
            BlocksMovement = false,
        });
    }

    private static void AppendEvent(ReducerContext ctx, Identity owner, string eventType, string details)
    {
        var tick = 0UL;
        foreach (var world in ctx.Db.WorldState.Iter())
        {
            tick = world.Tick;
            break;
        }

        ctx.Db.GameEvent.Insert(new GameEvent
        {
            Owner = owner,
            EventType = eventType,
            Details = details,
            Tick = tick,
        });
    }
}
