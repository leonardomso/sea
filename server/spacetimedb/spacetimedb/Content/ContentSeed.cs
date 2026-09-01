using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    private static void SeedContent(ReducerContext ctx)
    {
        var content = ContentCatalog.CreateDefault();
        var errors = ContentCatalog.Validate(content);
        if (errors.Count != 0)
        {
            throw new Exception(string.Join(" ", errors));
        }

        foreach (var ammunition in content.Ammunition)
        {
            ctx.Db.AmmoDefinition.Insert(new AmmoDefinition
            {
                AmmoId = ammunition.Id,
                HullDamage = ammunition.HullDamage,
                SailDamage = ammunition.SailDamage,
                CannonDamage = ammunition.CannonDamage,
                CrewDamage = ammunition.CrewDamage,
                RangeMultiplier = ammunition.RangeMultiplier,
                AppliedStatus = ammunition.AppliedStatus,
            });
        }

        foreach (var ability in content.Abilities)
        {
            ctx.Db.AbilityDefinition.Insert(new AbilityDefinition
            {
                AbilityId = ability.Id,
                CooldownTicks = ability.CooldownTicks,
                DurationTicks = ability.DurationTicks,
            });
        }

        foreach (var npc in content.Npcs)
        {
            ctx.Db.NpcDefinition.Insert(new NpcDefinition
            {
                NpcId = npc.Id,
                AggroRange = npc.AggroRange,
                DesiredRange = npc.DesiredRange,
                Hull = npc.Hull,
            });
        }

        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 1, RequiredExperience = 0 });
        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 2, RequiredExperience = 500 });
        ctx.Db.LevelDefinition.Insert(new LevelDefinition { Level = 3, RequiredExperience = 1_500 });
    }

    private static void SeedWorld(ReducerContext ctx)
    {
        InsertWorldObject(ctx, 1, "harbor", 0f, 0f, 8f, false);
        InsertWorldObject(ctx, 2, "island", 35f, 20f, 12f, true);
        InsertWorldObject(ctx, 3, "reef", -30f, -25f, 10f, true);
        InsertWorldObject(ctx, 4, "island", -46f, 43f, 16f, true);
        InsertWorldObject(ctx, 5, "island", 61f, -48f, 15f, true);
        InsertWorldObject(ctx, 6, "island", -63f, -58f, 11f, true);
        InsertWorldObject(ctx, 7, "island", 4f, 70f, 9f, true);
        InsertWorldObject(ctx, 8, "reef", 24f, -61f, 8f, true);
        InsertWorldObject(ctx, 9, "reef", 68f, 58f, 9f, true);
        InsertWorldObject(ctx, 11, "shoal", -4f, -42f, 15f, false, intensity: 0.7f);
        InsertWorldObject(ctx, 12, "shoal", 48f, 45f, 12f, false, intensity: 0.8f);
        InsertWorldObject(
            ctx,
            13,
            "storm",
            -72f,
            3f,
            14f,
            false,
            directionDegrees: 72f,
            movementSpeed: 1.5f,
            intensity: 1f);

        var trainingShip = CreateShip(10, "patrol", "npc", 45f, -10f);
        trainingShip.Hull = WorldRules.EnemyInitialHealth;
        trainingShip.MaxHull = WorldRules.EnemyInitialHealth;
        ctx.Db.Ship.Insert(trainingShip);
        ctx.Db.NpcAi.Insert(new NpcAi
        {
            ShipEntityId = trainingShip.EntityId,
            ArchetypeId = "patrol",
            IsActive = true,
            NextDecisionTick = 0,
            HomeSeed = 10,
        });
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
        InsertCurrentZone(ctx, 1, -55f, 35f, 28f, 70f, 1.25f);
        InsertCurrentZone(ctx, 2, 55f, -45f, 24f, 235f, 1f);
    }

    private static void InsertCurrentZone(
        ReducerContext ctx,
        ulong zoneId,
        float x,
        float y,
        float radius,
        float directionDegrees,
        float strength)
    {
        ctx.Db.CurrentZone.Insert(new CurrentZone
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
        });
    }

}
