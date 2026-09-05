using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

// JSON-to-catalog drift is guarded by `pnpm quality:content`, which byte-diffs the whole
// generated file; these tests only cover the validation rules.
public sealed class ContentCatalogTests
{
    private static readonly GameContent Catalog = ContentCatalog.CreateDefault();

    [Fact]
    public void Default_catalog_is_valid()
    {
        Assert.Empty(ContentCatalog.Validate(Catalog));
    }

    [Fact]
    public void Null_content_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ContentCatalog.Validate(null!));
    }

    [Fact]
    public void Zero_hit_point_hull_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Hulls = [Catalog.Hulls[0] with { HitPoints = 0 }] });
        Assert.Contains("hull_t1: hit points must be positive.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Not_a_number_hull_speed_is_rejected()
    {
        var errors = ContentCatalog.Validate(
            Catalog with { Hulls = [Catalog.Hulls[0] with { SpeedSquaresPerSecond = float.NaN }] });
        Assert.Contains("hull_t1: speed must be positive.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_ammunition_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Ammunition = [.. Catalog.Ammunition, Catalog.Ammunition[0]] });
        Assert.Contains("Duplicate ammunition id 'round'.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Ammunition_code_must_match_the_id()
    {
        var errors = ContentCatalog.Validate(
            Catalog with { Ammunition = [Catalog.Ammunition[0] with { Code = AmmunitionCode.Chain }] });
        Assert.Contains("round: code 'Chain' does not match the id.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Missing_round_shot_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with
        {
            Ammunition = Catalog.Ammunition.Where(ammo => ammo.Code != AmmunitionCode.Round).ToList(),
        });
        Assert.Contains("Ammunition must include the Round baseline.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Short_terrain_row_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { TerrainRows = [.. map.TerrainRows.Take(399), new string('.', 399)] }],
        });
        Assert.Contains("Map 1/1: terrain row 399 has 399 columns, expected 400.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Port_on_land_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Maps = [Catalog.Maps[0] with { PortX = 270f, PortY = 250f }] });
        Assert.Contains("Map 1/1: the port sector (270, 250) must be water.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Port_outside_the_map_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Maps = [Catalog.Maps[0] with { PortX = 500f }] });
        Assert.Contains("Map 1/1: the port lies outside the map.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Blocking_object_off_land_is_rejected()
    {
        var map = Catalog.Maps[0];
        var moved = map.Objects.Select(item => item.EntityId == 2 ? item with { X = 0f, Y = 0f } : item).ToList();
        var errors = ContentCatalog.Validate(Catalog with { Maps = [map with { Objects = moved }] });
        Assert.Contains(
            "Map 1/1: object 2 blocks movement but its sector (0, 0) is not land.",
            errors,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Unknown_world_object_kind_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Objects = [map.Objects[0] with { Kind = "wharf" }] }],
        });
        Assert.Contains("Map 1/1: object 1: unknown kind 'wharf'.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Blocks_movement_disagreeing_with_the_kind_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Objects = [map.Objects[0] with { BlocksMovement = true }] }],
        });
        Assert.Contains(
            "Map 1/1: object 1: blocksMovement disagrees with kind 'harbor'.",
            errors,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Out_of_range_object_heading_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Objects = [map.Objects[0] with { DirectionDegrees = 400f }] }],
        });
        Assert.Contains("Map 1/1: object 1: direction must be between 0 and 360.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_object_entity_id_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Objects = [.. map.Objects, map.Objects[0]] }],
        });
        Assert.Contains("Map 1/1: duplicate object entity id 1.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_current_zone_id_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Currents = [.. map.Currents, map.Currents[0]] }],
        });
        Assert.Contains("Map 1/1: duplicate current zone id 1.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Oversized_current_radius_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Currents = [map.Currents[0] with { Radius = 100f }] }],
        });
        Assert.Contains("Map 1/1: current zone 1: radius must be between 0 and 56.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Oversized_world_object_radius_is_rejected()
    {
        // The one gate this bound actually guards, and it guards every kind of object
        // rather than only the storm it is sized to. Without this the number could be
        // moved to anything at all and the whole suite would stay green.
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with
        {
            Maps = [map with { Objects = [map.Objects[0] with { Radius = 100f }] }],
        });
        Assert.Contains(
            $"Map 1/1: object {map.Objects[0].EntityId}: radius must be between 0 and 40.",
            errors,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Cannon_reload_below_the_floor_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Cannons = [Catalog.Cannons[0] with { ReloadSeconds = 1f }] });
        Assert.Contains("cannon_t1: reload 1s is below the floor 1.5s.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Armor_absolute_max_outside_the_unit_interval_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { StatCaps = Catalog.StatCaps with { ArmorAbsoluteMax = 1f } });
        Assert.Contains("StatCaps: armor absolute max must be between 0 and 1.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void A_non_positive_cannon_slot_bonus_cap_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { StatCaps = Catalog.StatCaps with { CannonSlotBonusCap = 0 } });
        Assert.Contains("StatCaps: cannon slot bonus cap must be positive.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Npc_on_an_unknown_map_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Npcs = [Catalog.Npcs[0] with { MapId = 9 }] });
        Assert.Contains("skiff: map 9 does not exist.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_map_id_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with { Maps = [map, map with { Code = "1/2" }] });
        Assert.Contains("Duplicate map id 1.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_map_code_is_rejected()
    {
        var map = Catalog.Maps[0];
        var errors = ContentCatalog.Validate(Catalog with { Maps = [map, map with { MapId = 2 }] });
        Assert.Contains("Duplicate map code '1/1'.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Duplicate_npc_archetype_code_is_rejected()
    {
        var npc = Catalog.Npcs[0];
        var errors = ContentCatalog.Validate(Catalog with { Npcs = [npc, npc with { Id = "skiff_copy" }] });
        Assert.Contains("skiff_copy: duplicate npc code 'Skiff'.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Zero_desired_range_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Npcs = [Catalog.Npcs[0] with { DesiredRangeSquares = 0f }] });
        Assert.Contains("skiff: desired range must be positive.", errors, StringComparer.Ordinal);
    }

    // Content/Catalog.cs resolves these ids at module load as StarterHull/StarterCannon. Renaming
    // the content id without updating Catalog.StarterHullId/StarterCannonId would break every login.
    [Fact]
    public void Starter_loadout_ids_match_the_module_constants()
    {
        Assert.Equal("hull_t1", Tier1.Hull.Id);
        Assert.Equal("cannon_t1", Tier1.Cannon.Id);
    }

    // Phase 8.1: all five hull tiers exist, one per Map Rank (SEA_2_MATH §2.4 for
    // HP/armor/slots/cost/magazine; SEA_5 §4.4 for speed).
    [Fact]
    public void All_five_hull_tiers_are_present()
    {
        var tiers = Catalog.Hulls.Select(hull => hull.Tier).OrderBy(tier => tier).ToArray();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, tiers);
    }

    [Theory]
    [InlineData((byte)1, "hull_t1", 1600u, 0.15f, 0.08f, 0.03f, (byte)8, 0u)]
    [InlineData((byte)2, "hull_t2", 4800u, 0.18f, 0.10f, 0.04f, (byte)14, 20000u)]
    [InlineData((byte)3, "hull_t3", 10500u, 0.22f, 0.12f, 0.05f, (byte)20, 120000u)]
    [InlineData((byte)4, "hull_t4", 20000u, 0.26f, 0.14f, 0.06f, (byte)26, 500000u)]
    [InlineData((byte)5, "hull_t5", 36000u, 0.30f, 0.16f, 0.08f, (byte)32, 2000000u)]
    public void Hull_tiers_match_the_design_sheet(
        byte tier, string id, uint hitPoints, float front, float sides, float back, byte slots, uint cost)
    {
        var hull = Catalog.Hulls.Single(candidate => candidate.Tier == tier);

        Assert.Equal(id, hull.Id);
        Assert.Equal(hitPoints, hull.HitPoints);
        Assert.Equal(front, hull.ArmorFront, 3);
        Assert.Equal(sides, hull.ArmorSides, 3);
        Assert.Equal(back, hull.ArmorBack, 3);
        Assert.Equal(slots, hull.CannonSlots);
        Assert.Equal((byte)3, hull.Magazine);
        Assert.Equal(cost, hull.CostGold);
        Assert.Equal(tier, hull.MapRankRequired);
    }

    // Speeds are SEA_5 §4.4: a bigger hull is always the slower one.
    [Theory]
    [InlineData((byte)1, 5.6f)]
    [InlineData((byte)2, 5.3f)]
    [InlineData((byte)3, 5.0f)]
    [InlineData((byte)4, 4.7f)]
    [InlineData((byte)5, 4.4f)]
    public void Hull_speeds_match_SEA_5(byte tier, float speed)
    {
        var hull = Catalog.Hulls.Single(candidate => candidate.Tier == tier);
        Assert.Equal(speed, hull.SpeedSquaresPerSecond, 3);
    }

    // Phase 8.2: all five cannon tiers exist (SEA_2_MATH §2.5 for damage/reload/cost).
    [Fact]
    public void All_five_cannon_tiers_are_present()
    {
        var tiers = Catalog.Cannons.Select(cannon => cannon.Tier).OrderBy(tier => tier).ToArray();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, tiers);
    }

    [Theory]
    [InlineData((byte)1, "cannon_t1", 20u, 3.0f, 500u)]
    [InlineData((byte)2, "cannon_t2", 32u, 2.9f, 3000u)]
    [InlineData((byte)3, "cannon_t3", 48u, 2.8f, 15000u)]
    [InlineData((byte)4, "cannon_t4", 68u, 2.7f, 50000u)]
    [InlineData((byte)5, "cannon_t5", 92u, 2.6f, 150000u)]
    public void Cannon_tiers_match_the_design_sheet(
        byte tier, string id, uint damage, float reloadSeconds, uint cost)
    {
        var cannon = Catalog.Cannons.Single(candidate => candidate.Tier == tier);

        Assert.Equal(id, cannon.Id);
        Assert.Equal(damage, cannon.Damage);
        Assert.Equal(reloadSeconds, cannon.ReloadSeconds, 3);
        Assert.Equal(cost, cannon.CostGold);
    }

    // Ranges are SEA_5 §7.1. The sheet is asserted against RangeRules rather than repeating
    // the figures, so the content and the rule that reads it cannot drift apart.
    [Fact]
    public void Cannon_range_matches_RangeRules()
    {
        foreach (var tier in new byte[] { 1, 2, 3, 4, 5 })
        {
            var cannon = Catalog.Cannons.Single(candidate => candidate.Tier == tier);
            Assert.Equal((byte)RangeRules.BaseRangeSquares(tier), cannon.RangeSquares);
        }
    }
}
