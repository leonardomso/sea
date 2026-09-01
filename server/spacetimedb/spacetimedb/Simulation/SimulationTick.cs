using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer]
    public static void RunSimulationTick(ReducerContext ctx, SimulationTimer _timer)
    {
        if (ctx.Db.WorldState.Id.Find(1) is not WorldState world)
        {
            return;
        }

        world.Tick++;
        ctx.Db.WorldState.Id.Update(world);
        var ships = new ShipTickBuffer();
        UpdateWind(ctx, world.Tick);
        MoveStorms(ctx, world.Tick);
        ProcessStatuses(ctx, ships, world.Tick);
        ProcessChannels(ctx, ships, world.Tick);
        ApplyEnvironmentalHazards(ctx, ships, world.Tick);
        ResolveVolleys(ctx, ships, world.Tick);
        ProcessRespawns(ctx, ships, world.Tick);
        ProcessLootExpiry(ctx, world.Tick);
        ships.Flush(ctx);
        ProcessNpcDecisions(ctx, world.Tick);
    }

    [SpacetimeDB.Reducer]
    public static void RunMovementShard(ReducerContext ctx, MovementShardTimer timer)
    {
        if (ctx.Db.WorldState.Id.Find(1) is not WorldState world)
        {
            return;
        }

        var ships = new ShipTickBuffer();
        AdvanceMovingShips(
            ctx,
            ships,
            new SpatialTickCache(),
            world.Tick,
            timer.ShardId);
        ships.Flush(ctx);
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
            ArchetypeCode = (byte)HotPathCodes.ShipArchetype(archetypeId),
            FactionCode = faction == "player"
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

}
