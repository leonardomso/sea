using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed partial class NpcRulesTests
{
    [Theory]
    [InlineData(AmmunitionCode.Chain)]
    [InlineData(AmmunitionCode.Incendiary)]
    public void Hostiles_acquire_players_with_their_preferred_shot(AmmunitionCode ammunition)
    {
        var decision = NpcRules.Decide(Snapshot(preferred: ammunition) with
        {
            CandidateTargetId = 42,
        });

        Assert.Equal(NpcActionKind.SelectTarget, decision.Action);
        Assert.Equal(42ul, decision.TargetEntityId);
        Assert.Equal(ammunition, decision.Ammunition);
    }

    [Fact]
    public void A_boarder_closes_the_range_and_a_gunner_opens_it()
    {
        var boarder = NpcRules.Decide(Snapshot(BoardingRange) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 40f,
            TargetX = 40f,
        });
        var gunner = NpcRules.Decide(Snapshot() with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 12f,
            TargetX = 12f,
        });

        Assert.Equal(NpcActionKind.SetCourse, boarder.Action);
        Assert.Equal(22f, boarder.DestinationX, 3);
        Assert.Equal(NpcActionKind.SetCourse, gunner.Action);
        Assert.Equal(-36f, gunner.DestinationX, 3);
    }

    [Theory]
    [InlineData(22f, NpcActionKind.Hold)]
    [InlineData(10f, NpcActionKind.SetCourse)]
    public void Npc_keeps_a_course_that_already_ends_at_its_holding_point(
        float courseX,
        NpcActionKind expected)
    {
        var decision = NpcRules.Decide(Snapshot(BoardingRange) with
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
        var decision = NpcRules.Decide(Snapshot() with
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
        var decision = NpcRules.Decide(Snapshot(preferred: AmmunitionCode.Incendiary) with
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
        var snapshot = Snapshot(preferred: AmmunitionCode.Incendiary) with
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
        var decision = NpcRules.Decide(Snapshot(BoardingRange) with
        {
            Active = active,
            Mode = mode,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    [Fact]
    public void LostTargetIsClearedBeforeAnotherAction()
    {
        var decision = NpcRules.Decide(Snapshot(preferred: AmmunitionCode.Incendiary) with
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
        Assert.Equal(expected, NpcRules.ShouldSearchForTarget(targetAvailable, aggroRange));
    }

    [Fact]
    public void NpcSelectsItsLoadoutBeforeFiring()
    {
        var decision = NpcRules.Decide(Snapshot(preferred: AmmunitionCode.Incendiary) with
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
        var starboard = Snapshot() with
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
        // A boarder at the origin, target 40 east, island squarely on the 22-unit hold point.
        var island = new NavigationBlocker(22f, 0f, 10f);
        var decision = NpcRules.Decide(Snapshot(BoardingRange) with
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
    public void Target_that_opens_the_disengage_range_is_let_go()
    {
        var chasing = Snapshot(BoardingRange) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = NpcRules.DisengageRange + 1f,
            TargetX = NpcRules.DisengageRange + 1f,
        };

        Assert.Equal(NpcActionKind.ClearTarget, NpcRules.Decide(chasing).Action);
        Assert.NotEqual(
            NpcActionKind.ClearTarget,
            NpcRules.Decide(chasing with
            {
                DistanceToTarget = NpcRules.DisengageRange,
                TargetX = NpcRules.DisengageRange,
            }).Action);
    }

    [Fact]
    public void A_ship_hunts_wherever_its_patrol_route_has_taken_it()
    {
        // The old rule only let a ship look for a target inside a bubble around its spawn,
        // which made every hostile far from home harmless scenery.
        Assert.True(NpcRules.ShouldSearchForTarget(false, 40f));
        Assert.False(NpcRules.ShouldSearchForTarget(true, 40f));
        Assert.False(NpcRules.ShouldSearchForTarget(false, 0f));
    }

    /// <summary>The range a hull that wants to be alongside holds.</summary>
    internal const float BoardingRange = 18f;

    /// <summary>The range a hull that would rather shoot holds.</summary>
    internal const float GunneryRange = 48f;

    // Nothing about a decision depends on which enemy it is any more: how close it wants to be
    // and what it loads are the whole of its character, so a snapshot is built from those.
    internal static NpcSnapshot Snapshot(
        float desiredRange = GunneryRange,
        AmmunitionCode preferred = AmmunitionCode.Round) => new()
        {
            Active = true,
            Mode = ShipMode.Operational,
            X = 0f,
            Y = 0f,
            Hull = 100,
            MaximumHull = 100,
            DesiredRange = desiredRange,
            SelectedAmmunition = AmmunitionCode.Round,
            PreferredAmmunition = preferred,
            CanFire = true,
            DecisionSeed = 99,
        };

    [Theory]
    [InlineData(true, 25u, 100u, true)]
    [InlineData(true, 26u, 100u, false)]
    [InlineData(false, 1u, 100u, false)]
    [InlineData(true, 0u, 0u, false)]
    public void Only_a_hull_that_runs_runs_and_only_at_a_quarter(
        bool fleesWhenCrippled,
        uint hull,
        uint maximumHull,
        bool expected)
    {
        Assert.Equal(expected, NpcRules.ShouldFlee(fleesWhenCrippled, hull, maximumHull));
    }

    [Fact]
    public void A_crippled_sea_dog_breaks_contact_before_it_patches_itself()
    {
        // It is at a quarter hull with a kit aboard, so both the flee rule and the repair rule
        // want this decision. Repairing under the guns that crippled it is how it dies.
        var decision = NpcRules.Decide(Snapshot(BoardingRange) with
        {
            Hull = 25,
            HasRepairKit = true,
            FleesWhenCrippled = true,
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 20f,
            TargetX = 20f,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.True(
            CombatRules.Distance(decision.DestinationX, decision.DestinationY, 20f, 0f) >
            NpcRules.DisengageRange);
    }

    [Theory]
    [InlineData(true, false, 50u, 100u, true)]
    [InlineData(true, false, 51u, 100u, false)]
    [InlineData(true, true, 10u, 100u, false)]
    [InlineData(false, false, 10u, 100u, false)]
    public void The_signal_goes_up_at_half_a_hull_and_only_once(
        bool callsForHelp,
        bool alreadyCalled,
        uint hull,
        uint maximumHull,
        bool expected)
    {
        Assert.Equal(
            expected,
            NpcRules.ShouldCallForHelp(callsForHelp, alreadyCalled, hull, maximumHull));
    }

    [Fact]
    public void An_escort_lies_at_its_mooring_until_its_captain_calls()
    {
        var moored = Snapshot(BoardingRange) with
        {
            AwaitingSignal = true,
            CandidateTargetId = 42,
        };

        Assert.Equal(NpcActionKind.Hold, NpcRules.Decide(moored).Action);
        Assert.Equal(
            NpcActionKind.SelectTarget,
            NpcRules.Decide(moored with { AwaitingSignal = false }).Action);
    }

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
