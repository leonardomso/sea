using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public readonly record struct GeneratedBonusSources(BonusSource[] Sources);

public static class ShipStatArbitraries
{
    /// <summary>Straddles the caps in both directions and slips in the occasional non-finite value.</summary>
    private static Gen<float> Ratio(float min, float max) =>
        from bucket in Gen.Choose(0, 99)
        from value in Gen.Choose(0, 1_000_000)
        select bucket switch
        {
            0 => float.NaN,
            1 => float.PositiveInfinity,
            2 => float.NegativeInfinity,
            _ => min + ((max - min) * (value / 1_000_000f)),
        };

    private static Gen<StatBonuses> Bonuses() =>
        from damage in Ratio(-0.05f, 0.30f)
        from reload in Ratio(-0.05f, 0.30f)
        from magazine in Gen.Choose(-1, 4)
        from hitPoints in Ratio(-0.05f, 0.30f)
        from armor in Ratio(-2f, 18f)
        from speed in Ratio(-0.05f, 0.30f)
        from turn in Ratio(-0.05f, 0.30f)
        from range in Gen.Choose(-1, 4)
        from repairAmount in Ratio(-0.1f, 0.6f)
        from repairChannel in Ratio(-0.1f, 0.6f)
        from slots in Gen.Choose(-1, 4)
        select new StatBonuses(
            damage, reload, magazine, hitPoints, armor, speed, turn, range, repairAmount, repairChannel, slots);

    private static Gen<BonusSource> Source() =>
        from kind in Gen.Elements(Enum.GetValues<BonusSourceKind>())
        from bonuses in Bonuses()
        select new BonusSource(kind, 0UL, bonuses);

    public static Arbitrary<GeneratedBonusSources> Sources() =>
        Arb.From(Source().ListOf().Select(list => new GeneratedBonusSources(
            list.Select((source, index) => source with { SourceId = (ulong)index }).ToArray())));
}

public sealed class ShipStatPropertyTests
{
    private static readonly StatCapsContent Caps = Tier1.Caps;

    /// <summary>A single source sitting on every cap: the best sheet the caps allow, budget aside.</summary>
    private static BonusSource MaxedSource() => new(
        BonusSourceKind.Buffs,
        ulong.MaxValue,
        new StatBonuses(
            Caps.DamageBonusCap,
            Caps.ReloadBonusCap,
            Caps.MagazineBonusCap,
            Caps.HitPointBonusCap,
            Caps.ArmorPointsCap,
            Caps.SpeedBonusCap,
            Caps.TurnBonusCap,
            Caps.RangeBonusCapSquares,
            Caps.RepairAmountBonusCap,
            Caps.RepairChannelBonusCap,
            Caps.CannonSlotBonusCap));

    private static StatCapsContent Unbounded() => Caps with { CombatPowerBudget = 1_000_000f };

    [Property(Arbitrary = new[] { typeof(ShipStatArbitraries) }, MaxTest = 300)]
    public void The_sheet_does_not_depend_on_the_order_sources_arrive_in(GeneratedBonusSources generated)
    {
        var forward = ShipStatRules.Compute(Tier1.Loadout(), generated.Sources, Caps);
        var reversed = ShipStatRules.Compute(Tier1.Loadout(), generated.Sources.Reverse().ToArray(), Caps);

        Assert.Equal(forward, reversed);
    }

    [Property(Arbitrary = new[] { typeof(ShipStatArbitraries) }, MaxTest = 300)]
    public void Every_stat_stays_between_the_bare_hull_and_the_fully_capped_hull(GeneratedBonusSources generated)
    {
        var floor = ShipStatRules.Compute(Tier1.Loadout(), Array.Empty<BonusSource>(), Caps);
        var ceiling = ShipStatRules.Compute(Tier1.Loadout(), new[] { MaxedSource() }, Unbounded());

        var sheet = ShipStatRules.Compute(Tier1.Loadout(), generated.Sources, Caps);

        Between(sheet.VolleyDamage, floor.VolleyDamage, ceiling.VolleyDamage);
        Between(sheet.ReloadMilliseconds, floor.ReloadMilliseconds, ceiling.ReloadMilliseconds);
        Between(sheet.Magazine, floor.Magazine, ceiling.Magazine);
        Between(sheet.MaxHitPoints, floor.MaxHitPoints, ceiling.MaxHitPoints);
        Between(sheet.ArmorFront, floor.ArmorFront, ceiling.ArmorFront);
        Between(sheet.ArmorSides, floor.ArmorSides, ceiling.ArmorSides);
        Between(sheet.ArmorBack, floor.ArmorBack, ceiling.ArmorBack);
        Between(sheet.SpeedSquaresPerSecond, floor.SpeedSquaresPerSecond, ceiling.SpeedSquaresPerSecond);
        Between(sheet.TurnDegreesPerSecond, floor.TurnDegreesPerSecond, ceiling.TurnDegreesPerSecond);
        Between(sheet.RangeSquares, floor.RangeSquares, ceiling.RangeSquares);
        Between(sheet.RepairAmount, floor.RepairAmount, ceiling.RepairAmount);
        Between(sheet.RepairChannelMilliseconds, floor.RepairChannelMilliseconds, ceiling.RepairChannelMilliseconds);
        Between(sheet.FightScore, floor.FightScore, ceiling.FightScore);
    }

    /// <summary>Cheap enough in Combat Power that it usually lands, so the comparison is not vacuous.</summary>
    private static BonusSource ModestSource() => new(
        BonusSourceKind.Buffs,
        ulong.MaxValue,
        StatBonuses.None with
        {
            Damage = Caps.DamageBonusCap / 4f,
            HitPoints = Caps.HitPointBonusCap / 4f,
            Magazine = Caps.MagazineBonusCap,
            Speed = Caps.SpeedBonusCap,
            Turn = Caps.TurnBonusCap,
            RangeSquares = Caps.RangeBonusCapSquares,
            RepairAmount = Caps.RepairAmountBonusCap,
            RepairChannel = Caps.RepairChannelBonusCap,
        });

    [Property(Arbitrary = new[] { typeof(ShipStatArbitraries) }, MaxTest = 300)]
    public void Appending_a_source_never_makes_the_ship_worse(GeneratedBonusSources generated)
    {
        // Under the real budget the appended source may be dropped; with the budget lifted it always lands.
        AssertAppendingHelps(generated.Sources, Caps);
        AssertAppendingHelps(generated.Sources, Unbounded());
    }

    private static void AssertAppendingHelps(BonusSource[] sources, StatCapsContent caps)
    {
        var before = ShipStatRules.Compute(Tier1.Loadout(), sources, caps);
        var after = ShipStatRules.Compute(Tier1.Loadout(), [.. sources, ModestSource()], caps);

        Assert.True(after.VolleyDamage >= before.VolleyDamage);
        Assert.True(after.ReloadMilliseconds <= before.ReloadMilliseconds);
        Assert.True(after.Magazine >= before.Magazine);
        Assert.True(after.MaxHitPoints >= before.MaxHitPoints);
        Assert.True(after.ArmorFront >= before.ArmorFront);
        Assert.True(after.ArmorSides >= before.ArmorSides);
        Assert.True(after.ArmorBack >= before.ArmorBack);
        Assert.True(after.SpeedSquaresPerSecond >= before.SpeedSquaresPerSecond);
        Assert.True(after.TurnDegreesPerSecond >= before.TurnDegreesPerSecond);
        Assert.True(after.RangeSquares >= before.RangeSquares);
        Assert.True(after.RepairAmount >= before.RepairAmount);
        Assert.True(after.RepairChannelMilliseconds <= before.RepairChannelMilliseconds);
        Assert.True(after.FightScore >= before.FightScore - 1e-4f);
    }

    [Property(Arbitrary = new[] { typeof(ShipStatArbitraries) }, MaxTest = 300)]
    public void Used_power_stays_inside_the_budget_and_inactive_power_is_the_rest(GeneratedBonusSources generated)
    {
        var sheet = ShipStatRules.Compute(Tier1.Loadout(), generated.Sources, Caps);
        var unbounded = ShipStatRules.Compute(Tier1.Loadout(), generated.Sources, Unbounded());

        Assert.InRange(sheet.CombatPowerUsed, 0f, Caps.CombatPowerBudget);
        Assert.True(sheet.CombatPowerInactive >= 0f);
        Assert.Equal(0f, unbounded.CombatPowerInactive);
        Assert.Equal(unbounded.CombatPowerUsed, sheet.CombatPowerUsed + sheet.CombatPowerInactive, 3);
    }

    private static void Between(float value, float first, float second)
    {
        Assert.True(float.IsFinite(value), $"expected a finite value, got {value}");
        Assert.InRange(value, MathF.Min(first, second) - 1e-3f, MathF.Max(first, second) + 1e-3f);
    }

    private static void Between(uint value, uint first, uint second) =>
        Assert.InRange(value, Math.Min(first, second), Math.Max(first, second));

    private static void Between(byte value, byte first, byte second) =>
        Assert.InRange(value, Math.Min(first, second), Math.Max(first, second));
}
