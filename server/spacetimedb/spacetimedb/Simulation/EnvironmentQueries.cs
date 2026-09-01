using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static (bool InStorm, bool InShoal, TacticalModifiers Modifiers) HazardsAt(
        ReducerContext ctx,
        float x,
        float y) => HazardsAt(ctx, new SpatialTickCache(), x, y);

    private static (bool InStorm, bool InShoal, TacticalModifiers Modifiers) HazardsAt(
        ReducerContext ctx,
        SpatialTickCache spatial,
        float x,
        float y)
    {
        var inStorm = false;
        var inShoal = false;
        var bounds = SpatialRules.BoundsAround(
            x,
            y,
            SpatialRules.MaximumWorldInfluenceRadius);
        foreach (var worldObject in spatial.WorldObjectsIn(ctx, bounds))
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

            var kind = (WorldObjectCode)worldObject.KindCode;
            inStorm |= kind == WorldObjectCode.Storm;
            inShoal |= kind == WorldObjectCode.Shoal;
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
