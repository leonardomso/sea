using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
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
        var ship = new Ship
        {
            EntityId = entityId,
            ArchetypeCode = (byte)HotPathCodes.ShipArchetype(archetypeId),
            FactionCode = string.Equals(faction, "player", StringComparison.Ordinal)
                ? (byte)FactionCode.Player
                : (byte)FactionCode.Npc,
            PositionX = x,
            PositionY = y,
            MapId = Catalog.Content.Maps[0].MapId,
            DestinationX = x,
            DestinationY = y,
            RouteIndex = 0,
            HasRoute = false,
            RouteVersion = 0,
            HeadingDegrees = 0f,
            Speed = 0f,
            EffectiveSpeedSquaresPerSecond = 0f,
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
            HasFired = false,
            LastShotTick = 0,
            LastCombatTick = 0,
            RespawnAtTick = 0,
            InvulnerableUntilTick = 0,
            EncounterId = 0,
        };

        // Every hull leaves the yard as a starter sloop. The owner's own sheet lands on the row at
        // login, and an NPC's tier numbers land on it at seed time.
        ApplyStatSheet(ref ship, BaselineStatSheet(), restock: true);
        return ship;
    }

    private static Ship FindShip(ReducerContext ctx, ulong entityId) =>
        ctx.Db.Ship.EntityId.Find(entityId) ??
        throw new InvalidOperationException("The requested ship does not exist.");
}
