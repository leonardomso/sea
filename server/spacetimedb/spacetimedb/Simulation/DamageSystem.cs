using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ResolveVolleys(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        foreach (var volley in ctx.Db.Volley.ByImpactDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            if (!ships.TryGet(ctx, volley.TargetEntityId, out var target) ||
                CombatRules.ResolveVolley(volley.ImpactAtTick, tick, target.IsActive && target.IsAlive) ==
                VolleyResolution.Harmless)
            {
                ctx.Db.Volley.VolleyId.Delete(volley.VolleyId);
                continue;
            }

            var defender = target;
            var appliedDamage = ApplyDamageToShip(
                ctx,
                ships,
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
                ApplyVolleyStatus(ctx, volley, ref defender, tick);
                AppendEvent(
                    ctx,
                    volley.SourceEntityId,
                    "broadside_impact",
                    $"entity_id={defender.EntityId},hull={appliedDamage.Hull},sails={appliedDamage.Sails},cannons={appliedDamage.Cannons},crew={appliedDamage.Crew}");
            }

            ships.Stage(defender);
            ctx.Db.Volley.VolleyId.Delete(volley.VolleyId);
        }
    }

    private static CombatDamage ApplyDamageToShip(
        ReducerContext ctx,
        ShipTickBuffer ships,
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

        var brace = HasActiveStatus(ctx, defender.EntityId, StatusCode.Brace, tick);
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

        if (InterruptActiveChannel(ctx, defender.EntityId, tick, cause))
        {
            defender.ModeCode = (byte)ShipMode.Operational;
        }
        var hullBefore = defender.Hull;
        var sailsBefore = defender.Sails;
        var cannonsBefore = defender.Cannons;
        var crewBefore = defender.Crew;
        defender.Hull = WorldRules.ApplyDamage(hullBefore, damage.Hull);
        defender.Sails = WorldRules.ApplyDamage(sailsBefore, damage.Sails);
        defender.Cannons = WorldRules.ApplyDamage(cannonsBefore, damage.Cannons);
        defender.Crew = WorldRules.ApplyDamage(crewBefore, damage.Crew);
        var applied = new CombatDamage(
            hullBefore - defender.Hull,
            sailsBefore - defender.Sails,
            cannonsBefore - defender.Cannons,
            crewBefore - defender.Crew);
        SynchronizeDisabledSails(ctx, defender, tick);
        var sunk = hullBefore > 0 && defender.Hull == 0;
        RecordCombatProgress(ctx, sourceEntityId, defender, applied);
        if (sunk)
        {
            SettleNpcEncounter(ctx, defender, tick);
            SinkShip(ctx, ships, sourceEntityId, ref defender, tick);
        }
        else if (sourceEntityId != 0 &&
            defender.FactionCode == (byte)FactionCode.Npc &&
            ctx.Db.PlayerOwnership.ShipEntityId.Find(sourceEntityId) is not null)
        {
            defender.TargetEntityId = sourceEntityId;
            defender.IsEngaged = true;
        }

        return applied;
    }

    private static void SinkShip(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong sourceEntityId,
        ref Ship defender,
        ulong tick)
    {
        defender.IsAlive = false;
        defender.IsActive = false;
        defender.IsMoving = false;
        defender.HasCourse = false;
        defender.IsStopping = false;
        defender.ModeCode = (byte)ShipMode.Sunk;
        ClearTargetLocks(ctx, ships, defender.EntityId);
        if (sourceEntityId != 0)
        {
            defender.TargetEntityId = 0;
        }

        ScheduleRespawn(ctx, ref defender, tick);
    }

    private static void ApplyVolleyStatus(
        ReducerContext ctx,
        Volley volley,
        ref Ship defender,
        ulong tick)
    {
        if (ctx.Db.AmmoDefinition.AmmoId.Find(volley.AmmoId) is not AmmoDefinition ammo ||
            ammo.AppliedStatusCode == (byte)StatusCode.None)
        {
            return;
        }

        var statusCode = (StatusCode)ammo.AppliedStatusCode;
        var chance = statusCode == StatusCode.Flooding ? 35u : 100u;
        if (!TacticalRules.ShouldApplyStatus(volley.VolleyId ^ defender.EntityId, chance))
        {
            return;
        }

        if (ApplyStatus(
            ctx,
            defender.EntityId,
            statusCode,
            tick,
            TacticalRules.StatusDurationTicks,
            maximumStacks: 3))
        {
            defender.MovementStatusMask |= HotPathCodes.MovementMask(statusCode);
        }
    }

    private static void ClearTargetLocks(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong targetEntityId)
    {
        foreach (var source in ctx.Db.Ship.ByTarget.Filter(targetEntityId))
        {
            if (!ships.TryGet(ctx, source.EntityId, out var cleared))
            {
                continue;
            }

            cleared.TargetEntityId = 0;
            cleared.IsEngaged = false;
            ships.Stage(cleared);
        }
    }

    private static ShipStatus? FindStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        StatusCode statusCode)
    {
        foreach (var status in ctx.Db.ShipStatus.ByShipStatus.Filter(
                     (shipEntityId, (byte)statusCode)))
        {
            return status;
        }

        return null;
    }

    private static bool HasActiveStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        StatusCode statusCode,
        ulong tick) =>
        FindStatus(ctx, shipEntityId, statusCode) is ShipStatus status &&
        status.IsActive && tick < status.ExpiresAtTick;

    private static uint ActiveStatusStacks(
        ReducerContext ctx,
        ulong shipEntityId,
        StatusCode statusCode,
        ulong tick) =>
        FindStatus(ctx, shipEntityId, statusCode) is ShipStatus status &&
        status.IsActive && tick < status.ExpiresAtTick
            ? status.Stacks
            : 0;

    private static bool ApplyStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        StatusCode statusCode,
        ulong tick,
        uint durationTicks,
        uint maximumStacks)
    {
        var existing = FindStatus(ctx, shipEntityId, statusCode);
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
            current.NextProcessTick = SimulationWorkRules.NextStatusProcessTick(
                statusCode,
                tick,
                current.ExpiresAtTick);
            ctx.Db.ShipStatus.StatusId.Update(current);
        }
        else
        {
            ctx.Db.ShipStatus.Insert(new ShipStatus
            {
                ShipEntityId = shipEntityId,
                StatusType = HotPathCodes.StatusId(statusCode),
                StatusCode = (byte)statusCode,
                Stacks = application.State.Stacks,
                ExpiresAtTick = application.State.ExpiresAtTick,
                ImmunityUntilTick = application.State.ImmunityUntilTick,
                NextProcessTick = SimulationWorkRules.NextStatusProcessTick(
                    statusCode,
                    tick,
                    application.State.ExpiresAtTick),
                IsActive = true,
            });
        }

        AppendEvent(
            ctx,
            shipEntityId,
            "status_applied",
            $"status={HotPathCodes.StatusId(statusCode)}");
        return true;
    }

    private static void DeactivateStatus(
        ReducerContext ctx,
        ulong shipEntityId,
        StatusCode statusCode,
        ulong tick)
    {
        if (FindStatus(ctx, shipEntityId, statusCode) is not ShipStatus status ||
            !status.IsActive)
        {
            return;
        }

        status.IsActive = false;
        status.Stacks = 0;
        status.ImmunityUntilTick = tick + TacticalRules.StatusImmunityTicks;
        status.NextProcessTick = ulong.MaxValue;
        ctx.Db.ShipStatus.StatusId.Update(status);
    }

    private static void SynchronizeDisabledSails(
        ReducerContext ctx,
        Ship ship,
        ulong tick)
    {
        if (ship.Sails == 0)
        {
            if (FindStatus(ctx, ship.EntityId, StatusCode.DisabledSails) is ShipStatus existing)
            {
                existing.IsActive = true;
                existing.Stacks = 1;
                existing.ExpiresAtTick = ulong.MaxValue;
                existing.ImmunityUntilTick = 0;
                existing.NextProcessTick = ulong.MaxValue;
                ctx.Db.ShipStatus.StatusId.Update(existing);
            }
            else
            {
                ctx.Db.ShipStatus.Insert(new ShipStatus
                {
                    ShipEntityId = ship.EntityId,
                    StatusType = "disabled_sails",
                    StatusCode = (byte)StatusCode.DisabledSails,
                    Stacks = 1,
                    ExpiresAtTick = ulong.MaxValue,
                    ImmunityUntilTick = 0,
                    NextProcessTick = ulong.MaxValue,
                    IsActive = true,
                });
            }
        }
        else
        {
            DeactivateStatus(ctx, ship.EntityId, StatusCode.DisabledSails, tick);
        }
    }

}
