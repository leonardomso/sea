using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static SpawnPoint FindSafeSpawn(ReducerContext ctx, ulong seed)
    {
        var blockers = new List<SpawnBlocker>();
        foreach (var worldObject in UnsafeSpawnWorldObjects(ctx))
        {
            if (worldObject.IsActive && worldObject.BlocksMovement)
            {
                blockers.Add(new SpawnBlocker(
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius));
            }
        }

        if (!SpawnRules.TryFindSafePosition(seed, blockers, out var point))
        {
            throw new InvalidOperationException("No safe player spawn is available.");
        }

        return point;
    }

    private static SpawnPoint FindSafeRespawn(ReducerContext ctx, ulong seed)
    {
        var blockers = new List<SpawnBlocker>();
        foreach (var kind in new[]
                 {
                     WorldObjectCode.Island,
                     WorldObjectCode.Reef,
                     WorldObjectCode.Storm,
                     WorldObjectCode.Shoal,
                 })
        {
            foreach (var worldObject in ctx.Db.WorldObject.ByActiveKind.Filter(
                         (true, (byte)kind)))
            {
                blockers.Add(new SpawnBlocker(
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius));
            }
        }

        if (!SpawnRules.TryFindSafePosition(seed, blockers, out var point))
        {
            throw new InvalidOperationException("No safe respawn position is available.");
        }

        return point;
    }

    private static List<NavigationBlocker> NavigationBlockers(ReducerContext ctx)
    {
        var field = ctx.Db.NavigationFieldState.Id.Find(1) ??
            throw new InvalidOperationException("Navigation field state is missing.");
        var blockers = new List<NavigationBlocker>(field.Blockers.Count);
        foreach (var blocker in field.Blockers)
        {
            blockers.Add(new NavigationBlocker(blocker.X, blocker.Y, blocker.Radius));
        }

        return blockers;
    }

    private static void SeedNavigationField(ReducerContext ctx)
    {
        var blockers = new List<NavigationBlocker>();
        AddNavigationBlockers(BlockingWorldObjects(ctx), blockers);
        ctx.Db.NavigationFieldState.Insert(new NavigationFieldState
        {
            Id = 1,
            Blockers = blockers.Select(blocker => new NavigationBlockerState
            {
                X = blocker.X,
                Y = blocker.Y,
                Radius = blocker.Radius,
            }).ToList(),
        });
    }

    private static IEnumerable<WorldObject> BlockingWorldObjects(ReducerContext ctx)
    {
        foreach (var kind in new[] { WorldObjectCode.Island, WorldObjectCode.Reef })
        {
            foreach (var worldObject in ctx.Db.WorldObject.ByActiveKind.Filter(
                         (true, (byte)kind)))
            {
                yield return worldObject;
            }
        }
    }

    private static IEnumerable<WorldObject> UnsafeSpawnWorldObjects(ReducerContext ctx)
    {
        foreach (var kind in new[]
                 {
                     WorldObjectCode.Island,
                     WorldObjectCode.Reef,
                     WorldObjectCode.Storm,
                     WorldObjectCode.Shoal,
                 })
        {
            foreach (var worldObject in ctx.Db.WorldObject.ByActiveKind.Filter(
                         (true, (byte)kind)))
            {
                yield return worldObject;
            }
        }
    }

    private static void AddNavigationBlockers(
        IEnumerable<WorldObject> worldObjects,
        List<NavigationBlocker> blockers)
    {
        foreach (var worldObject in worldObjects)
        {
            if (!worldObject.IsActive ||
                !HotPathCodes.BlocksMovement((WorldObjectCode)worldObject.KindCode))
            {
                continue;
            }

            blockers.Add(new NavigationBlocker(
                worldObject.PositionX,
                worldObject.PositionY,
                worldObject.Radius));
        }
    }

    private static void ConfigureNavigationWaypoint(
        ref Ship ship,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        ship.HasWaypoint = NavigationRules.TryFindDetour(
            ship.PositionX,
            ship.PositionY,
            ship.DestinationX,
            ship.DestinationY,
            blockers,
            out var waypoint);
        ship.WaypointX = ship.HasWaypoint ? waypoint.X : ship.DestinationX;
        ship.WaypointY = ship.HasWaypoint ? waypoint.Y : ship.DestinationY;
    }

    private static void ConfigureNavigationWaypoint(
        ref ShipKinematics ship,
        IReadOnlyCollection<NavigationBlocker> blockers)
    {
        ship.HasWaypoint = NavigationRules.TryFindDetour(
            ship.PositionX,
            ship.PositionY,
            ship.DestinationX,
            ship.DestinationY,
            blockers,
            out var waypoint);
        ship.WaypointX = ship.HasWaypoint ? waypoint.X : ship.DestinationX;
        ship.WaypointY = ship.HasWaypoint ? waypoint.Y : ship.DestinationY;
        ship.DesiredHeadingDegrees = SailingRules.DesiredHeading(
            ship.PositionX,
            ship.PositionY,
            ship.WaypointX,
            ship.WaypointY);
    }

    private static ulong IdentitySeed(Identity identity)
    {
        var seed = 1469598103934665603UL;
        foreach (var character in identity.ToString())
        {
            seed ^= character;
            seed = unchecked(seed * 1099511628211UL);
        }

        return seed;
    }

    private static void InsertWorldObject(
        ReducerContext ctx,
        ulong entityId,
        string kind,
        float x,
        float y,
        float radius,
        bool blocksMovement,
        float directionDegrees = 0f,
        float movementSpeed = 0f,
        float intensity = 0f)
    {
        if (!HotPathCodes.TryParseWorldObject(kind, out var kindCode))
        {
            throw new InvalidOperationException($"Unknown world object kind '{kind}'.");
        }

        ctx.Db.WorldObject.Insert(new WorldObject
        {
            EntityId = entityId,
            Kind = kind,
            KindCode = (byte)kindCode,
            PositionX = x,
            PositionY = y,
            Radius = radius,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            IsActive = true,
            BlocksMovement = blocksMovement,
            DirectionDegrees = directionDegrees,
            MovementSpeed = movementSpeed,
            Intensity = intensity,
        });
    }
}
