using Xunit;

namespace Sea.Server.Tests;

public sealed class ContentIndexTests
{
    private static readonly GameContent Catalog = Tier1.Content;

    [Fact]
    public void Every_ammunition_is_indexed_by_its_own_code()
    {
        var index = ContentIndex.AmmunitionByCode(Catalog);

        Assert.Equal(ContentIndex.CodeSlots, index.Length);
        foreach (var ammunition in Catalog.Ammunition)
        {
            Assert.Same(ammunition, index[(byte)ammunition.Code]);
        }
    }

    [Fact]
    public void Ammunition_codes_no_catalog_entry_claims_stay_empty()
    {
        var index = ContentIndex.AmmunitionByCode(Catalog);
        var claimed = new HashSet<byte>();
        foreach (var ammunition in Catalog.Ammunition)
        {
            claimed.Add((byte)ammunition.Code);
        }

        for (var code = 0; code < index.Length; code++)
        {
            Assert.Equal(claimed.Contains((byte)code), index[code] is not null);
        }

        // AmmunitionCode.None is the parse failure sentinel and must never resolve to content.
        Assert.Null(index[(byte)AmmunitionCode.None]);
    }

    [Fact]
    public void Every_ability_is_indexed_by_its_own_code()
    {
        var index = ContentIndex.AbilityByCode(Catalog);

        Assert.Equal(ContentIndex.CodeSlots, index.Length);
        foreach (var ability in Catalog.Abilities)
        {
            Assert.Same(ability, index[(byte)ability.Code]);
        }
    }

    [Fact]
    public void Ability_codes_no_catalog_entry_claims_stay_empty()
    {
        var index = ContentIndex.AbilityByCode(Catalog);
        var claimed = new HashSet<byte>();
        foreach (var ability in Catalog.Abilities)
        {
            claimed.Add((byte)ability.Code);
        }

        for (var code = 0; code < index.Length; code++)
        {
            Assert.Equal(claimed.Contains((byte)code), index[code] is not null);
        }

        // AbilityCode.None is the parse failure sentinel and must never resolve to content.
        Assert.Null(index[(byte)AbilityCode.None]);
    }

    [Fact]
    public void Two_abilities_sharing_a_code_are_rejected()
    {
        var first = Catalog.Abilities[0];
        var collided = Catalog with { Abilities = [first, first with { Id = "ability_copy" }] };

        var error = Assert.Throws<InvalidOperationException>(
            () => ContentIndex.AbilityByCode(collided));
        Assert.Equal(
            $"Ability code '{first.Code}' is claimed by both '{first.Id}' and 'ability_copy'.",
            error.Message,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Every_npc_is_indexed_by_its_own_archetype_code()
    {
        var index = ContentIndex.NpcByArchetypeCode(Catalog);

        Assert.Equal(ContentIndex.CodeSlots, index.Length);
        foreach (var npc in Catalog.Npcs)
        {
            Assert.Same(npc, index[(byte)npc.Code]);
        }
    }

    [Fact]
    public void Npc_codes_no_catalog_entry_claims_stay_empty()
    {
        var index = ContentIndex.NpcByArchetypeCode(Catalog);
        var claimed = new HashSet<byte>();
        foreach (var npc in Catalog.Npcs)
        {
            claimed.Add((byte)npc.Code);
        }

        for (var code = 0; code < index.Length; code++)
        {
            Assert.Equal(claimed.Contains((byte)code), index[code] is not null);
        }

        Assert.Null(index[(byte)ShipArchetypeCode.PlayerSloop]);
    }

    [Fact]
    public void Two_ammunition_entries_sharing_a_code_are_rejected()
    {
        var round = Catalog.Ammunition.Single(ammo => ammo.Code == AmmunitionCode.Round);
        var collided = Catalog with { Ammunition = [round, round with { Id = "round_copy" }] };

        var error = Assert.Throws<InvalidOperationException>(
            () => ContentIndex.AmmunitionByCode(collided));
        Assert.Equal(
            "Ammunition code 'Round' is claimed by both 'round' and 'round_copy'.",
            error.Message,
            StringComparer.Ordinal);
    }

    [Fact]
    public void Two_npcs_sharing_an_archetype_code_are_rejected()
    {
        var first = Catalog.Npcs[0];
        var collided = Catalog with { Npcs = [first, first with { Id = "patrol_copy" }] };

        var error = Assert.Throws<InvalidOperationException>(
            () => ContentIndex.NpcByArchetypeCode(collided));
        Assert.Equal(
            $"Npc code '{first.Code}' is claimed by both '{first.Id}' and 'patrol_copy'.",
            error.Message,
            StringComparer.Ordinal);
    }

    [Fact]
    public void An_empty_catalog_family_indexes_to_all_empty_slots()
    {
        var index = ContentIndex.AmmunitionByCode(Catalog with { Ammunition = [] });

        Assert.All(index, Assert.Null);
    }

    [Fact]
    public void Null_content_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ContentIndex.AmmunitionByCode(null!));
        Assert.Throws<ArgumentNullException>(() => ContentIndex.AbilityByCode(null!));
        Assert.Throws<ArgumentNullException>(() => ContentIndex.NpcByArchetypeCode(null!));
    }

    [Fact]
    public void ById_finds_every_entry_by_its_own_id()
    {
        var index = ContentIndex.ById(Catalog.Hulls, hull => hull.Id, "Hull");

        foreach (var hull in Catalog.Hulls)
        {
            Assert.True(index.ContainsKey(hull.Id));
            Assert.Same(hull, index[hull.Id]);
        }

        Assert.False(index.ContainsKey("missing_hull"));
    }

    [Fact]
    public void Two_hulls_sharing_an_id_are_rejected()
    {
        var first = Tier1.Hull with { Id = "dup" };
        var second = Tier1.Hull with { Id = "dup" };

        var error = Assert.Throws<InvalidOperationException>(
            () => ContentIndex.ById(new[] { first, second }, hull => hull.Id, "Hull"));
        Assert.Equal("Hull id 'dup' is declared twice.", error.Message, StringComparer.Ordinal);
    }
}
