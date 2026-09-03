using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class CombatRulesTests
{
    [Fact]
    public void Combat_damage_total_widens_without_overflow()
    {
        var damage = new CombatDamage(uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);

        Assert.Equal(4UL * uint.MaxValue, damage.Total);
        Assert.Equal(0ul, default(CombatDamage).Total);
    }

    [Theory]
    [InlineData(BroadsideSide.Port, -140f, true)]
    [InlineData(BroadsideSide.Port, -40f, true)]
    [InlineData(BroadsideSide.Port, -39.99f, false)]
    [InlineData(BroadsideSide.Starboard, 40f, true)]
    [InlineData(BroadsideSide.Starboard, 140f, true)]
    [InlineData(BroadsideSide.Starboard, 140.01f, false)]
    public void Broadside_arcs_include_the_exact_fifty_degree_edges(
        BroadsideSide side,
        float targetBearingDegrees,
        bool expected)
    {
        Assert.Equal(expected, CombatRules.IsInsideBroadsideArc(
            sourceX: 0f,
            sourceY: 0f,
            headingDegrees: 0f,
            targetX: MathF.Sin(targetBearingDegrees * MathF.PI / 180f) * 10f,
            targetY: MathF.Cos(targetBearingDegrees * MathF.PI / 180f) * 10f,
            side));
    }

    [Theory]
    [InlineData(300f, BroadsideSide.Starboard, 30f, true)]
    [InlineData(300f, BroadsideSide.Starboard, -30f, false)]
    [InlineData(300f, BroadsideSide.Port, -120f, true)]
    [InlineData(-300f, BroadsideSide.Port, -60f, true)]
    public void Broadside_arcs_wrap_headings_past_a_full_turn(
        float headingDegrees,
        BroadsideSide side,
        float targetBearingDegrees,
        bool expected)
    {
        Assert.Equal(expected, CombatRules.IsInsideBroadsideArc(
            sourceX: 0f,
            sourceY: 0f,
            headingDegrees,
            targetX: MathF.Sin(targetBearingDegrees * MathF.PI / 180f) * 10f,
            targetY: MathF.Cos(targetBearingDegrees * MathF.PI / 180f) * 10f,
            side));
    }

    [Theory]
    [InlineData(10f, 20f, false)]
    [InlineData(20f, 10f, true)]
    public void Broadside_arcs_are_relative_to_the_source_position(float targetX, float targetY, bool expected)
    {
        Assert.Equal(expected, CombatRules.IsInsideBroadsideArc(
            sourceX: 10f,
            sourceY: 10f,
            headingDegrees: 0f,
            targetX,
            targetY,
            BroadsideSide.Starboard));
    }

    [Fact]
    public void Distance_is_relative_to_the_source_position()
    {
        Assert.Equal(5f, CombatRules.Distance(3, 4, 6, 8));
    }

    [Theory]
    [InlineData(false, true, 100u, 10u, 20ul, 10ul, FireRejection.SourceSunk)]
    [InlineData(true, false, 100u, 10u, 20ul, 10ul, FireRejection.TargetSunk)]
    [InlineData(true, true, 0u, 10u, 20ul, 10ul, FireRejection.CannonsDisabled)]
    [InlineData(true, true, 100u, 0u, 20ul, 10ul, FireRejection.NoAmmunition)]
    [InlineData(true, true, 100u, 10u, 19ul, 20ul, FireRejection.Reloading)]
    [InlineData(true, true, 100u, 10u, 20ul, 20ul, FireRejection.None)]
    public void Fire_admission_rejects_unavailable_combat_resources(
        bool sourceAlive,
        bool targetAlive,
        uint cannons,
        uint ammunition,
        ulong currentTick,
        ulong readyAtTick,
        FireRejection expected)
    {
        var request = ValidFireRequest() with
        {
            SourceAlive = sourceAlive,
            TargetAlive = targetAlive,
            Cannons = cannons,
            Ammunition = ammunition,
            CurrentTick = currentTick,
            ReadyAtTick = readyAtTick,
        };

        Assert.Equal(expected, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_uses_the_selected_ammunition_range()
    {
        var request = ValidFireRequest() with
        {
            TargetX = 0f,
            TargetY = 34f,
            MaximumRange = 60f,
            RangeMultiplier = 0.55f,
        };

        Assert.Equal(FireRejection.OutOfRange, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_rejects_the_wrong_side()
    {
        var request = ValidFireRequest() with
        {
            Side = BroadsideSide.Port,
            TargetX = 10f,
            TargetY = 0f,
        };

        Assert.Equal(FireRejection.OutsideArc, CombatRules.ValidateFire(request));
    }

    [Fact]
    public void Fire_admission_rejects_repair_and_boarding_channels()
    {
        var request = ValidFireRequest() with { IsChanneling = true };

        Assert.Equal(FireRejection.Busy, CombatRules.ValidateFire(request));
    }

    [Theory]
    [InlineData(0f, 1ul)]
    [InlineData(4f, 1ul)]
    [InlineData(60f, 15ul)]
    public void Volley_travel_ticks_are_fixed_from_launch_distance(float distance, ulong expected)
    {
        Assert.Equal(expected, CombatRules.VolleyTravelTicks(
            distance,
            projectileSpeed: 40f,
            tickRateHz: 10));
    }

    [Theory]
    [InlineData(9ul, true, VolleyResolution.Waiting)]
    [InlineData(10ul, true, VolleyResolution.Impact)]
    [InlineData(15ul, true, VolleyResolution.Impact)]
    [InlineData(10ul, false, VolleyResolution.Harmless)]
    public void Fired_volleys_only_care_about_arrival_tick_and_target_survival(
        ulong currentTick,
        bool targetAlive,
        VolleyResolution expected)
    {
        Assert.Equal(expected, CombatRules.ResolveVolley(
            impactAtTick: 10,
            currentTick,
            targetAlive));
    }

    [Fact]
    public void Weak_point_aim_amplifies_that_subsystem_without_changing_ammunition_identity()
    {
        var chain = ContentCatalog.CreateDefault().Ammunition.Single(item =>
            string.Equals(item.Id, "chain", StringComparison.Ordinal));

        var damage = CombatRules.DamageProfile(
            chain,
            WeakPoint.Sails,
            cannonPower: WorldRules.InitialCannonDamage,
            cannons: 100,
            maxCannons: 100);

        Assert.Equal(5u, damage.Hull);
        Assert.Equal(35u, damage.Sails);
        Assert.Equal(2u, damage.Cannons);
        Assert.Equal(2u, damage.Crew);
    }

    [Theory]
    [InlineData("hull", WeakPoint.Hull)]
    [InlineData("SAILS", WeakPoint.Sails)]
    [InlineData("Cannons", WeakPoint.Cannons)]
    public void Weak_points_parse_case_insensitively(string value, WeakPoint expected)
    {
        Assert.True(CombatRules.TryParseWeakPoint(value, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void Unsupported_weak_points_are_rejected()
    {
        Assert.False(CombatRules.TryParseWeakPoint("crew", out _));
    }

    [Fact]
    public void MissingSelectedTargetIsRejectedBeforeTargetState()
    {
        var request = ValidFireRequest() with { TargetSelected = false };

        Assert.Equal(FireRejection.NoTarget, CombatRules.ValidateFire(request));
    }

    [Theory]
    [InlineData(-1f, 40f, 10u)]
    [InlineData(float.NaN, 40f, 10u)]
    [InlineData(1f, 0f, 10u)]
    [InlineData(1f, float.PositiveInfinity, 10u)]
    [InlineData(1f, 1f, 0u)]
    public void InvalidVolleyTimingInputsAreRejected(
        float distance,
        float projectileSpeed,
        uint tickRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatRules.VolleyTravelTicks(distance, projectileSpeed, tickRate));
    }

    [Fact]
    public void DamageProfileRejectsMissingContentAndZeroMaximumCannons()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CombatRules.DamageProfile(null!, WeakPoint.Hull, 25, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatRules.DamageProfile(
                ContentCatalog.CreateDefault().Ammunition[0],
                WeakPoint.Hull,
                25,
                1,
                0));
    }

    [Fact]
    public void ZeroCannonEffectivenessProducesNoDamage()
    {
        var damage = CombatRules.DamageProfile(
            ContentCatalog.CreateDefault().Ammunition[0],
            WeakPoint.Hull,
            cannonPower: 0,
            cannons: 100,
            maxCannons: 100);

        Assert.Equal(default, damage);
        Assert.Equal(5f, CombatRules.Distance(0, 0, 3, 4));
    }

    [Fact]
    public void Aiming_amplifies_only_the_chosen_subsystem()
    {
        var hullAim = RoundShotProfile(WeakPoint.Hull, WorldRules.InitialCannonDamage);
        var sailsAim = RoundShotProfile(WeakPoint.Sails, WorldRules.InitialCannonDamage);
        var cannonsAim = RoundShotProfile(WeakPoint.Cannons, WorldRules.InitialCannonDamage);

        Assert.True(hullAim.Hull > sailsAim.Hull);
        Assert.True(sailsAim.Sails > hullAim.Sails);
        Assert.True(cannonsAim.Cannons > hullAim.Cannons);
        Assert.Equal(sailsAim.Hull, cannonsAim.Hull);
        Assert.Equal(hullAim.Sails, cannonsAim.Sails);
        Assert.Equal(hullAim.Cannons, sailsAim.Cannons);
        Assert.Equal(hullAim.Crew, sailsAim.Crew);
        Assert.Equal(hullAim.Crew, cannonsAim.Crew);
    }

    [Fact]
    public void Damage_scales_linearly_with_cannon_effectiveness()
    {
        var single = RoundShotProfile(WeakPoint.Hull, WorldRules.InitialCannonDamage);
        var doubled = RoundShotProfile(WeakPoint.Hull, 2 * WorldRules.InitialCannonDamage);

        Assert.Equal(2u * single.Sails, doubled.Sails);
        Assert.Equal(2u * single.Crew, doubled.Crew);
    }

    [Fact]
    public void Damage_beyond_the_uint_range_is_rejected()
    {
        Assert.Throws<OverflowException>(() => CombatRules.DamageProfile(
            RoundShot(),
            WeakPoint.Hull,
            cannonPower: uint.MaxValue,
            cannons: uint.MaxValue,
            maxCannons: 1));
    }

    [Theory]
    [InlineData("round", WeakPoint.Hull, 31u)]
    [InlineData("round", WeakPoint.Sails, 6u)]
    [InlineData("round", WeakPoint.Cannons, 6u)]
    [InlineData("chain", WeakPoint.Hull, 6u)]
    [InlineData("chain", WeakPoint.Sails, 35u)]
    [InlineData("chain", WeakPoint.Cannons, 3u)]
    [InlineData("grapeshot", WeakPoint.Hull, 5u)]
    [InlineData("grapeshot", WeakPoint.Sails, 4u)]
    [InlineData("grapeshot", WeakPoint.Cannons, 5u)]
    [InlineData("incendiary", WeakPoint.Hull, 18u)]
    [InlineData("incendiary", WeakPoint.Sails, 10u)]
    [InlineData("incendiary", WeakPoint.Cannons, 10u)]
    public void Every_ammunition_and_weak_point_combination_has_deterministic_aimed_damage(
        string ammunitionId,
        WeakPoint weakPoint,
        uint expected)
    {
        var ammunition = ContentCatalog.CreateDefault().Ammunition
            .Single(item => string.Equals(item.Id, ammunitionId, StringComparison.Ordinal));

        var damage = CombatRules.DamageProfile(
            ammunition,
            weakPoint,
            WorldRules.InitialCannonDamage,
            cannons: 100,
            maxCannons: 100);

        var aimedDamage = weakPoint switch
        {
            WeakPoint.Hull => damage.Hull,
            WeakPoint.Sails => damage.Sails,
            WeakPoint.Cannons => damage.Cannons,
            _ => throw new ArgumentOutOfRangeException(nameof(weakPoint)),
        };
        Assert.Equal(expected, aimedDamage);
    }

    private static AmmunitionContent RoundShot() => ContentCatalog.CreateDefault().Ammunition
        .Single(item => string.Equals(item.Id, "round", StringComparison.Ordinal));

    private static CombatDamage RoundShotProfile(WeakPoint weakPoint, uint cannonPower) =>
        CombatRules.DamageProfile(RoundShot(), weakPoint, cannonPower, cannons: 100, maxCannons: 100);

    private static FireRequest ValidFireRequest() => new()
    {
        SourceAlive = true,
        TargetSelected = true,
        TargetAlive = true,
        Cannons = 100,
        Ammunition = 10,
        CurrentTick = 20,
        ReadyAtTick = 10,
        SourceX = 0f,
        SourceY = 0f,
        SourceHeadingDegrees = 0f,
        TargetX = -10f,
        TargetY = 0f,
        MaximumRange = 60f,
        RangeMultiplier = 1f,
        Side = BroadsideSide.Port,
    };
}
