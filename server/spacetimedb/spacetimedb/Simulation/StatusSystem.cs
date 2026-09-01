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
            if (tick >= status.ExpiresAtTick)
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
                continue;
            }

            if (!ships.TryGet(ctx, status.ShipEntityId, out var ship) ||
                !ship.IsActive || !ship.IsAlive)
            {
                var inactive = status;
                inactive.IsActive = false;
                inactive.Stacks = 0;
                inactive.NextProcessTick = ulong.MaxValue;
                ctx.Db.ShipStatus.StatusId.Update(inactive);
                continue;
            }

            var statusCode = (StatusCode)status.StatusCode;
            var damage = TacticalRules.PeriodicStatusDamage(
                statusCode,
                status.Stacks);
            if (damage > 0)
            {
                var damaged = ship;
                ApplyDamageToShip(
                    ctx,
                    ships,
                    sourceEntityId: 0,
                    ref damaged,
                    new CombatDamage(damage, 0, 0, 0),
                    tick,
                    status.StatusType);
                ships.Stage(damaged);
            }
            else if (statusCode == StatusCode.EmergencyPump &&
                ship.Hull < ship.MaxHull)
            {
                var restored = ship;
                restored.Hull = Math.Min(restored.MaxHull, restored.Hull + 2);
                ships.Stage(restored);
            }

            var scheduled = status;
            scheduled.NextProcessTick = SimulationWorkRules.NextStatusProcessTick(
                statusCode,
                tick,
                status.ExpiresAtTick);
            ctx.Db.ShipStatus.StatusId.Update(scheduled);
        }
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
