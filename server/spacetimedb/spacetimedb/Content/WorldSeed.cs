using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    /// <summary>How many hostiles keep the map's water busy at once.</summary>
    private const int CommonSpawnSlots = 12;

    /// <summary>Section 5.3's cadence: one sail in five is a veteran rather than a common.</summary>
    private const int VeteranEverySlots = 5;

    private const byte CommonTier = 1;
    private const byte VeteranTier = 2;

    /// <summary>The first entity id the world's own hulls take; players are allocated above them.</summary>
    private const ulong FirstNpcEntityId = 10;

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
        SeedNpcs(ctx, HostileHomeBlockers(ctx, SpawnBlockers(ctx)));
    }

    /// <summary>
    /// The map's roster: commons everywhere, a veteran every fifth sail, and the named captain
    /// once, with the escorts she calls moored beside her.
    /// </summary>
    private static void SeedNpcs(ReducerContext ctx, List<SpawnBlocker> blockers)
    {
        var npcs = Catalog.Content.Npcs;
        var commons = OfTier(npcs, CommonTier);
        var veterans = OfTier(npcs, VeteranTier);
        var entityId = FirstNpcEntityId;
        for (var slot = 0; slot < CommonSpawnSlots; slot++)
        {
            // A captain who works one patch of water should meet both of the map's commons and,
            // often enough to be worth watching for, something heavier.
            var definition = veterans.Count > 0 && slot % VeteranEverySlots == VeteranEverySlots - 1
                ? veterans[0]
                : commons[slot % commons.Count];
            SeedNpc(ctx, blockers, entityId, definition, slot, leaderEntityId: 0);
            entityId++;
        }

        SeedNamedCaptain(ctx, blockers, entityId, veterans);
    }

    /// <summary>
    /// The named ship and the two hulls she calls at half health. They are seeded with her rather
    /// than conjured mid-fight: the sea carries the same number of ships whatever happens on it,
    /// and no tick has to insert or delete one.
    /// </summary>
    private static void SeedNamedCaptain(
        ReducerContext ctx,
        List<SpawnBlocker> blockers,
        ulong entityId,
        IReadOnlyList<NpcContent> veterans)
    {
        var named = Named(Catalog.Content.Npcs);
        if (named is null || veterans.Count == 0)
        {
            return;
        }

        var home = SeedNpc(ctx, blockers, entityId, named, CommonSpawnSlots, leaderEntityId: 0);
        for (var escort = 0; escort < NpcRules.CallHelpCount; escort++)
        {
            var escortId = entityId + 1 + (ulong)escort;
            var berth = SpawnRules.TryFindSafePositionNear(
                escortId,
                home.X,
                home.Y,
                NpcRules.HomeAnchorRadius,
                blockers,
                out var mooring)
                ? mooring
                : home;
            SeedNpcAt(ctx, escortId, veterans[0], berth, CommonSpawnSlots, entityId);
        }
    }

    private static List<NpcContent> OfTier(IReadOnlyList<NpcContent> npcs, byte tier)
    {
        var matches = new List<NpcContent>();
        foreach (var npc in npcs)
        {
            if (npc.Tier == tier)
            {
                matches.Add(npc);
            }
        }

        return matches.Count > 0
            ? matches
            : throw new InvalidOperationException($"The catalog has no tier {tier} enemy.");
    }

    /// <summary>The one enemy on the map that does not fight its losing battles alone.</summary>
    private static NpcContent? Named(IReadOnlyList<NpcContent> npcs)
    {
        foreach (var npc in npcs)
        {
            if (npc.CallsForHelp)
            {
                return npc;
            }
        }

        return null;
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

    private static SpawnPoint SeedNpc(
        ReducerContext ctx,
        IReadOnlyList<SpawnBlocker> blockers,
        ulong entityId,
        NpcContent definition,
        int slot,
        ulong leaderEntityId)
    {
        var spawn = FindSafeSpawn(
            blockers,
            entityId ^ unchecked((ulong)(slot + 1) * 0x9E3779B97F4A7C15UL));
        SeedNpcAt(ctx, entityId, definition, spawn, slot, leaderEntityId);
        return spawn;
    }

    /// <summary>
    /// One hostile, on the water it was given. Its magazine and its guns' reach are the baseline
    /// sloop's; everything the tier decides -- hull, volley, armour, speed, reach and bounty --
    /// comes from <see cref="NpcDerivation"/>, so no number here is authored twice.
    /// </summary>
    private static void SeedNpcAt(
        ReducerContext ctx,
        ulong entityId,
        NpcContent definition,
        SpawnPoint spawn,
        int slot,
        ulong leaderEntityId)
    {
        var stats = Catalog.NpcStatsByArchetypeCode[(byte)definition.Code];
        var ship = CreateShip(entityId, definition.Id, "npc", spawn.X, spawn.Y);
        ship.MaximumSpeed = stats.MaximumSpeedSquares;
        ship.Hull = stats.MaximumHull;
        ship.MaxHull = stats.MaximumHull;
        ship.VolleyDamage = stats.VolleyDamage;
        ship.ArmorFront = stats.Armor;
        ship.ArmorSides = stats.Armor;
        ship.ArmorBack = stats.Armor;
        ship.SelectedAmmoCode = (byte)definition.PreferredAmmunition;
        ship.EncounterId = entityId;
        ctx.Db.Ship.Insert(ship);
        InsertShipMovement(ctx, ship, CurrentSimulationTick(ctx));
        OpenNpcEncounter(ctx, ship, stats.GoldReward, definition.ExperienceReward, tick: 0);
        ctx.Db.NpcAi.Insert(new NpcAi
        {
            ShipEntityId = entityId,
            IsActive = true,
            DecisionShard = SimulationWorkRules.NpcShard(entityId),
            NextDecisionTick = (ulong)slot,
            HomeSeed = entityId * 17,
            HomeX = spawn.X,
            HomeY = spawn.Y,
            LeaderEntityId = leaderEntityId,
            HasCalledHelp = false,
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
