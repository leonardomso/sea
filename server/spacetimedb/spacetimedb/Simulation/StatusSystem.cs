using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessStatuses(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        foreach (var status in ctx.Db.ShipStatus.ByStatusDue.Filter(
                     (true, new Bound<ulong>(0, tick))))
        {
            ProcessDueStatus(ctx, ships, status, tick);
        }
    }

    private static void ProcessDueStatus(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipStatus status,
        ulong tick)
    {
        if (tick >= status.ExpiresAtTick)
        {
            ExpireStatus(ctx, ships, status, tick);
            return;
        }

        if (!ships.TryGet(ctx, status.ShipEntityId, out var ship) ||
            !ship.IsActive || !ship.IsAlive)
        {
            DeactivateStatus(ctx, status);
            return;
        }

        ApplyPeriodicStatusEffect(ctx, ships, status, ship, tick);
        var scheduled = status;
        scheduled.NextProcessTick = SimulationWorkRules.NextStatusProcessTick(
            (StatusCode)status.StatusCode,
            tick,
            status.ExpiresAtTick);
        ctx.Db.ShipStatus.StatusId.Update(scheduled);
    }

    private static void ExpireStatus(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipStatus status,
        ulong tick)
    {
        var lifecycle = TacticalRules.ExpireStatus(
            new TacticalStatusState(
                status.IsActive,
                status.Stacks,
                status.ExpiresAtTick,
                status.ImmunityUntilTick),
            tick,
            TacticalRules.StatusImmunityTicks);
        var expired = status;
        expired.IsActive = lifecycle.IsActive;
        expired.Stacks = lifecycle.Stacks;
        expired.ImmunityUntilTick = lifecycle.ImmunityUntilTick;
        expired.NextProcessTick = ulong.MaxValue;
        ctx.Db.ShipStatus.StatusId.Update(expired);
        ClearMovementStatus(ctx, ships, status.ShipEntityId, (StatusCode)status.StatusCode);
    }

    private static void DeactivateStatus(ReducerContext ctx, ShipStatus status)
    {
        var inactive = status;
        inactive.IsActive = false;
        inactive.Stacks = 0;
        inactive.NextProcessTick = ulong.MaxValue;
        ctx.Db.ShipStatus.StatusId.Update(inactive);
    }

    private static void ApplyPeriodicStatusEffect(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipStatus status,
        Ship ship,
        ulong tick)
    {
        var statusCode = (StatusCode)status.StatusCode;
        var damage = TacticalRules.PeriodicStatusDamage(statusCode, status.Stacks);
        if (damage > 0)
        {
            ApplyStatusDamage(ctx, ships, status, ship, tick, damage);
            return;
        }

        if (statusCode == StatusCode.EmergencyPump && ship.Hull < ship.MaxHull)
        {
            ship.Hull = Math.Min(ship.MaxHull, ship.Hull + 2);
            ships.Stage(ship);
        }
    }

    private static void ApplyStatusDamage(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ShipStatus status,
        Ship ship,
        ulong tick,
        uint damage)
    {
        ApplyDamageToShip(
            ctx,
            ships,
            sourceEntityId: 0,
            ref ship,
            new CombatDamage(damage, 0, 0, 0),
            tick,
            status.StatusType);
        ships.Stage(ship);
    }

    private static void ClearMovementStatus(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong shipEntityId,
        StatusCode statusCode)
    {
        var mask = HotPathCodes.MovementMask(statusCode);
        if (mask == 0 || !ships.TryGet(ctx, shipEntityId, out var ship))
        {
            return;
        }

        ship.MovementStatusMask &= (byte)~mask;
        ships.Stage(ship);
    }

}
