using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static SpawnPoint FindSafeSpawn(ReducerContext ctx, ulong seed)
    {
        var blockers = new List<SpawnBlocker>();
        foreach (var worldObject in BlockingWorldObjects(ctx))
        {
            if (worldObject.IsActive && worldObject.BlocksMovement)
            {
                blockers.Add(new SpawnBlocker(
                    worldObject.PositionX,
                    worldObject.PositionY,
                    worldObject.Radius));
            }
        }

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (ship.IsAlive)
            {
                blockers.Add(new SpawnBlocker(ship.PositionX, ship.PositionY, 4f));
            }
        }

        if (!SpawnRules.TryFindSafePosition(seed, blockers, out var point))
        {
            throw new Exception("No safe player spawn is available.");
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

        foreach (var ship in ctx.Db.Ship.ByActive.Filter(true))
        {
            if (ship.IsAlive)
            {
                blockers.Add(new SpawnBlocker(ship.PositionX, ship.PositionY, 4f));
            }
        }

        if (!SpawnRules.TryFindSafePosition(seed, blockers, out var point))
        {
            throw new InvalidOperationException("No safe respawn position is available.");
        }

        return point;
    }

    private static List<NavigationBlocker> NavigationBlockersAt(
        ReducerContext ctx,
        float x,
        float y)
    {
        var blockers = new List<NavigationBlocker>();
        var bounds = SpatialRules.BoundsAround(
            x,
            y,
            SpatialRules.MaximumWorldInfluenceRadius);
        AddNavigationBlockers(WorldObjectsIn(ctx, bounds), blockers);
        return blockers;
    }

    private static List<NavigationBlocker> NavigationBlockersForCourse(
        ReducerContext ctx,
        float startX,
        float startY,
        float destinationX,
        float destinationY)
    {
        var blockers = new List<NavigationBlocker>();
        var bounds = SpatialRules.BoundsForSegment(
            startX,
            startY,
            destinationX,
            destinationY,
            SpatialRules.MaximumWorldInfluenceRadius);
        AddNavigationBlockers(WorldObjectsIn(ctx, bounds), blockers);
        return blockers;
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

    private static void AddNavigationBlockers(
        IEnumerable<WorldObject> worldObjects,
        ICollection<NavigationBlocker> blockers)
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
