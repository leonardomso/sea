using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
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

        if (InterruptActiveChannel(ctx, defender.EntityId, tick, cause))
        {
            defender.ModeCode = (byte)ShipMode.Operational;
        }
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
            defender.ModeCode = (byte)ShipMode.Sunk;
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

}
