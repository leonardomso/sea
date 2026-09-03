using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void ApplyEnvironmentalHazards(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick)
    {
        MoveStorms(ctx, tick);
        ApplyEnvironmentalHazardKind(ctx, ships, tick, WorldObjectCode.Storm);
        ApplyEnvironmentalHazardKind(ctx, ships, tick, WorldObjectCode.Shoal);
    }

    private static void ApplyEnvironmentalHazardKind(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick,
        WorldObjectCode kind)
    {
        var exposedShips = FindExposedShips(ctx, ships, kind);
        ClearMissingExposures(ctx, ships, kind, exposedShips);
        foreach (var shipEntityId in exposedShips)
        {
            if (!ships.TryGet(ctx, shipEntityId, out var ship) || !ship.IsAlive)
            {
                continue;
            }

            var affected = ship;
            affected.EnvironmentExposureCode = HazardRules.SetExposure(
                affected.EnvironmentExposureCode,
                kind,
                exposed: true);
            if (kind == WorldObjectCode.Storm)
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

            if (kind == WorldObjectCode.Shoal && TacticalRules.ShouldApplyStatus(
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

    // Only ships currently flagged as exposed are indexed here, so leaving a hazard
    // costs one small scan rather than a pass over every ship.
    private static void ClearMissingExposures(
        ReducerContext ctx,
        ShipTickBuffer ships,
        WorldObjectCode kind,
        HashSet<ulong> exposedShips)
    {
        for (byte exposureCode = 1; exposureCode <= 3; exposureCode++)
        {
            if (!HazardRules.HasExposure(exposureCode, kind))
            {
                continue;
            }

            foreach (var indexedShip in ctx.Db.Ship.ByEnvironmentExposure.Filter(exposureCode))
            {
                if (exposedShips.Contains(indexedShip.EntityId))
                {
                    continue;
                }

                var ship = ships.TryGetStaged(indexedShip.EntityId, out var staged)
                    ? staged
                    : indexedShip;
                ship.EnvironmentExposureCode = HazardRules.SetExposure(
                    ship.EnvironmentExposureCode,
                    kind,
                    exposed: false);
                ships.Stage(ship);
            }
        }
    }

    // Exposure is decided from the thin published kinematics; a ship staged earlier
    // this tick (a respawn, for instance) is judged at its staged position instead.
    private static HashSet<ulong> FindExposedShips(
        ReducerContext ctx,
        ShipTickBuffer ships,
        WorldObjectCode kind)
    {
        var exposedShips = new HashSet<ulong>();
        foreach (var hazard in ctx.Db.WorldObject.ByActiveKind.Filter((true, (byte)kind)))
        {
            var bounds = SpatialRules.BoundsAround(
                hazard.PositionX,
                hazard.PositionY,
                hazard.Radius);
            foreach (var movement in ActiveMovementIn(ctx, bounds))
            {
                var (positionX, positionY, isAlive) =
                    ships.TryGetStaged(movement.EntityId, out var staged)
                        ? (staged.PositionX, staged.PositionY, staged.IsAlive)
                        : (movement.PositionX, movement.PositionY, movement.IsAlive);
                if (isAlive && WorldRules.IsInRange(
                        positionX,
                        positionY,
                        hazard.PositionX,
                        hazard.PositionY,
                        hazard.Radius))
                {
                    exposedShips.Add(movement.EntityId);
                }
            }
        }

        return exposedShips;
    }
}
