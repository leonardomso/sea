using BenchmarkDotNet.Attributes;
using Sea.Server;

namespace Sea.Server.Benchmarks;

/// <summary>
/// Milestone 1a hot paths: the content catalog and the ship stat sheet. Every login, refit and volley
/// goes through one of these, so each is measured with the shipped tier-1 content rather than a fixture.
/// </summary>
public static class DomainBenchmarks
{
    private static readonly GameContent Content = ContentCatalog.CreateDefault();

    private static readonly HullContent Hull = Content.Hulls.Single(hull => hull.Tier == 1);

    private static readonly CannonContent Cannon = Content.Cannons.Single(cannon => cannon.Tier == 1);

    private static readonly AmmunitionContent Round =
        Content.Ammunition.Single(ammo => ammo.Code == AmmunitionCode.Round);

    [MemoryDiagnoser]
    public class ShipStatBenchmark
    {
        private readonly ShipLoadout loadout =
            new(Hull, Cannon, Hull.CannonSlots, Round.DamageMultiplier, Round.ReloadMultiplier);

        private readonly BonusSource[] none = [];

        // One source per kind, listed out of drop order so the sort inside Compute does real work.
        private readonly BonusSource[] fullKit =
        [
            new(BonusSourceKind.Buffs, 6, StatBonuses.None with { Damage = 0.10f, Reload = 0.05f }),
            new(BonusSourceKind.Skills, 5, StatBonuses.None with { HitPoints = 0.08f, ArmorPoints = 5f }),
            new(BonusSourceKind.Crew, 4, StatBonuses.None with { Reload = 0.10f, RepairAmount = 0.20f }),
            new(BonusSourceKind.Sails, 3, StatBonuses.None with { Speed = 0.15f, Turn = 0.10f }),
            new(BonusSourceKind.Plates, 2, StatBonuses.None with { ArmorPoints = 10f, HitPoints = 0.12f }),
            new(BonusSourceKind.HullVariant, 1, StatBonuses.None with { Magazine = 2, ExtraCannonSlots = 1 }),
        ];

        [Benchmark(Baseline = true)]
        public ShipStatSheet ComputeBareHull() => ShipStatRules.Compute(loadout, none, Content.StatCaps);

        [Benchmark]
        public ShipStatSheet ComputeFullKit() => ShipStatRules.Compute(loadout, fullKit, Content.StatCaps);
    }

    [MemoryDiagnoser]
    public class ContentValidationBenchmark
    {
        [Benchmark]
        public int ValidateDefaultCatalog() => ContentCatalog.Validate(Content).Count;
    }

    [MemoryDiagnoser]
    public class ContentIndexBenchmark
    {
        [Benchmark]
        public AmmunitionContent?[] AmmunitionByCode() => ContentIndex.AmmunitionByCode(Content);

        [Benchmark]
        public int HullsById() => ContentIndex.ById(Content.Hulls, hull => hull.Id, "hull").Count;
    }

    /// <summary>
    /// One volley, resolved the way the tick loop resolves it: the face the shot lands on, the
    /// armour that face carries, and the damage left after both. Every hit in the world runs this.
    /// </summary>
    [MemoryDiagnoser]
    public class VolleyBenchmark
    {
        private readonly ShipStatSheet sheet = ShipStatRules.Compute(
            new ShipLoadout(Hull, Cannon, Hull.CannonSlots, Round.DamageMultiplier, Round.ReloadMultiplier),
            [],
            Content.StatCaps);

        // Mutable fields, not literals: a shot written out in constants is folded away by the JIT
        // and the benchmark then measures nothing at all.
        private float sourceX = 35f;

        private float sourceY = 120f;

        private float targetHeading = 90f;

        [Benchmark(Baseline = true)]
        public ArmorFace ResolveFacing() =>
            CombatRules.ResolveFacing(sourceX, sourceY, targetHeading, 100f, 60f);

        [Benchmark]
        public uint ResolveVolley()
        {
            var face = CombatRules.ResolveFacing(sourceX, sourceY, targetHeading, 100f, 60f);
            var armor = CombatRules.ArmorOn(face, Hull.ArmorFront, Hull.ArmorSides, Hull.ArmorBack);
            return CombatRules.ResolveDamage(sheet.VolleyDamage, Round.DamageMultiplier, armor);
        }
    }

    /// <summary>
    /// The magazine is advanced once per ship per tick whether or not anyone is firing, so it is
    /// the most-run piece of combat code in the module.
    /// </summary>
    [MemoryDiagnoser]
    public class MagazineBenchmark
    {
        private static readonly uint ReloadTicks = CombatRules.ReloadTicks(3_000);

        private readonly MagazineState reloading = new(1, 4);

        private readonly MagazineState full = new(3, 0);

        [Benchmark(Baseline = true)]
        public MagazineState AdvanceWhileReloading() =>
            CombatRules.Advance(reloading, 3, ReloadTicks, 0);

        [Benchmark]
        public MagazineState AdvanceWhileFull() =>
            CombatRules.Advance(full, 3, ReloadTicks, 0);

        [Benchmark]
        public MagazineState SpendVolley() => CombatRules.Spend(full);
    }
}
