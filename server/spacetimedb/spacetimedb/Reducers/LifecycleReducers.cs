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
            ContentVersion = 5,
        });
        ctx.Db.SimulationClock.Insert(new SimulationClock
        {
            Id = 1,
            Tick = 0,
            NextEntityId = 1000,
        });
        ctx.Db.SimulationTelemetry.Insert(new SimulationTelemetry { Id = 1 });
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
                TimeSpan.FromMilliseconds(SimulationWorkRules.DispatchIntervalMilliseconds)),
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
            EnsurePlayerProgression(ctx, ctx.Sender);
            EnsurePlayerAccount(ctx, ctx.Sender);
            EnsureHull(ctx, ctx.Sender, ownership.ShipEntityId);
            EnsureCommandState(ctx, ctx.Sender, ownership.ShipEntityId);
            SynchronizePlayerClock(ctx, ctx.Sender);
            return;
        }

        var entityId = AllocateEntityId(ctx);
        var spawn = FindSafeSpawn(ctx, IdentitySeed(ctx.Sender));
        var ship = CreateShip(entityId, "player_sloop", "player", spawn.X, spawn.Y);
        var tick = CurrentSimulationTick(ctx);
        ship.InvulnerableUntilTick = RespawnRules.PlayerProtectionUntil(tick);
        ctx.Db.Ship.Insert(ship);
        InsertShipMovement(ctx, ship, tick);
        ctx.Db.PlayerOwnership.Insert(new PlayerOwnership
        {
            Owner = ctx.Sender,
            ShipEntityId = entityId,
            IsConnected = true,
        });
        AdjustConnectedPlayerCount(ctx, 1);
        EnsurePlayerProgression(ctx, ctx.Sender);
        EnsurePlayerAccount(ctx, ctx.Sender);
        EnsureHull(ctx, ctx.Sender, entityId);
        EnsureCommandState(ctx, ctx.Sender, entityId);
        SeedPlayerInventory(ctx, entityId);
        AppendEvent(ctx, tick, entityId, "player_loaded", $"entity_id={entityId}");
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

    private static void EnsurePlayerProgression(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.PlayerProgression.Owner.Find(owner) is null)
        {
            ctx.Db.PlayerProgression.Insert(new PlayerProgression
            {
                Owner = owner,
                MapRank = 1,
                Gold = 0,
            });
        }
    }

    private static void EnsurePlayerAccount(ReducerContext ctx, Identity owner)
    {
        if (ctx.Db.PlayerAccount.Owner.Find(owner) is null)
        {
            ctx.Db.PlayerAccount.Insert(new PlayerAccount
            {
                Owner = owner,
                AccountId = "",
            });
        }
    }

    /// <summary>
    /// Ensures the player owns a starter hull and that every owned hull's <see cref="ShipStats"/> row
    /// reflects it. Login seeding lives here, next to <see cref="EnsurePlayerProgression"/> and
    /// <see cref="EnsurePlayerAccount"/>, not in the tick pipeline.
    /// </summary>
    private static void EnsureHull(ReducerContext ctx, Identity owner, ulong shipEntityId)
    {
        // 1a seeds exactly one hull; this loop already has the shape 1c's dock needs once a player
        // can own several.
        var owned = false;
        foreach (var existing in ctx.Db.Hull.ByOwner.Filter(owner))
        {
            // Logging back in must not hand a damaged ship a free repair, so the sheet lands on
            // the row without restocking it.
            PublishStatSheet(ctx, shipEntityId, RecomputeShipStats(ctx, existing), restock: false);
            owned = true;
        }

        if (owned)
        {
            return;
        }

        var hull = ctx.Db.Hull.Insert(new Hull
        {
            HullId = 0,
            Owner = owner,
            HullDefId = Catalog.StarterHull.Id,
            Name = Catalog.StarterHull.Name,
            CannonDefId = Catalog.StarterCannon.Id,
            CannonCount = Catalog.StarterHull.CannonSlots,
        });
        PublishStatSheet(ctx, shipEntityId, RecomputeShipStats(ctx, hull), restock: true);
    }

    /// <summary>
    /// Copies a freshly computed stat sheet onto the live <see cref="Ship"/> row. The fat row
    /// carries the combat numbers outright so the tick never joins the dock tables to fire a
    /// volley; kinematics are untouched, so this needs no movement republication.
    /// </summary>
    private static void PublishStatSheet(
        ReducerContext ctx,
        ulong shipEntityId,
        ShipStatSheet sheet,
        bool restock)
    {
        if (ctx.Db.Ship.EntityId.Find(shipEntityId) is not Ship ship)
        {
            return;
        }

        ApplyStatSheet(ref ship, sheet, restock);
        ctx.Db.Ship.EntityId.Update(ship);
    }
}
