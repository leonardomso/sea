using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyEnvironmentalHazards(ReducerContext ctx, ulong tick)
    {
        if (tick % WorldRules.TickRateHz != 0)
        {
            return;
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (!ship.IsAlive)
            {
                continue;
            }

            var hazards = HazardsAt(ctx, ship.PositionX, ship.PositionY);
            var affected = ship;
            if (hazards.InStorm)
            {
                ApplyDamageToShip(
                    ctx,
                    sourceEntityId: 0,
                    ref affected,
                    new CombatDamage(2, 0, 0, 0),
                    tick,
                    "storm");
            }

            if (hazards.InShoal && TacticalRules.ShouldApplyStatus(
                    ship.EntityId ^ tick,
                    chancePercent: 35))
            {
                ApplyStatus(
                    ctx,
                    ship.EntityId,
                    "flooding",
                    tick,
                    TacticalRules.StatusDurationTicks,
                    maximumStacks: 3);
            }

            if (!affected.Equals(ship))
            {
                ctx.Db.Ship.EntityId.Update(affected);
            }
        }
    }

}
