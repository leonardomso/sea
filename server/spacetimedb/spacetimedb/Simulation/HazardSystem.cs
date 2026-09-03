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
        for (byte shardId = 0; shardId < SimulationWorkRules.HazardShardCount; shardId++)
        {
            ApplyEnvironmentalHazardKind(ctx, ships, tick, WorldObjectCode.Storm, shardId);
            ApplyEnvironmentalHazardKind(ctx, ships, tick, WorldObjectCode.Shoal, shardId);
        }
    }

    private static void ApplyEnvironmentalHazardKind(
        ReducerContext ctx,
        ShipTickBuffer ships,
        ulong tick,
        WorldObjectCode kind,
        byte shardId)
    {
        var exposedShips = FindExposedShips(ctx, ships, kind, shardId);
        ClearMissingExposures(ctx, ships, kind, shardId, exposedShips);
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

    private static void ClearMissingExposures(
        ReducerContext ctx,
        ShipTickBuffer ships,
        WorldObjectCode kind,
        byte shardId,
        HashSet<ulong> exposedShips)
    {
        for (byte exposureCode = 1; exposureCode <= 3; exposureCode++)
        {
            if (!HazardRules.HasExposure(exposureCode, kind))
            {
                continue;
            }

            foreach (var indexedShip in ctx.Db.Ship
                         .ByEnvironmentExposureHazardShard.Filter(
                             (exposureCode, shardId)))
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

    private static HashSet<ulong> FindExposedShips(
        ReducerContext ctx,
        ShipTickBuffer ships,
        WorldObjectCode kind,
        byte shardId)
    {
        var exposedShips = new HashSet<ulong>();
        foreach (var hazard in ctx.Db.WorldObject.ByActiveKind.Filter((true, (byte)kind)))
        {
            var bounds = SpatialRules.BoundsAround(
                hazard.PositionX,
                hazard.PositionY,
                hazard.Radius);
            foreach (var indexedShip in ActiveShipsInHazardShard(ctx, bounds, shardId))
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

                exposedShips.Add(ship.EntityId);
            }
        }

        return exposedShips;
    }

}
