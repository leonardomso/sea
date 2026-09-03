using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ScheduleRespawn(
        ReducerContext ctx,
        ref Ship ship,
        ulong tick)
    {
        var player = ship.FactionCode == (byte)FactionCode.Player;
        ship.RespawnAtTick = tick + (player
            ? RespawnRules.PlayerDelayTicks
            : RespawnRules.NpcDelayTicks);
        if (ctx.Db.RespawnWork.ShipEntityId.Find(ship.EntityId) is RespawnWork work)
        {
            work.IsPending = true;
            work.RespawnAtTick = ship.RespawnAtTick;
            ctx.Db.RespawnWork.ShipEntityId.Update(work);
        }
        else
        {
            ctx.Db.RespawnWork.Insert(new RespawnWork
            {
                ShipEntityId = ship.EntityId,
                IsPending = true,
                RespawnAtTick = ship.RespawnAtTick,
            });
        }

        if (!player)
        {
            SpawnNpcLoot(ctx, ship, tick);
            if (ctx.Db.NpcAi.ShipEntityId.Find(ship.EntityId) is NpcAi ai)
            {
                ai.IsActive = false;
                ctx.Db.NpcAi.ShipEntityId.Update(ai);
            }
        }
    }

    private static void ProcessRespawns(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        foreach (var work in ctx.Db.RespawnWork.ByRespawnDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            if (!ships.TryGet(ctx, work.ShipEntityId, out var ship))
            {
                ctx.Db.RespawnWork.ShipEntityId.Delete(work.ShipEntityId);
                continue;
            }

            var spawn = FindSafeRespawn(
                ctx,
                ship,
                ship.EntityId ^ work.RespawnAtTick ^ ship.EncounterId);
            RestoreShipForRespawn(ctx, ref ship, spawn, tick);
            ships.Stage(ship);
            ctx.Db.RespawnWork.ShipEntityId.Delete(work.ShipEntityId);
            AppendEvent(ctx, tick, ship.EntityId, "ship_respawned", "");
        }
    }

    private static void RestoreShipForRespawn(
        ReducerContext ctx,
        ref Ship ship,
        SpawnPoint spawn,
        ulong tick)
    {
        var player = ship.FactionCode == (byte)FactionCode.Player;
        var restored = RespawnRules.Restore(player, ship.MaxHull, tick);
        ship.PositionX = spawn.X;
        ship.PositionY = spawn.Y;
        ship.DestinationX = spawn.X;
        ship.DestinationY = spawn.Y;
        ship.WaypointX = spawn.X;
        ship.WaypointY = spawn.Y;
        ship.HasWaypoint = false;
        ship.HasCourse = false;
        ship.IsStopping = false;
        ship.IsMoving = false;
        ship.Speed = 0f;
        ship.CurrentVelocityX = 0f;
        ship.CurrentVelocityY = 0f;
        ship.ChunkX = SpatialRules.ChunkCoordinate(spawn.X);
        ship.ChunkY = SpatialRules.ChunkCoordinate(spawn.Y);
        ship.TargetEntityId = 0;
        ship.IsEngaged = false;
        ship.IsActive = true;
        ship.IsAlive = true;
        ship.ModeCode = (byte)ShipMode.Operational;
        ship.MovementStatusMask = 0;
        ship.MovementSlowMagnitude = 0f;
        ship.EnvironmentExposureCode = 0;
        ship.Hull = restored.Hull;

        // A ship comes back with its guns loaded; the fire interval is the only thing between it
        // and its first volley.
        ship.ReadyVolleys = ship.MagazineSize;
        ship.ReloadProgressTicks = 0;
        ship.IsReloading = false;
        ship.HasFired = false;
        ship.LastShotTick = 0;
        ship.RespawnAtTick = 0;
        ship.InvulnerableUntilTick = restored.InvulnerableUntilTick;
        ClearRespawnState(ctx, ship.EntityId);
        if (!player)
        {
            ReopenNpcEncounter(ctx, ref ship, tick);
        }
    }

    /// <summary>
    /// A respawned NPC is a fresh bounty: it gets a new encounter id so the contributions from the
    /// fight that sank it cannot be paid out twice.
    /// </summary>
    private static void ReopenNpcEncounter(ReducerContext ctx, ref Ship ship, ulong tick)
    {
        if (ctx.Db.NpcAi.ShipEntityId.Find(ship.EntityId) is not NpcAi ai)
        {
            return;
        }

        ship.EncounterId = AllocateEntityId(ctx);
        var definition = Catalog.NpcByArchetypeCode[ship.ArchetypeCode] ??
            throw new InvalidOperationException("Respawning NPC definition is missing.");

        OpenNpcEncounter(ctx, ship, definition.GoldReward, definition.ExperienceReward, tick);
        ai.IsActive = true;
        ai.NextDecisionTick = tick + NpcRules.DecisionIntervalTicks;
        ctx.Db.NpcAi.ShipEntityId.Update(ai);
    }

    private static void ClearRespawnState(ReducerContext ctx, ulong shipEntityId)
    {
        ClearEffects(ctx, shipEntityId);

        if (ctx.Db.ShipChannel.ShipEntityId.Find(shipEntityId) is not null)
        {
            ctx.Db.ShipChannel.ShipEntityId.Delete(shipEntityId);
        }

        var cooldownIds = ctx.Db.Cooldown.ByShip.Filter(shipEntityId)
            .Select(cooldown => cooldown.CooldownId)
            .ToArray();
        foreach (var cooldownId in cooldownIds)
        {
            ctx.Db.Cooldown.CooldownId.Delete(cooldownId);
        }
    }
}
