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
            Maps = [map with { TerrainRows = [.. map.TerrainRows.Take(19), "..................."] }],
        });
        Assert.Contains("Map 1/1: terrain row 19 has 19 columns, expected 20.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Port_on_land_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Maps = [Catalog.Maps[0] with { PortX = 35f, PortY = 20f }] });
        Assert.Contains("Map 1/1: the port sector (13, 12) must be water.", errors, StringComparer.Ordinal);
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
            "Map 1/1: object 2 blocks movement but its sector (10, 10) is not land.",
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
        Assert.Contains("Map 1/1: current zone 1: radius must be between 0 and 28.", errors, StringComparer.Ordinal);
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
        Assert.Contains("patrol: map 9 does not exist.", errors, StringComparer.Ordinal);
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
        var errors = ContentCatalog.Validate(Catalog with { Npcs = [npc, npc with { Id = "patrol_copy" }] });
        Assert.Contains("patrol_copy: duplicate npc code 'Patrol'.", errors, StringComparer.Ordinal);
    }

    [Fact]
    public void Zero_desired_range_is_rejected()
    {
        var errors = ContentCatalog.Validate(Catalog with { Npcs = [Catalog.Npcs[0] with { DesiredRangeSquares = 0f }] });
        Assert.Contains("patrol: desired range must be positive.", errors, StringComparer.Ordinal);
    }

    // Content/Catalog.cs resolves these ids at module load as StarterHull/StarterCannon. Renaming
    // the content id without updating Catalog.StarterHullId/StarterCannonId would break every login.
    [Fact]
    public void Starter_loadout_ids_match_the_module_constants()
    {
        Assert.Equal("hull_t1", Tier1.Hull.Id);
        Assert.Equal("cannon_t1", Tier1.Cannon.Id);
    }
}
