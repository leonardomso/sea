using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunSimulationTick(ReducerContext ctx, SimulationTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock world)
        {
            return;
        }

        world.Tick++;
        ctx.Db.SimulationClock.Id.Update(world);
        UpdateWind(ctx, world.Tick);
    }

    [SpacetimeDB.Reducer]
    public static void RunStatusTick(ReducerContext ctx, StatusTimer timer) =>
        RunShipWork(ctx, ProcessStatuses);

    [SpacetimeDB.Reducer]
    public static void RunChannelTick(ReducerContext ctx, ChannelTimer timer) =>
        RunShipWork(ctx, ProcessChannels);

    [SpacetimeDB.Reducer]
    public static void RunVolleyTick(ReducerContext ctx, VolleyTimer timer) =>
        RunShipWork(ctx, ResolveVolleys);

    [SpacetimeDB.Reducer]
    public static void RunRespawnTick(ReducerContext ctx, RespawnTimer timer) =>
        RunShipWork(ctx, ProcessRespawns);

    [SpacetimeDB.Reducer]
    public static void RunLootExpiryTick(ReducerContext ctx, LootExpiryTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is SimulationClock world)
        {
            ProcessLootExpiry(ctx, world.Tick);
        }
    }

    private static void RunShipWork(
        ReducerContext ctx,
        Action<ReducerContext, ShipTickBuffer, ulong> process)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock world)
        {
            return;
        }

        var ships = new ShipTickBuffer();
        process(ctx, ships, world.Tick);
        ships.Flush(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void RunHazardTick(ReducerContext ctx, HazardTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock world)
        {
            return;
        }

        var kind = (WorldObjectCode)timer.HazardKindCode;
        if (kind is not WorldObjectCode.Storm and not WorldObjectCode.Shoal)
        {
            throw new InvalidOperationException("The hazard timer has an invalid kind.");
        }
        if (timer.ShardId >= SimulationWorkRules.HazardShardCount)
        {
            throw new InvalidOperationException("The hazard timer has an invalid shard.");
        }

        var ships = new ShipTickBuffer();
        if (kind == WorldObjectCode.Storm && timer.ShardId == 0)
        {
            MoveStorms(ctx, world.Tick);
        }

        ApplyEnvironmentalHazardKind(ctx, ships, world.Tick, kind, timer.ShardId);
        ships.Flush(ctx);
    }

    [SpacetimeDB.Reducer]
    public static void RunNpcTick(ReducerContext ctx, NpcTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock world)
        {
            return;
        }

        if (timer.ShardId >= SimulationWorkRules.NpcShardCount)
        {
            throw new InvalidOperationException("The NPC timer has an invalid shard.");
        }

        var npcWork = ProcessNpcDecisions(ctx, world.Tick, timer.ShardId);
        RecordNpcTelemetry(ctx, world.Tick, npcWork);
    }

    [SpacetimeDB.Reducer]
    public static void RunMovementShard(ReducerContext ctx, MovementShardTimer timer)
    {
        if (ctx.Db.SimulationClock.Id.Find(1) is not SimulationClock world)
        {
            return;
        }

        var movementWork = AdvanceMovingShips(
            ctx,
            new SpatialTickCache(),
            world.Tick,
            timer.ShardId,
            world.ActiveLootCount > 0);
        RecordMovementTelemetry(ctx, world.Tick, movementWork);
    }

    private static void RecordMovementTelemetry(
        ReducerContext ctx,
        ulong tick,
        (uint Processed, uint Dormant) work)
    {
        if (!SimulationWorkRules.ShouldSampleTelemetry(tick) ||
            ctx.Db.SimulationTelemetry.Id.Find(1) is not SimulationTelemetry telemetry)
        {
            return;
        }

        telemetry.ObservedAtTick = tick;
        telemetry.SampledMovementRows += work.Processed;
        telemetry.DormantMovementRows += work.Dormant;
        ctx.Db.SimulationTelemetry.Id.Update(telemetry);
    }

    private static void RecordNpcTelemetry(
        ReducerContext ctx,
        ulong tick,
        (uint Processed, uint Dormant) work)
    {
        if (!SimulationWorkRules.ShouldSampleTelemetry(tick) ||
            ctx.Db.SimulationTelemetry.Id.Find(1) is not SimulationTelemetry telemetry)
        {
            return;
        }

        telemetry.ObservedAtTick = tick;
        telemetry.SampledNpcRows += work.Processed;
        telemetry.DormantNpcRows += work.Dormant;
        ctx.Db.SimulationTelemetry.Id.Update(telemetry);
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

        SetLoadedConnectionState(ctx, ref ownership, connected);
    }

    private static void SetLoadedConnectionState(
        ReducerContext ctx,
        ref PlayerOwnership ownership,
        bool connected)
    {
        if (ownership.IsConnected == connected)
        {
            return;
        }

        ownership.IsConnected = connected;
        ctx.Db.PlayerOwnership.Owner.Update(ownership);
        AdjustConnectedPlayerCount(ctx, connected ? 1 : -1);
    }

    private static void AdjustConnectedPlayerCount(ReducerContext ctx, int delta)
    {
        var clock = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var previous = clock.ConnectedPlayerCount;
        clock.ConnectedPlayerCount = delta > 0
            ? checked(previous + (uint)delta)
            : previous - Math.Min(previous, (uint)-delta);
        ctx.Db.SimulationClock.Id.Update(clock);
        if ((previous == 0) != (clock.ConnectedPlayerCount == 0))
        {
            SetSimulationCadence(ctx, clock.ConnectedPlayerCount > 0);
        }
    }

    private static ulong AllocateEntityId(ReducerContext ctx)
    {
        var world = ctx.Db.SimulationClock.Id.Find(1) ??
            throw new InvalidOperationException("Simulation clock is missing.");
        var entityId = world.NextEntityId;
        world.NextEntityId++;
        ctx.Db.SimulationClock.Id.Update(world);
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
            ArchetypeCode = (byte)HotPathCodes.ShipArchetype(archetypeId),
            FactionCode = string.Equals(faction, "player", StringComparison.Ordinal)
                ? (byte)FactionCode.Player
                : (byte)FactionCode.Npc,
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
            MovementShard = SimulationWorkRules.MovementShard(entityId),
            HazardShard = SimulationWorkRules.HazardShard(
                SimulationWorkRules.MovementShard(entityId)),
            IsActive = true,
            IsAlive = true,
            IsEngaged = false,
            ModeCode = (byte)ShipMode.Operational,
            MovementStatusMask = 0,
            EnvironmentExposureCode = 0,
            CurrentVelocityX = 0f,
            CurrentVelocityY = 0f,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            TargetEntityId = 0,
            SelectedAmmoCode = (byte)AmmunitionCode.Round,
            SelectedWeakPointCode = (byte)WeakPointCode.Hull,
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
            EncounterId = 0,
        };
    }

    private static Ship FindPlayerShip(ReducerContext ctx, Identity owner)
    {
        var ownership = ctx.Db.PlayerOwnership.Owner.Find(owner) ??
            throw new InvalidOperationException("Player has not been loaded.");
        return FindShip(ctx, ownership.ShipEntityId);
    }

    private static Ship FindShip(ReducerContext ctx, ulong entityId) =>
        ctx.Db.Ship.EntityId.Find(entityId) ??
        throw new InvalidOperationException("The requested ship does not exist.");

    private static PlayerProgression FindProgression(ReducerContext ctx, Identity owner) =>
        ctx.Db.PlayerProgression.Owner.Find(owner) ??
        throw new InvalidOperationException("Player progression is missing.");

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

}
