using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ProcessStatuses(ReducerContext ctx, ulong tick)
    {
        foreach (var status in ctx.Db.ShipStatus.ByActive.Filter(true))
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
                ctx.Db.ShipStatus.StatusId.Update(expired);
                continue;
            }

            if (ctx.Db.Ship.EntityId.Find(status.ShipEntityId) is not Ship ship ||
                !ship.IsActive || !ship.IsAlive)
            {
                continue;
            }

            var damage = TacticalRules.PeriodicStatusDamage(
                status.StatusType,
                status.Stacks,
                tick);
            if (damage > 0)
            {
                var damaged = ship;
                ApplyDamageToShip(
                    ctx,
                    sourceEntityId: 0,
                    ref damaged,
                    new CombatDamage(damage, 0, 0, 0),
                    tick,
                    status.StatusType);
                ctx.Db.Ship.EntityId.Update(damaged);
                continue;
            }

            if (status.StatusType == "emergency_pump" && tick % 5 == 0 &&
                ship.Hull < ship.MaxHull)
            {
                var restored = ship;
                restored.Hull = Math.Min(restored.MaxHull, restored.Hull + 2);
                ctx.Db.Ship.EntityId.Update(restored);
            }
        }
    }

}
