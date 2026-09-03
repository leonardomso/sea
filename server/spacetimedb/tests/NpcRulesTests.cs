using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed partial class NpcRulesTests
{
    [Theory]
    [InlineData(ShipArchetypeCode.Raider, AmmunitionCode.Chain)]
    [InlineData(ShipArchetypeCode.Gunship, AmmunitionCode.Incendiary)]
    public void Hostile_archetypes_acquire_players_with_their_preferred_shot(
        ShipArchetypeCode archetype,
        AmmunitionCode ammunition)
    {
        var decision = NpcRules.Decide(Snapshot(archetype) with
        {
            CandidateTargetId = 42,
        });

        Assert.Equal(NpcActionKind.SelectTarget, decision.Action);
        Assert.Equal(42ul, decision.TargetEntityId);
        Assert.Equal(ammunition, decision.Ammunition);
    }

    [Fact]
    public void Raider_closes_range_and_gunship_retreats_when_too_close()
    {
        var raider = NpcRules.Decide(Snapshot(ShipArchetypeCode.Raider) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 40f,
            TargetX = 40f,
        });
        var gunship = NpcRules.Decide(Snapshot(ShipArchetypeCode.Gunship) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 12f,
            TargetX = 12f,
        });

        Assert.Equal(NpcActionKind.SetCourse, raider.Action);
        Assert.Equal(22f, raider.DestinationX, 3);
        Assert.Equal(NpcActionKind.SetCourse, gunship.Action);
        Assert.Equal(-36f, gunship.DestinationX, 3);
    }

    [Theory]
    [InlineData(22f, NpcActionKind.Hold)]
    [InlineData(10f, NpcActionKind.SetCourse)]
    public void Npc_keeps_a_course_that_already_ends_at_its_holding_point(
        float courseX,
        NpcActionKind expected)
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Raider) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 40f,
            TargetX = 40f,
            HasCourse = true,
            CourseX = courseX,
        });

        Assert.Equal(expected, decision.Action);
    }

    [Fact]
    public void Npc_at_range_with_an_empty_magazine_holds_its_station()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 45f,
            TargetX = 45f,
            HasCourse = true,
            CanFire = false,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    [Fact]
    public void Damaged_npc_repairs_before_taking_an_offensive_action()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Gunship) with
        {
            Hull = 20,
            HasRepairKit = true,
            TargetEntityId = 42,
            TargetAvailable = true,
        });

        Assert.Equal(NpcActionKind.StartRepair, decision.Action);
    }

    [Theory]
    [InlineData(30u, 100u, true)]
    [InlineData(31u, 100u, false)]
    [InlineData(0u, 0u, false)]
    public void Repair_inventory_is_queried_only_at_the_repair_threshold(
        uint hull,
        uint maximumHull,
        bool expected)
    {
        Assert.Equal(expected, NpcRules.ShouldAttemptRepair(hull, maximumHull));
    }

    [Fact]
    public void Fire_decision_is_deterministic_for_the_same_snapshot()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Gunship) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 48f,
            TargetX = -48f,
            HeadingDegrees = 0f,
            SelectedAmmunition = AmmunitionCode.Incendiary,
            CanFire = true,
        };

        Assert.Equal(NpcRules.Decide(snapshot), NpcRules.Decide(snapshot));
        Assert.Equal(NpcActionKind.Fire, NpcRules.Decide(snapshot).Action);
    }

    [Theory]
    [InlineData(false, ShipMode.Operational)]
    [InlineData(true, ShipMode.Sunk)]
    [InlineData(true, ShipMode.Repairing)]
    public void InactiveOrNonOperationalNpcHolds(bool active, ShipMode mode)
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Raider) with
        {
            Active = active,
            Mode = mode,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    [Fact]
    public void LostTargetIsClearedBeforeAnotherAction()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Gunship) with
        {
            TargetEntityId = 42,
            TargetAvailable = false,
        });

        Assert.Equal(NpcActionKind.ClearTarget, decision.Action);
    }

    [Theory]
    [InlineData(false, 65f, true)]
    [InlineData(true, 65f, false)]
    [InlineData(false, 0f, false)]
    public void NpcSearchesOnlyWhenItNeedsAnAggroTarget(
        bool targetAvailable,
        float aggroRange,
        bool expected)
    {
        Assert.Equal(expected, NpcRules.ShouldSearchForTarget(targetAvailable, aggroRange, 0f));
    }

    [Fact]
    public void NpcSelectsItsLoadoutBeforeFiring()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Gunship) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 48,
            TargetX = -48,
            SelectedAmmunition = AmmunitionCode.Round,
        });

        Assert.Equal(NpcActionKind.SetAmmo, decision.Action);
        Assert.Equal(AmmunitionCode.Incendiary, decision.Ammunition);
    }

    [Fact]
    public void AShipAtRangeFiresWhenLoadedAndOtherwiseHolds()
    {
        var starboard = Snapshot(ShipArchetypeCode.Patrol) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 45,
            TargetX = 45,
            SelectedAmmunition = AmmunitionCode.Round,
            CanFire = true,
        };

        Assert.Equal(NpcActionKind.Fire, NpcRules.Decide(starboard).Action);
        Assert.Equal(NpcActionKind.Hold,
            NpcRules.Decide(starboard with { CanFire = false }).Action);
    }

    [Fact]
    public void Hold_range_point_inside_an_island_is_steered_to_open_water()
    {
        // Raider at the origin, target 40 east, island squarely on the 22-unit hold point.
        var island = new NavigationBlocker(22f, 0f, 10f);
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Raider) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 40f,
            TargetX = 40f,
            Blockers = [island],
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.False(NavigationRules.IsDestinationBlocked(
            decision.DestinationX,
            decision.DestinationY,
            [island]));
    }

    [Fact]
    public void Ship_dragged_past_the_leash_lets_its_target_go()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Raider) with
        {
            X = NpcRules.LeashRadius + 1f,
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 10f,
            TargetX = NpcRules.LeashRadius + 11f,
        });

        Assert.Equal(NpcActionKind.ClearTarget, decision.Action);
    }

    [Fact]
    public void Ship_outside_home_waters_sails_home_instead_of_hunting()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Raider) with
        {
            X = NpcRules.LeashRadius + 1f,
            HasCourse = true,
            CourseX = NpcRules.LeashRadius + 20f,
        };

        var decision = NpcRules.Decide(snapshot);

        Assert.False(NpcRules.ShouldSearchForTarget(false, 40f, NpcRules.RoamRadius + 1f));
        Assert.True(NpcRules.ShouldSearchForTarget(false, 40f, NpcRules.RoamRadius));
        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.InRange(
            CombatRules.Distance(snapshot.HomeX, snapshot.HomeY, decision.DestinationX, decision.DestinationY),
            0f,
            NpcRules.RoamRadius);
    }

    private static NpcSnapshot Snapshot(ShipArchetypeCode archetype) => new()
    {
        Archetype = archetype,
        Active = true,
        Mode = ShipMode.Operational,
        X = 0f,
        Y = 0f,
        Hull = 100,
        MaximumHull = 100,
        DesiredRange = archetype == ShipArchetypeCode.Raider ? 18f : 48f,
        SelectedAmmunition = AmmunitionCode.Round,
        PreferredAmmunition = archetype switch
        {
            ShipArchetypeCode.Raider => AmmunitionCode.Chain,
            ShipArchetypeCode.Gunship => AmmunitionCode.Incendiary,
            _ => AmmunitionCode.Round,
        },
        CanFire = true,
        DecisionSeed = 99,
        DecisionTick = 500,
    };

    [Theory]
    [InlineData(100UL, 50UL, 80f, true)]
    [InlineData(50UL, 50UL, 80f, false)]
    [InlineData(0UL, 50UL, 30f, true)]
    [InlineData(0UL, 50UL, 30.5f, false)]
    public void Shielded_or_harbored_players_are_never_npc_targets(
        ulong invulnerableUntilTick,
        ulong tick,
        float distanceFromHarbor,
        bool expected)
    {
        Assert.Equal(
            expected,
            NpcRules.IsProtectedFromNpcs(invulnerableUntilTick, tick, distanceFromHarbor));
    }
}
