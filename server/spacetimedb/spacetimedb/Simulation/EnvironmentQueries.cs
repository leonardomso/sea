using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (bool InStorm, bool InShoal, TacticalModifiers Modifiers) HazardsAt(
        ReducerContext ctx,
        float x,
        float y)
    {
        var inStorm = false;
        var inShoal = false;
        foreach (var worldObject in ctx.Db.WorldObject.Iter())
        {
            if (!worldObject.IsActive ||
                !WorldRules.IsInRange(
                    x,
                    y,
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius))
            {
                continue;
            }

            inStorm |= worldObject.Kind == "storm";
            inShoal |= worldObject.Kind == "shoal";
        }

        return (
            inStorm,
            inShoal,
            TacticalRules.MovementModifiers(
                fullSail: false,
                slowedStacks: 0,
                sailsDisabled: false,
                sailIntegrity: 1f,
                inShoal,
                inStorm,
                repairing: false));
    }

    private static CombatDamage ScaleCombatDamage(CombatDamage damage, float multiplier) =>
        new(
            ScaleDamage(damage.Hull, multiplier),
            ScaleDamage(damage.Sails, multiplier),
            ScaleDamage(damage.Cannons, multiplier),
            ScaleDamage(damage.Crew, multiplier));

    private static uint ScaleDamage(uint damage, float multiplier) =>
        damage == 0
            ? 0
            : (uint)MathF.Round(damage * multiplier, MidpointRounding.AwayFromZero);

}
