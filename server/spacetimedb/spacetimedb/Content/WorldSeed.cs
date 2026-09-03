using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SeedWorld(ReducerContext ctx)
    {
        var content = Catalog.Content;
        var map = content.Maps[0];
        foreach (var item in map.Objects)
        {
            InsertWorldObject(
                ctx,
                item.EntityId,
                item.Kind,
                item.X,
                item.Y,
                item.Radius,
                item.BlocksMovement,
                item.DirectionDegrees,
                item.MovementSpeed,
                item.Intensity);
        }

        // The blocking world objects are all inserted above, so one scan covers every spawn below.
        var blockers = SpawnBlockers(ctx);
        var hostileBlockers = HostileHomeBlockers(ctx, blockers);
        var entityId = 10ul;
        foreach (var definition in content.Npcs)
        {
            for (var index = 0; index < 4; index++)
            {
                SeedNpc(
                    ctx,
                    definition.AggroRange > 0f ? hostileBlockers : blockers,
                    entityId,
                    definition,
                    index);
                entityId++;
            }
        }
    }

    private static List<SpawnBlocker> HostileHomeBlockers(
        ReducerContext ctx,
        List<SpawnBlocker> blockers)
    {
        var hostileBlockers = new List<SpawnBlocker>(blockers);
        if (FindHarbor(ctx) is WorldObject harbor)
        {
            hostileBlockers.Add(new SpawnBlocker(
                harbor.PositionX,
                harbor.PositionY,
                NpcRules.HostileHomeClearance - SpawnRules.Separation));
        }

        return hostileBlockers;
    }

    private static void SeedNpc(
        ReducerContext ctx,
        IReadOnlyList<SpawnBlocker> blockers,
        ulong entityId,
        NpcContent definition,
        int archetypeIndex)
    {
        var spawn = FindSafeSpawn(
            blockers,
            entityId ^ unchecked((ulong)(archetypeIndex + 1) * 0x9E3779B97F4A7C15UL));
        var ship = CreateShip(entityId, definition.Id, "npc", spawn.X, spawn.Y);
        ship.MaximumSpeed = definition.MaximumSpeed;
        ship.Hull = definition.Hull;
        ship.MaxHull = definition.Hull;
        ship.CannonDamage = definition.CannonDamage;
        ship.CannonCooldownTicks = WorldRules.EnemyCannonCooldownTicks;
        ship.SelectedAmmoCode = (byte)definition.PreferredAmmunition;
        ship.SelectedWeakPointCode = (byte)definition.PreferredWeakPoint;
        ship.EncounterId = entityId;
        ctx.Db.Ship.Insert(ship);
        InsertShipMovement(ctx, ship, CurrentSimulationTick(ctx));
        OpenNpcEncounter(ctx, ship, definition.GoldReward, definition.ExperienceReward, tick: 0);
        ctx.Db.NpcAi.Insert(new NpcAi
        {
            ShipEntityId = entityId,
            IsActive = true,
            DecisionShard = SimulationWorkRules.NpcShard(entityId),
            NextDecisionTick = (ulong)archetypeIndex,
            HomeSeed = entityId * 17,
            HomeX = spawn.X,
            HomeY = spawn.Y,
        });
        SeedNpcInventory(ctx, entityId);
    }

    private static void SeedEnvironment(ReducerContext ctx)
    {
        const ulong seed = 0x5EA2026;
        var wind = EnvironmentRules.WindForEpoch(seed, 0);
        ctx.Db.EnvironmentState.Insert(new EnvironmentState
        {
            Id = 1,
            Seed = seed,
            WindEpoch = 0,
            WindDirectionDegrees = wind.DirectionDegrees,
            WindStrength = wind.Strength,
            NextWindChangeTick = EnvironmentRules.WindEpochTicks,
        });

        var map = Catalog.Content.Maps[0];
        var zones = new List<CurrentZone>(map.Currents.Count);
        foreach (var current in map.Currents)
        {
            zones.Add(InsertCurrentZone(
                ctx,
                current.ZoneId,
                current.X,
                current.Y,
                current.Radius,
                current.DirectionDegrees,
                current.Strength));
        }

        ctx.Db.CurrentFieldState.Insert(BuildCurrentFieldState(zones));
    }

    private static CurrentZone InsertCurrentZone(
        ReducerContext ctx,
        ulong zoneId,
        float x,
        float y,
        float radius,
        float directionDegrees,
        float strength)
    {
        var zone = new CurrentZone
        {
            ZoneId = zoneId,
            PositionX = x,
            PositionY = y,
            Radius = radius,
            DirectionDegrees = directionDegrees,
            Strength = strength,
            ChunkX = SpatialRules.ChunkCoordinate(x),
            ChunkY = SpatialRules.ChunkCoordinate(y),
            IsActive = true,
        };
        ctx.Db.CurrentZone.Insert(zone);
        return zone;
    }

    private static CurrentFieldState BuildCurrentFieldState(
        List<CurrentZone> source)
    {
        if (source.Count > 64)
        {
            throw new InvalidOperationException("A current field supports at most 64 zones.");
        }

        var masks = Enumerable.Repeat(
                0UL,
                SpatialRules.ChunkCountPerAxis * SpatialRules.ChunkCountPerAxis)
            .ToList();
        var zones = new List<CurrentFieldZone>(source.Count);
        for (var index = 0; index < source.Count; index++)
        {
            var zone = source[index];
            var velocity = EnvironmentRules.DirectionalVelocity(
                zone.DirectionDegrees,
                zone.Strength);
            zones.Add(new CurrentFieldZone
            {
                PositionX = zone.PositionX,
                PositionY = zone.PositionY,
                Radius = zone.Radius,
                VelocityX = velocity.X,
                VelocityY = velocity.Y,
            });
            var bounds = SpatialRules.BoundsAround(
                zone.PositionX,
                zone.PositionY,
                zone.Radius);
            for (var chunkX = bounds.MinX; chunkX <= bounds.MaxX; chunkX++)
            {
                for (var chunkY = bounds.MinY; chunkY <= bounds.MaxY; chunkY++)
                {
                    var cell = chunkY * SpatialRules.ChunkCountPerAxis + chunkX;
                    masks[cell] |= 1UL << index;
                }
            }
        }

        return new CurrentFieldState { Id = 1, Zones = zones, CellMasks = masks };
    }
}
