using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

// One row per validation rule: a catalog that breaks exactly that rule and the message it must
// produce. The message is matched in full so a mislabelled field is caught as well as a missing check.
public sealed class ContentValidationTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();
    private static readonly MapContent Map = Catalog.Maps[0];

    public static TheoryData<string, GameContent> RejectedContent => new()
    {
        { "StatCaps: damage bonus cap must be positive.", Caps(caps => caps with { DamageBonusCap = 0f }) },
        { "StatCaps: hit point bonus cap must be positive.", Caps(caps => caps with { HitPointBonusCap = 0f }) },
        { "StatCaps: armor points cap must be positive.", Caps(caps => caps with { ArmorPointsCap = 0f }) },
        { "StatCaps: speed bonus cap must be positive.", Caps(caps => caps with { SpeedBonusCap = 0f }) },
        { "StatCaps: turn bonus cap must be positive.", Caps(caps => caps with { TurnBonusCap = 0f }) },
        { "StatCaps: repair amount bonus cap must be positive.", Caps(caps => caps with { RepairAmountBonusCap = 0f }) },
        { "StatCaps: combat power budget must be positive.", Caps(caps => caps with { CombatPowerBudget = 0f }) },
        { "StatCaps: combat power armor weight must be positive.", Caps(caps => caps with { CombatPowerArmorWeight = 0f }) },
        { "StatCaps: reload floor must be positive.", Caps(caps => caps with { ReloadFloorSeconds = 0f }) },
        { "StatCaps: fire minimum interval must be positive.", Caps(caps => caps with { FireMinIntervalSeconds = 0f }) },
        { "StatCaps: magazine refill idle seconds must be positive.", Caps(caps => caps with { MagazineRefillIdleSeconds = 0f }) },
        { "StatCaps: burn per second must be positive.", Caps(caps => caps with { BurnPerSecond = 0f }) },
        { "StatCaps: burn duration must be positive.", Caps(caps => caps with { BurnDurationSeconds = 0f }) },
        { "StatCaps: repair channel must be positive.", Caps(caps => caps with { RepairChannelSeconds = 0f }) },
        { "StatCaps: repair cooldown must be positive.", Caps(caps => caps with { RepairCooldownSeconds = 0f }) },
        { "StatCaps: repair fatigue must be positive.", Caps(caps => caps with { RepairFatigue = 0f }) },
        { "StatCaps: repair fatigue window must be positive.", Caps(caps => caps with { RepairFatigueWindowSeconds = 0f }) },
        { "StatCaps: repair cancel threshold must be positive.", Caps(caps => caps with { RepairCancelThreshold = 0f }) },
        { "StatCaps: kit heal amount must be positive.", Caps(caps => caps with { KitHealAmount = 0f }) },
        { "StatCaps: kit cooldown must be positive.", Caps(caps => caps with { KitCooldownSeconds = 0f }) },
        { "StatCaps: respawn seconds must be positive.", Caps(caps => caps with { RespawnSeconds = 0f }) },
        { "StatCaps: spawn shield seconds must be positive.", Caps(caps => caps with { SpawnShieldSeconds = 0f }) },
        { "StatCaps: magazine bonus cap must be positive.", Caps(caps => caps with { MagazineBonusCap = 0 }) },
        { "StatCaps: range bonus cap must be positive.", Caps(caps => caps with { RangeBonusCapSquares = 0 }) },
        { "StatCaps: gold base must be positive.", Caps(caps => caps with { GoldBase = 0 }) },
        { "StatCaps: reload bonus cap must be between 0 and 1.", Caps(caps => caps with { ReloadBonusCap = 0f }) },
        { "StatCaps: repair channel bonus cap must be between 0 and 1.", Caps(caps => caps with { RepairChannelBonusCap = 1f }) },
        { "StatCaps: repair base amount must be between 0 and 1.", Caps(caps => caps with { RepairBaseAmount = 1f }) },
        { "StatCaps: burn heal multiplier must not be negative.", Caps(caps => caps with { BurnHealMultiplier = -1f }) },
        { "StatCaps: NPC hit point multipliers must have 6 entries.", Caps(caps => caps with { NpcHitPointMultipliers = [1f] }) },
        { "StatCaps: NPC dps multipliers for tier 3 must be positive.", Caps(caps => caps with { NpcDpsMultipliers = [1f, 1f, 0f, 1f, 1f, 1f] }) },
        { "StatCaps: NPC armor by tier must have 6 entries.", Caps(caps => caps with { NpcArmorByTier = [0.1f] }) },
        { "StatCaps: NPC armor for tier 6 must be between 0 and 0.45.", Caps(caps => caps with { NpcArmorByTier = [0.1f, 0.1f, 0.15f, 0.2f, 0.2f, 0.9f] }) },
        { "StatCaps: gold growth must be above 1.", Caps(caps => caps with { GoldGrowth = 1f }) },
        { "At least one hull is required.", Catalog with { Hulls = [] } },
        { "Duplicate hull id 'hull_t1'.", Catalog with { Hulls = [Catalog.Hulls[0], Catalog.Hulls[0]] } },
        { "hull id is empty.", Hull(hull => hull with { Id = " " }) },
        { "hull_t1: name is empty.", Hull(hull => hull with { Name = "" }) },
        { "hull_t1: front armor must be between 0 and 0.45.", Hull(hull => hull with { ArmorFront = 1f }) },
        { "hull_t1: side armor must be between 0 and 0.45.", Hull(hull => hull with { ArmorSides = 1f }) },
        { "hull_t1: back armor must be between 0 and 0.45.", Hull(hull => hull with { ArmorBack = 1f }) },
        { "hull_t1: cannon slots must be positive.", Hull(hull => hull with { CannonSlots = 0 }) },
        { "hull_t1: magazine must be positive.", Hull(hull => hull with { Magazine = 0 }) },
        { "hull_t1: tier must be positive.", Hull(hull => hull with { Tier = 0 }) },
        { "hull_t1: turn rate must be positive.", Hull(hull => hull with { TurnDegreesPerSecond = 0f }) },
        { "hull_t1: map rank required must be positive.", Hull(hull => hull with { MapRankRequired = 0 }) },
        { "At least one cannon is required.", Catalog with { Cannons = [] } },
        { "Duplicate cannon id 'cannon_t1'.", Catalog with { Cannons = [Catalog.Cannons[0], Catalog.Cannons[0]] } },
        { "cannon id is empty.", Cannon(cannon => cannon with { Id = "" }) },
        { "cannon_t1: name is empty.", Cannon(cannon => cannon with { Name = "" }) },
        { "cannon_t1: damage must be positive.", Cannon(cannon => cannon with { Damage = 0 }) },
        { "cannon_t1: range must be positive.", Cannon(cannon => cannon with { RangeSquares = 0 }) },
        { "cannon_t1: tier must be positive.", Cannon(cannon => cannon with { Tier = 0 }) },
        { "cannon_t1: reload 1.23s is below the floor 1.5s.", Cannon(cannon => cannon with { ReloadSeconds = 1.234f }) },
        { "At least one ammunition is required.", Catalog with { Ammunition = [] } },
        { "round: name is empty.", Ammunition(ammo => ammo with { Name = "" }) },
        { "round: ammunition code must not be None.", Ammunition(ammo => ammo with { Code = AmmunitionCode.None }) },
        { "round_copy: duplicate ammunition code 'Round'.", Catalog with { Ammunition = [Catalog.Ammunition[0], Catalog.Ammunition[0] with { Id = "round_copy" }] } },
        { "round: damage multiplier must be positive.", Ammunition(ammo => ammo with { DamageMultiplier = 0f }) },
        { "round: reload multiplier must be positive.", Ammunition(ammo => ammo with { ReloadMultiplier = 0f }) },
        { "round: range multiplier must be positive.", Ammunition(ammo => ammo with { RangeMultiplier = 0f }) },
        { "round: effect magnitude must not be negative.", Ammunition(ammo => ammo with { EffectMagnitude = -1f }) },
        { "round: effect duration must not be negative.", Ammunition(ammo => ammo with { EffectDurationSeconds = -1f }) },
        { "At least one npc is required.", Catalog with { Npcs = [] } },
        { "Duplicate npc id 'patrol'.", Catalog with { Npcs = [Catalog.Npcs[0], Catalog.Npcs[0]] } },
        { "npc id is empty.", Npc(npc => npc with { Id = "" }) },
        { "patrol: name is empty.", Npc(npc => npc with { Name = "" }) },
        { "patrol: npc code must not be PlayerSloop.", Npc(npc => npc with { Code = ShipArchetypeCode.PlayerSloop }) },
        { "patrol: code 'Raider' does not match the id.", Npc(npc => npc with { Code = ShipArchetypeCode.Raider }) },
        { "patrol: tier must be positive.", Npc(npc => npc with { Tier = 0 }) },
        { "patrol: maximum speed must be positive.", Npc(npc => npc with { MaximumSpeed = 0f }) },
        { "patrol: aggro range must be between 0 and 44.", Npc(npc => npc with { AggroRange = -1f }) },
        { "patrol: aggro range must be between 0 and 44.", Npc(npc => npc with { AggroRange = 45f }) },
        { "patrol: desired range must be between 0 and 44.", Npc(npc => npc with { DesiredRange = 45f }) },
        { "patrol: hull must be positive.", Npc(npc => npc with { Hull = 0 }) },
        { "patrol: cannon damage must be positive.", Npc(npc => npc with { CannonDamage = 0 }) },
        { "patrol: gold reward must be positive.", Npc(npc => npc with { GoldReward = 0 }) },
        { "patrol: experience reward must be positive.", Npc(npc => npc with { ExperienceReward = 0 }) },
        { "Map 1: map rank must be positive.", Catalog with { Maps = [Map with { Code = "", MapRank = 0 }] } },
        { "Map 2: map rank must be positive.", Catalog with { Maps = [Map, Map with { MapId = 2, MapRank = 0 }] } },
        { "Map 1/1: object 1: radius must be positive.", WorldObject(item => item with { Radius = 0f }) },
    };

    public static TheoryData<string, GameContent> AcceptedContent => new()
    {
        { "a heading on the far edge of the circle", WorldObject(item => item with { DirectionDegrees = 360f }) },
        { "an unarmoured hull face", Hull(hull => hull with { ArmorFront = 0f }) },
        { "a reload exactly on the floor", Cannon(cannon => cannon with { ReloadSeconds = Catalog.StatCaps.ReloadFloorSeconds }) },
    };

    [Theory]
    [MemberData(nameof(RejectedContent))]
    public void Broken_content_reports_the_rule_it_breaks(string expected, GameContent content)
    {
        Assert.Contains(expected, ContentCatalog.Validate(content), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AcceptedContent))]
    public void Boundary_content_is_accepted(string description, GameContent content)
    {
        Assert.NotEmpty(description);
        Assert.Empty(ContentCatalog.Validate(content));
    }

    [Fact]
    public void A_negative_object_radius_reports_only_the_positivity_rule()
    {
        var errors = ContentCatalog.Validate(WorldObject(item => item with { Radius = -1f }));

        var radius = Assert.Single(errors, error => error.StartsWith("Map 1/1: object 1: radius", StringComparison.Ordinal));
        Assert.Equal("Map 1/1: object 1: radius must be positive.", radius);
    }

    private static GameContent Caps(Func<StatCapsContent, StatCapsContent> change) =>
        Catalog with { StatCaps = change(Catalog.StatCaps) };

    private static GameContent Hull(Func<HullContent, HullContent> change) =>
        Catalog with { Hulls = [change(Catalog.Hulls[0])] };

    private static GameContent Cannon(Func<CannonContent, CannonContent> change) =>
        Catalog with { Cannons = [change(Catalog.Cannons[0])] };

    private static GameContent Ammunition(Func<AmmunitionContent, AmmunitionContent> change) =>
        Catalog with { Ammunition = [change(Catalog.Ammunition[0])] };

    private static GameContent Npc(Func<NpcContent, NpcContent> change) =>
        Catalog with { Npcs = [change(Catalog.Npcs[0])] };

    private static GameContent WorldObject(Func<WorldObjectContent, WorldObjectContent> change) =>
        Catalog with { Maps = [Map with { Objects = [change(Map.Objects[0])] }] };
}
