using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
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
            ContentVersion = 4,
        });
        ctx.Db.SimulationClock.Insert(new SimulationClock
        {
            Id = 1,
            Tick = 0,
            NextEntityId = 1000,
        });
        ctx.Db.SimulationTelemetry.Insert(new SimulationTelemetry { Id = 1 });
        ctx.Db.SimulationDispatchState.Insert(new SimulationDispatchState { Id = 1 });
        ctx.Db.MovementSnapshotDispatchState.Insert(
            new MovementSnapshotDispatchState { Id = 1 });
        ctx.Db.HazardDispatchState.Insert(new HazardDispatchState { Id = 1 });
        SeedContent(ctx);
        SeedWorld(ctx);
        SeedNavigationField(ctx);
        SeedEnvironment(ctx);
        ScheduleSimulationSystems(ctx);
    }

    private static void ScheduleSimulationSystems(ReducerContext ctx)
    {
        ctx.Db.SimulationDispatchTimer.Insert(new SimulationDispatchTimer
        {
            ScheduledAt = new ScheduleAt.Interval(
                TimeSpan.FromMilliseconds(
                    SimulationWorkRules.DispatchIntervalMilliseconds(false))),
        });
        ctx.Db.MovementSnapshotDispatchTimer.Insert(new MovementSnapshotDispatchTimer
        {
            ScheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(
                SimulationWorkRules.SnapshotIntervalMilliseconds(false))),
        });
        ctx.Db.HazardDispatchTimer.Insert(new HazardDispatchTimer
        {
            ScheduledAt = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(
                SimulationWorkRules.HazardIntervalMilliseconds(false))),
        });
        for (byte shardId = 0; shardId < SimulationWorkRules.MovementShardCount; shardId++)
        {
            ctx.Db.MovementShardState.Insert(new MovementShardState
            {
                ShardId = shardId,
                LastSimulatedTick = 0,
                Ships = [],
            });
        }
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
            SetLoadedConnectionState(ctx, ref ownership, true);
            EnsureProgression(ctx, ctx.Sender);
            EnsureCommandState(ctx, ctx.Sender, ownership.ShipEntityId);
            SynchronizePlayerClock(ctx, ctx.Sender);
            return;
        }

        var entityId = AllocateEntityId(ctx);
        var spawn = FindSafeSpawn(ctx, IdentitySeed(ctx.Sender));
        var ship = CreateShip(entityId, "player_sloop", "player", spawn.X, spawn.Y);
        ctx.Db.Ship.Insert(ship);
        InsertShipMovement(ctx, ship);
        ctx.Db.PlayerOwnership.Insert(new PlayerOwnership
        {
            Owner = ctx.Sender,
            ShipEntityId = entityId,
            IsConnected = true,
        });
        AdjustConnectedPlayerCount(ctx, 1);
        ctx.Db.PlayerProgression.Insert(new PlayerProgression
        {
            Owner = ctx.Sender,
            Level = 1,
            Experience = 0,
            Gold = 0,
        });
        EnsureCommandState(ctx, ctx.Sender, entityId);
        SeedPlayerInventory(ctx, entityId);
        AppendEvent(ctx, entityId, "player_loaded", $"entity_id={entityId}");
        SynchronizePlayerClock(ctx, ctx.Sender);
    }

    private static void SynchronizePlayerClock(ReducerContext ctx, Identity owner)
    {
        var clock = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var playerClock = ctx.Db.PlayerClock.Owner.Find(owner) ?? new PlayerClock
        {
            Owner = owner,
        };
        playerClock.Tick = clock.Tick;
        playerClock.TickRateHz = WorldRules.TickRateHz;
        if (ctx.Db.PlayerClock.Owner.Find(owner) is null)
        {
            ctx.Db.PlayerClock.Insert(playerClock);
        }
        else
        {
            ctx.Db.PlayerClock.Owner.Update(playerClock);
        }
    }

}
