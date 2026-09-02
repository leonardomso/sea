using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ShipStatRulesTests
{
    private static readonly StatCapsContent Caps = Tier1.Caps;

    private static BonusSource Source(BonusSourceKind kind, ulong sourceId, StatBonuses bonuses) =>
        new(kind, sourceId, bonuses);

    [Fact]
    public void Tier_1_baseline_matches_the_design_sheet()
    {
        var sheet = Tier1.Sheet();

        Assert.Equal(160u, sheet.VolleyDamage);
        Assert.Equal(3000u, sheet.ReloadMilliseconds);
        Assert.Equal((byte)3, sheet.Magazine);
        Assert.Equal(1600u, sheet.MaxHitPoints);
        Assert.Equal(0.15f, sheet.ArmorFront, 3);
        Assert.Equal(0.08f, sheet.ArmorSides, 3);
        Assert.Equal(0.03f, sheet.ArmorBack, 3);
        Assert.Equal(2.4f, sheet.SpeedSquaresPerSecond, 3);
        Assert.Equal(60f, sheet.TurnDegreesPerSecond, 3);
        Assert.Equal((byte)8, sheet.RangeSquares);
        Assert.Equal(0.2f, sheet.RepairAmount, 3);
        Assert.Equal(3000u, sheet.RepairChannelMilliseconds);
        Assert.Equal(0f, sheet.CombatPowerUsed);
        Assert.Equal(0f, sheet.CombatPowerInactive);
        Assert.Equal(1f, sheet.FightScore, 3);
    }

    [Fact]
    public void Bonuses_are_added_then_capped()
    {
        var share = Caps.DamageBonusCap * 0.75f;
        Assert.True(share + share > Caps.DamageBonusCap, "the two sources must together exceed the cap");

        var sources = new[]
        {
            Source(BonusSourceKind.Plates, 1, StatBonuses.None with { Damage = share }),
            Source(BonusSourceKind.Skills, 2, StatBonuses.None with { Damage = share }),
        };

        var sheet = ShipStatRules.Compute(Tier1.Loadout(), sources, Caps);

        Assert.Equal((uint)(Tier1.Sheet().VolleyDamage * (1f + Caps.DamageBonusCap)), sheet.VolleyDamage);
        Assert.Equal(Caps.DamageBonusCap * 100f, sheet.CombatPowerUsed, 3);
        Assert.Equal(0f, sheet.CombatPowerInactive);
    }

    [Fact]
    public void Reload_never_drops_below_the_floor()
    {
        var reloadSeconds = Caps.ReloadFloorSeconds * 1.05f;
        var loadout = Tier1.Loadout() with { Cannon = Tier1.Cannon with { ReloadSeconds = reloadSeconds } };
        var bonus = StatBonuses.None with { Reload = Caps.ReloadBonusCap };
        var sources = new[] { Source(BonusSourceKind.Sails, 1, bonus) };

        var sheet = ShipStatRules.Compute(loadout, sources, Caps);

        Assert.True(reloadSeconds * (1f - Caps.ReloadBonusCap) < Caps.ReloadFloorSeconds, "the bonus must undercut it");
        Assert.Equal((uint)MathF.Round(Caps.ReloadFloorSeconds * 1000f), sheet.ReloadMilliseconds);
    }

    [Fact]
    public void Ammo_multipliers_scale_volley_and_reload()
    {
        var loadout = Tier1.Loadout() with { AmmoDamageMultiplier = 0.7f, AmmoReloadMultiplier = 1.1f };

        var sheet = ShipStatRules.Compute(loadout, Array.Empty<BonusSource>(), Caps);

        Assert.Equal(112u, sheet.VolleyDamage);
        Assert.Equal(3300u, sheet.ReloadMilliseconds);
    }

    [Fact]
    public void Over_budget_sources_are_dropped_from_the_end_of_the_order()
    {
        var damage = StatBonuses.None with { Damage = Caps.DamageBonusCap };
        var hullVariant = Source(BonusSourceKind.HullVariant, 1, damage);
        var plates = Source(BonusSourceKind.Plates, 2, StatBonuses.None with { HitPoints = Caps.HitPointBonusCap });

        var forward = ShipStatRules.Compute(Tier1.Loadout(), new[] { hullVariant, plates }, Caps);
        var reversed = ShipStatRules.Compute(Tier1.Loadout(), new[] { plates, hullVariant }, Caps);

        Assert.Equal(forward, reversed);
        Assert.Equal(Caps.DamageBonusCap * 100f, forward.CombatPowerUsed, 3);
        Assert.Equal(Caps.HitPointBonusCap * 100f, forward.CombatPowerInactive, 3);
        Assert.Equal((uint)(Tier1.Sheet().VolleyDamage * (1f + Caps.DamageBonusCap)), forward.VolleyDamage);
        Assert.Equal(Tier1.Sheet().MaxHitPoints, forward.MaxHitPoints);
    }

    [Fact]
    public void Same_kind_sources_are_ordered_by_source_id()
    {
        var first = Source(BonusSourceKind.Plates, 1, StatBonuses.None with { Damage = Caps.DamageBonusCap / 3f });
        var second = Source(BonusSourceKind.Plates, 2, StatBonuses.None with { HitPoints = Caps.HitPointBonusCap });
        var third = Source(BonusSourceKind.Plates, 3, StatBonuses.None with { ArmorPoints = Caps.ArmorPointsCap });

        var forward = ShipStatRules.Compute(Tier1.Loadout(), new[] { first, second, third }, Caps);
        var reversed = ShipStatRules.Compute(Tier1.Loadout(), new[] { third, second, first }, Caps);

        Assert.Equal(forward, reversed);
        Assert.True(forward.CombatPowerInactive > 0f, "the third source must not fit the budget");
    }

    [Fact]
    public void Inactive_power_is_the_capped_remainder()
    {
        var sources = new[]
        {
            Source(BonusSourceKind.HullVariant, 1, StatBonuses.None with { Damage = Caps.DamageBonusCap }),
            Source(BonusSourceKind.Plates, 2, StatBonuses.None with { HitPoints = Caps.HitPointBonusCap }),
            Source(BonusSourceKind.Buffs, 3, StatBonuses.None with { ArmorPoints = Caps.ArmorPointsCap }),
        };

        var sheet = ShipStatRules.Compute(Tier1.Loadout(), sources, Caps);
        var unbounded = ShipStatRules.Compute(Tier1.Loadout(), sources, Caps with { CombatPowerBudget = 1_000f });

        Assert.True(sheet.CombatPowerUsed <= Caps.CombatPowerBudget);
        Assert.True(sheet.CombatPowerInactive > 0f);
        Assert.Equal(0f, unbounded.CombatPowerInactive);
        Assert.Equal(unbounded.CombatPowerUsed, sheet.CombatPowerUsed + sheet.CombatPowerInactive, 3);
    }

    [Fact]
    public void Armor_points_respect_the_absolute_maximum()
    {
        var hull = Tier1.Hull with { ArmorFront = Caps.ArmorAbsoluteMax - (Caps.ArmorPointsCap / 200f) };
        var loadout = Tier1.Loadout() with { Hull = hull };
        var plating = StatBonuses.None with { ArmorPoints = Caps.ArmorPointsCap };
        var sources = new[] { Source(BonusSourceKind.Plates, 1, plating) };

        var sheet = ShipStatRules.Compute(loadout, sources, Caps);

        Assert.Equal(Caps.ArmorAbsoluteMax, sheet.ArmorFront, 3);
        Assert.Equal(hull.ArmorSides + (Caps.ArmorPointsCap / 100f), sheet.ArmorSides, 3);
        Assert.Equal(Caps.CombatPowerArmorWeight * Caps.ArmorPointsCap, sheet.CombatPowerUsed, 3);
    }

    [Fact]
    public void Extra_cannon_slots_cost_their_share_of_the_hull()
    {
        var slots = Tier1.Hull.CannonSlots;
        var extra = 2;
        var sources = new[] { Source(BonusSourceKind.Crew, 1, StatBonuses.None with { ExtraCannonSlots = extra }) };

        var sheet = ShipStatRules.Compute(Tier1.Loadout(), sources, Caps);

        Assert.Equal((uint)((slots + extra) * Tier1.Cannon.Damage), sheet.VolleyDamage);
        Assert.Equal(100f * extra / slots, sheet.CombatPowerUsed, 3);
    }

    [Fact]
    public void A_negative_source_contributes_nothing()
    {
        var positive = Source(BonusSourceKind.Plates, 1, StatBonuses.None with { Damage = Caps.DamageBonusCap });
        var negative = Source(
            BonusSourceKind.Buffs,
            2,
            StatBonuses.None with { Damage = -Caps.DamageBonusCap, Magazine = -Tier1.Hull.Magazine });

        var withNegative = ShipStatRules.Compute(Tier1.Loadout(), new[] { positive, negative }, Caps);
        var withoutNegative = ShipStatRules.Compute(Tier1.Loadout(), new[] { positive }, Caps);

        Assert.Equal(withoutNegative, withNegative);
        Assert.Equal((uint)(Tier1.Sheet().VolleyDamage * (1f + Caps.DamageBonusCap)), withNegative.VolleyDamage);
        Assert.Equal(Tier1.Hull.Magazine, withNegative.Magazine);
        Assert.Equal(Caps.DamageBonusCap * 100f, withNegative.CombatPowerUsed, 3);
    }

    [Fact]
    public void A_loadout_without_cannons_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new ShipLoadout(Tier1.Hull, Tier1.Cannon, 0, 1f, 1f));
    }

    [Fact]
    public void A_loadout_with_a_non_finite_ammo_multiplier_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new ShipLoadout(Tier1.Hull, Tier1.Cannon, 8, float.NaN, 1f));
    }

    [Fact]
    public void A_copy_that_removes_every_cannon_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = Tier1.Loadout() with { CannonCount = 0 });
    }

    [Fact]
    public void A_copy_with_a_non_finite_ammo_multiplier_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = Tier1.Loadout() with { AmmoDamageMultiplier = float.NaN });
    }

    [Fact]
    public void A_copy_onto_a_hull_without_cannon_slots_is_rejected()
    {
        var hull = Tier1.Hull with { CannonSlots = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = Tier1.Loadout() with { Hull = hull });
    }

    [Fact]
    public void A_copy_without_a_hull_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _ = Tier1.Loadout() with { Hull = null! });
    }

    [Fact]
    public void A_copy_without_a_cannon_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _ = Tier1.Loadout() with { Cannon = null! });
    }

    [Fact]
    public void A_copy_with_a_zero_ammo_multiplier_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = Tier1.Loadout() with { AmmoReloadMultiplier = 0f });
    }

    [Fact]
    public void Sources_that_overflow_a_bonus_total_are_rejected()
    {
        var slots = StatBonuses.None with { ExtraCannonSlots = int.MaxValue };
        var sources = new[]
        {
            Source(BonusSourceKind.Crew, 1, slots),
            Source(BonusSourceKind.Crew, 2, slots),
        };

        Assert.Throws<OverflowException>(() => ShipStatRules.Compute(Tier1.Loadout(), sources, Caps));
    }
}
