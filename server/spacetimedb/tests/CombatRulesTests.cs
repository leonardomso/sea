using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CombatRulesTests
{
    [Theory]
    [InlineData(0f, ArmorFace.Front)]
    [InlineData(44.99f, ArmorFace.Front)]
    [InlineData(45f, ArmorFace.Front)]
    [InlineData(45.01f, ArmorFace.Sides)]
    [InlineData(90f, ArmorFace.Sides)]
    [InlineData(134.99f, ArmorFace.Sides)]
    [InlineData(135f, ArmorFace.Back)]
    [InlineData(180f, ArmorFace.Back)]
    [InlineData(-45f, ArmorFace.Front)]
    [InlineData(-90f, ArmorFace.Sides)]
    [InlineData(-135f, ArmorFace.Back)]
    public void Armour_faces_split_at_the_exact_arc_edges(float bearingDegrees, ArmorFace expected)
    {
        // The shooter is placed on the compass the rest of the chart uses, so the bearing the
        // fixture means is the bearing ResolveFacing reads. It used to place her with an
        // unnegated cosine, which put every one of these cases on the opposite side of the hull.
        //
        // Not GeometryRules.Direction: that reads a table sampled every quarter degree, so it
        // cannot place a shooter at 45.01 -- she lands on 45 and the case testing the far side
        // of the edge tests the near side instead. ResolveFacing reads the bearing back through
        // MathF.Atan2, which is exact, so placing her with exact trigonometry is what puts her
        // where the fixture says. A boundary case has to be able to sit off the grid.
        var (sine, cosine) = MathF.SinCos(bearingDegrees * (MathF.PI / 180f));

        Assert.Equal(expected, CombatRules.ResolveFacing(
            targetHeadingDegrees: 0f,
            targetX: 0f,
            targetY: 0f,
            sourceX: sine * 10f,
            sourceY: (0f - cosine) * 10f));
    }

    [Theory]
    [InlineData(360f, ArmorFace.Front)]
    [InlineData(-360f, ArmorFace.Front)]
    [InlineData(180f, ArmorFace.Back)]
    [InlineData(90f, ArmorFace.Sides)]
    public void Armour_faces_follow_a_heading_wrapped_past_a_full_turn(
        float headingDegrees,
        ArmorFace expected)
    {
        // The shooter sits due north of the target, which is the smaller y on this chart;
        // only the target's heading moves.
        Assert.Equal(expected, CombatRules.ResolveFacing(
            headingDegrees,
            targetX: 0f,
            targetY: 0f,
            sourceX: 0f,
            sourceY: -10f));
    }

    [Fact]
    public void Armour_faces_are_measured_from_the_target_position()
    {
        Assert.Equal(ArmorFace.Front, CombatRules.ResolveFacing(
            targetHeadingDegrees: 0f,
            targetX: 10f,
            targetY: 10f,
            sourceX: 10f,
            sourceY: -10f));
    }

    [Theory]
    [InlineData(ArmorFace.Front, 0.4f)]
    [InlineData(ArmorFace.Sides, 0.2f)]
    [InlineData(ArmorFace.Back, 0.05f)]
    public void Armour_reads_the_face_the_volley_landed_on(ArmorFace face, float expected)
    {
        Assert.Equal(expected, CombatRules.ArmorOn(face, front: 0.4f, sides: 0.2f, back: 0.05f));
    }

    /// <summary>
    /// The one case the whole facing rule has to get right, and the one no test pinned:
    /// north is -Y on this chart, so a shooter due north of her target sits at the smaller
    /// Y. Every other case here was written against a source placed with an unnegated
    /// cosine, which put "north" due south and agreed with an equally inverted
    /// <c>ResolveFacing</c>. Two wrongs made a green suite.
    /// </summary>
    [Theory]
    [InlineData(0f, ArmorFace.Front)]
    [InlineData(90f, ArmorFace.Sides)]
    [InlineData(180f, ArmorFace.Back)]
    [InlineData(270f, ArmorFace.Sides)]
    public void A_shot_from_due_north_reads_off_the_face_the_target_turns_to_it(
        float targetHeadingDegrees,
        ArmorFace expected)
    {
        Assert.Equal(expected, CombatRules.ResolveFacing(
            targetHeadingDegrees,
            targetX: 200f,
            targetY: 200f,
            sourceX: 200f,
            sourceY: 190f));
    }

    /// <summary>
    /// Facing has to answer the same compass the rest of the simulation sails by, so the
    /// bearing it measures from is the one <see cref="GeometryRules.HeadingTo"/> gives.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(45f)]
    [InlineData(137f)]
    [InlineData(300f)]
    public void Facing_is_measured_from_the_same_bearing_the_chart_uses(float bearingDegrees)
    {
        var (offsetX, offsetY) = GeometryRules.Direction(bearingDegrees);
        var sourceX = 200f + offsetX * 10f;
        var sourceY = 200f + offsetY * 10f;

        Assert.Equal(
            bearingDegrees,
            GeometryRules.HeadingTo(200f, 200f, sourceX, sourceY, 0f),
            1);
        Assert.Equal(
            ArmorFace.Front,
            CombatRules.ResolveFacing(bearingDegrees, 200f, 200f, sourceX, sourceY));
    }

    [Fact]
    public void Distance_is_relative_to_the_source_position()
    {
        Assert.Equal(5f, CombatRules.Distance(3, 4, 6, 8));
    }

    [Theory]
    [InlineData(false, true, true, 1u, FireRejection.SourceSunk)]
    [InlineData(true, false, true, 1u, FireRejection.NoTarget)]
    [InlineData(true, true, false, 1u, FireRejection.TargetSunk)]
    [InlineData(true, true, true, 0u, FireRejection.Reloading)]
    [InlineData(true, true, true, 1u, FireRejection.None)]
    public void Fire_admission_rejects_an_unavailable_shot(
        bool sourceAlive,
        bool targetSelected,
        bool targetAlive,
        uint readyVolleys,
        FireRejection expected)
    {
        var request = ValidFireRequest() with
        {
            SourceAlive = sourceAlive,
            TargetSelected = targetSelected,
            TargetAlive = targetAlive,
            ReadyVolleys = readyVolleys,
        };

        Assert.Equal(expected, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_rejects_a_target_beyond_the_range_it_was_given()
    {
        var request = ValidFireRequest() with { TargetX = 0f, TargetY = 34f, RangeUnits = 33f };

        Assert.Equal(FireRejection.OutOfRange, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_accepts_a_target_exactly_on_the_range_ring()
    {
        var request = ValidFireRequest() with { TargetX = 0f, TargetY = 34f, RangeUnits = 34f };

        Assert.Equal(FireRejection.None, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_rejects_an_active_channel()
    {
        var request = ValidFireRequest() with { IsChanneling = true };

        Assert.Equal(FireRejection.Busy, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_rejects_a_ship_in_port()
    {
        var request = ValidFireRequest() with { InPort = true };

        Assert.Equal(FireRejection.InPort, CombatRules.ValidateFire(request));
    }

    [Theory]
    [InlineData(false, 0ul, 0ul, FireRejection.None)]
    [InlineData(true, 0ul, 9ul, FireRejection.FiringTooFast)]
    [InlineData(true, 0ul, 10ul, FireRejection.None)]
    [InlineData(true, 100ul, 109ul, FireRejection.FiringTooFast)]
    public void The_fire_interval_is_a_floor_a_full_magazine_cannot_beat(
        bool hasFired,
        ulong lastShotTick,
        ulong currentTick,
        FireRejection expected)
    {
        var request = ValidFireRequest() with
        {
            ReadyVolleys = 4,
            HasFired = hasFired,
            LastShotTick = lastShotTick,
            CurrentTick = currentTick,
        };

        Assert.Equal(expected, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void An_empty_magazine_is_refused_before_the_range_check()
    {
        var request = ValidFireRequest() with
        {
            ReadyVolleys = 0,
            TargetX = 0f,
            TargetY = 1000f,
        };

        Assert.Equal(FireRejection.Reloading, CombatRules.ValidateFire(request));
    }

    [Theory]
    [InlineData(100u, 1f, 0f, 100u)]
    [InlineData(100u, 1f, 0.25f, 75u)]
    [InlineData(100u, 0.7f, 0.2f, 56u)]
    [InlineData(100u, 0.6f, 0.35f, 39u)]
    [InlineData(0u, 1f, 0f, 0u)]
    [InlineData(100u, 1f, 1f, 0u)]
    public void Damage_is_the_floor_of_volley_times_ammunition_times_the_open_face(
        uint volleyDamage,
        float ammoMultiplier,
        float armor,
        uint expected)
    {
        Assert.Equal(expected, CombatRules.ResolveDamage(volleyDamage, ammoMultiplier, armor));
    }

    [Theory]
    [InlineData(-0.5f, 50u)]
    [InlineData(float.NaN, 50u)]
    [InlineData(float.NegativeInfinity, 50u)]
    [InlineData(1.5f, 0u)]
    [InlineData(float.PositiveInfinity, 50u)]
    public void Armour_outside_the_unit_range_is_clamped_rather_than_trusted(
        float armor,
        uint expected)
    {
        // A hand-written NPC row is the only way to reach this; the derived sheet is already
        // capped. Anything unreal reads as no armour at all, and a finite value past 1 clamps to
        // total absorption; neither can ever hand the target hull back.
        Assert.Equal(expected, CombatRules.ResolveDamage(50, 1f, armor));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    public void A_negative_or_unreal_ammunition_multiplier_is_rejected(float multiplier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatRules.ResolveDamage(50, multiplier, 0f));
    }

    [Fact]
    public void Spending_a_volley_restarts_the_reload_behind_it()
    {
        var spent = CombatRules.Spend(new MagazineState(3, 7));

        Assert.Equal(new MagazineState(2, 0), spent);
    }

    [Fact]
    public void An_empty_magazine_has_no_volley_to_spend()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatRules.Spend(new MagazineState(0, 0)));
    }

    [Theory]
    [InlineData(0u, 0u, 0u, 1u)]
    [InlineData(0u, 1u, 0u, 2u)]
    [InlineData(0u, 2u, 1u, 0u)]
    public void Reload_progress_carries_a_volley_back_into_the_magazine(
        uint ready,
        uint progress,
        uint expectedReady,
        uint expectedProgress)
    {
        var advanced = CombatRules.Advance(
            new MagazineState(ready, progress),
            magazineSize: 4,
            reloadTicks: 3,
            ticksSinceCombat: 0);

        Assert.Equal(new MagazineState(expectedReady, expectedProgress), advanced);
    }

    [Fact]
    public void A_full_magazine_holds_its_progress_at_zero()
    {
        var advanced = CombatRules.Advance(
            new MagazineState(4, 2),
            magazineSize: 4,
            reloadTicks: 3,
            ticksSinceCombat: 0);

        Assert.Equal(new MagazineState(4, 0), advanced);
    }

    [Theory]
    [InlineData(CombatRules.IdleRefillTicks - 1, 1u)]
    [InlineData(CombatRules.IdleRefillTicks, 4u)]
    public void Fifteen_quiet_seconds_refill_the_magazine_outright(
        ulong ticksSinceCombat,
        uint expectedReady)
    {
        var advanced = CombatRules.Advance(
            new MagazineState(1, 1),
            magazineSize: 4,
            reloadTicks: 30,
            ticksSinceCombat);

        Assert.Equal(expectedReady, advanced.ReadyVolleys);
    }

    [Theory]
    [InlineData(0u, 3u)]
    [InlineData(3u, 0u)]
    public void A_magazine_or_reload_of_zero_is_rejected(uint magazineSize, uint reloadTicks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatRules.Advance(
            new MagazineState(0, 0),
            magazineSize,
            reloadTicks,
            ticksSinceCombat: 0));
    }

    [Theory]
    [InlineData(0u, 1u)]
    [InlineData(1u, 1u)]
    [InlineData(100u, 1u)]
    [InlineData(101u, 2u)]
    [InlineData(2000u, 20u)]
    [InlineData(2050u, 21u)]
    public void Reload_milliseconds_round_up_to_whole_ticks(uint milliseconds, uint expected)
    {
        Assert.Equal(expected, CombatRules.ReloadTicks(milliseconds));
    }

    [Fact]
    public void Every_shipped_ammunition_leaves_a_round_shot_at_or_above_its_damage()
    {
        var catalog = ContentCatalog.CreateDefault();
        var round = catalog.Ammunition
            .Single(item => string.Equals(item.Id, "round", StringComparison.Ordinal));
        var baseline = CombatRules.ResolveDamage(100, round.DamageMultiplier, 0f);

        foreach (var ammunition in catalog.Ammunition)
        {
            Assert.True(
                CombatRules.ResolveDamage(100, ammunition.DamageMultiplier, 0f) <= baseline,
                $"{ammunition.Id} out-damages round shot without giving anything up.");
        }
    }

    private static FireRequest ValidFireRequest() => new()
    {
        SourceAlive = true,
        TargetSelected = true,
        TargetAlive = true,
        InPort = false,
        IsChanneling = false,
        ReadyVolleys = 1,
        CurrentTick = 20,
        HasFired = false,
        LastShotTick = 0,
        SourceX = 0f,
        SourceY = 0f,
        TargetX = -10f,
        TargetY = 0f,
        RangeUnits = 60f,
    };
}
