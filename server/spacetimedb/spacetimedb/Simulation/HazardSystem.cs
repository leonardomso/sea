using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyEnvironmentalHazards(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        if (tick % WorldRules.TickRateHz != 0)
        {
            return;
        }

        var exposures = new Dictionary<ulong, (bool InStorm, bool InShoal)>();
        AddHazardExposures(ctx, ships, WorldObjectCode.Storm, exposures);
        AddHazardExposures(ctx, ships, WorldObjectCode.Shoal, exposures);
        ClearMissingExposures(ctx, ships, exposures);
        foreach (var (shipEntityId, exposure) in exposures)
        {
            if (!ships.TryGet(ctx, shipEntityId, out var ship) || !ship.IsAlive)
            {
                continue;
            }

            var affected = ship;
            affected.EnvironmentExposureCode = (byte)(
                (exposure.InStorm ? 1 : 0) |
                (exposure.InShoal ? 2 : 0));
            if (exposure.InStorm)
            {
                ApplyDamageToShip(
                    ctx,
                    ships,
                    sourceEntityId: 0,
                    ref affected,
                    new CombatDamage(2, 0, 0, 0),
                    tick,
                    "storm");
            }

            if (exposure.InShoal && TacticalRules.ShouldApplyStatus(
                    ship.EntityId ^ tick,
                    chancePercent: 35))
            {
                ApplyStatus(
                    ctx,
                    ship.EntityId,
                    StatusCode.Flooding,
                    tick,
                    TacticalRules.StatusDurationTicks,
                    maximumStacks: 3);
            }

            if (!affected.Equals(ship))
            {
                ships.Stage(affected);
            }
        }
    }

    private static void ClearMissingExposures(
        ReducerContext ctx,
        ShipTickBuffer ships,
        Dictionary<ulong, (bool InStorm, bool InShoal)> exposures)
    {
        for (byte exposureCode = 1; exposureCode <= 3; exposureCode++)
        {
            foreach (var indexedShip in ctx.Db.Ship.ByEnvironmentExposure.Filter(exposureCode))
            {
                if (exposures.ContainsKey(indexedShip.EntityId))
                {
                    continue;
                }

                var ship = ships.TryGetStaged(indexedShip.EntityId, out var staged)
                    ? staged
                    : indexedShip;
                ship.EnvironmentExposureCode = 0;
                ships.Stage(ship);
            }
        }
    }

    private static void AddHazardExposures(
        ReducerContext ctx,
        ShipTickBuffer ships,
        WorldObjectCode kind,
        Dictionary<ulong, (bool InStorm, bool InShoal)> exposures)
    {
        foreach (var hazard in ctx.Db.WorldObject.ByActiveKind.Filter((true, (byte)kind)))
        {
            var bounds = SpatialRules.BoundsAround(
                hazard.PositionX,
                hazard.PositionY,
                hazard.Radius);
            foreach (var indexedShip in ActiveShipsIn(ctx, bounds))
            {
                var ship = ships.TryGetStaged(indexedShip.EntityId, out var staged)
                    ? staged
                    : indexedShip;

                if (!ship.IsAlive || !WorldRules.IsInRange(
                        ship.PositionX,
                        ship.PositionY,
                        hazard.PositionX,
                        hazard.PositionY,
                        hazard.Radius))
                {
                    continue;
                }

                exposures.TryGetValue(ship.EntityId, out var current);
                exposures[ship.EntityId] = (
                    current.InStorm || kind == WorldObjectCode.Storm,
                    current.InShoal || kind == WorldObjectCode.Shoal);
            }
        }
    }

}
