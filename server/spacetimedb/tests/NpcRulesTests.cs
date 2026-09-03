using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class NpcRulesTests
{
    [Fact]
    public void Neutral_patrol_roams_until_it_has_been_attacked()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            CandidateTargetId = 42,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.Equal(0ul, decision.TargetEntityId);
    }

    [Theory]
    [InlineData(ShipArchetypeCode.Raider, AmmunitionCode.Chain, WeakPointCode.Sails)]
    [InlineData(ShipArchetypeCode.Gunship, AmmunitionCode.Incendiary, WeakPointCode.Hull)]
    public void Hostile_archetypes_acquire_players_with_their_tactical_loadout(
        ShipArchetypeCode archetype,
        AmmunitionCode ammunition,
        WeakPointCode weakPoint)
    {
        var decision = NpcRules.Decide(Snapshot(archetype) with
        {
            CandidateTargetId = 42,
        });

        Assert.Equal(NpcActionKind.SelectTarget, decision.Action);
        Assert.Equal(42ul, decision.TargetEntityId);
        Assert.Equal(ammunition, decision.Ammunition);
        Assert.Equal(weakPoint, decision.WeakPoint);
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
    public void Npc_finishes_its_turn_before_plotting_another_broadside_turn()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 45f,
            TargetX = 45f,
            HasCourse = true,
            PortReady = false,
            StarboardReady = false,
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
    public void Broadside_decision_is_deterministic_for_the_same_snapshot()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Gunship) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 48f,
            TargetX = -48f,
            HeadingDegrees = 0f,
            SelectedAmmunition = AmmunitionCode.Incendiary,
            PortReady = true,
            StarboardReady = true,
        };

        Assert.Equal(NpcRules.Decide(snapshot), NpcRules.Decide(snapshot));
        Assert.Equal(NpcActionKind.FirePort, NpcRules.Decide(snapshot).Action);
    }

    [Fact]
    public void Fixed_seed_roaming_replays_and_stays_inside_the_chart()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { HomeX = 30f, HomeY = -20f };

        var first = NpcRules.RoamDestination(snapshot);
        var replay = NpcRules.RoamDestination(snapshot);

        Assert.Equal(first, replay);
        Assert.InRange(first.X, WorldRules.MapMin, WorldRules.MapMax);
        Assert.InRange(first.Y, WorldRules.MapMin, WorldRules.MapMax);
    }

    [Theory]
    [InlineData(0f, 0f, 500UL)]
    [InlineData(30f, -20f, 505UL)]
    [InlineData(-60f, 70f, 510UL)]
    public void Roaming_patrols_the_waters_around_home_with_a_real_leg(
        float homeX,
        float homeY,
        ulong tick)
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = homeX,
            Y = homeY,
            HomeX = homeX,
            HomeY = homeY,
            DecisionTick = tick,
        };

        var destination = NpcRules.RoamDestination(snapshot);

        Assert.InRange(
            CombatRules.Distance(homeX, homeY, destination.X, destination.Y),
            NpcRules.MinimumRoamLeg,
            NpcRules.RoamRadius);
    }

    [Fact]
    public void Roaming_near_the_chart_edge_stays_on_the_chart()
    {
        for (var tick = 0UL; tick < 200; tick += NpcRules.DecisionIntervalTicks)
        {
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                X = WorldRules.MapMax,
                Y = WorldRules.MapMin,
                HomeX = WorldRules.MapMax,
                HomeY = WorldRules.MapMin,
                DecisionTick = tick,
            });

            Assert.InRange(destination.X, WorldRules.MapMin, WorldRules.MapMax);
            Assert.InRange(destination.Y, WorldRules.MapMin, WorldRules.MapMax);
        }
    }

    [Fact]
    public void Idle_npc_sets_course_for_a_roam_waypoint()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with { HomeX = 30f, HomeY = -20f };

        var decision = NpcRules.Decide(snapshot);
        var waypoint = NpcRules.RoamDestination(snapshot);

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
        Assert.Equal(waypoint.X, decision.DestinationX);
        Assert.Equal(waypoint.Y, decision.DestinationY);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(8, false)]
    public void Automatic_aggro_limits_hostile_fan_in(
        int currentAttackers,
        bool expected)
    {
        Assert.Equal(expected, NpcRules.HasAutomaticAggroCapacity(currentAttackers));
    }

    [Theory]
    [InlineData(false, ShipMode.Operational)]
    [InlineData(true, ShipMode.Sunk)]
    [InlineData(true, ShipMode.Repairing)]
    [InlineData(true, ShipMode.Boarding)]
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
    public void RoamingNpcKeepsItsExistingCourse()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            HasCourse = true,
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
    public void StarboardArcFiresAndUnavailableArcsTurn()
    {
        var starboard = Snapshot(ShipArchetypeCode.Patrol) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 45,
            TargetX = 45,
            SelectedAmmunition = AmmunitionCode.Round,
            PortReady = false,
            StarboardReady = true,
        };

        Assert.Equal(NpcActionKind.FireStarboard, NpcRules.Decide(starboard).Action);
        Assert.Equal(NpcActionKind.SetCourse,
            NpcRules.Decide(starboard with { StarboardReady = false }).Action);
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
    public void Roaming_never_plots_a_leg_into_an_island()
    {
        var island = new NavigationBlocker(20f, 0f, 30f);
        for (var tick = 0UL; tick < 400; tick += NpcRules.DecisionIntervalTicks)
        {
            var destination = NpcRules.RoamDestination(Snapshot(ShipArchetypeCode.Patrol) with
            {
                DecisionTick = tick,
                Blockers = [island],
            });

            Assert.False(NavigationRules.IsDestinationBlocked(destination.X, destination.Y, [island]));
        }
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
        PreferredWeakPoint = archetype == ShipArchetypeCode.Raider
            ? WeakPointCode.Sails
            : WeakPointCode.Hull,
        PortReady = true,
        StarboardReady = true,
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

    [Fact]
    public void Hostile_homes_sit_beyond_a_full_roam_leg_from_the_harbor_waters()
    {
        Assert.Equal(
            NpcRules.RoamRadius + WorldRules.HarborSafeRadius,
            NpcRules.HostileHomeClearance);
    }


    [Theory]
    [InlineData(98UL, 60f)]
    [InlineData(99UL, -60f)]
    public void Roaming_swings_the_next_leg_on_around_home_in_one_fixed_direction(
        ulong seed,
        float expectedStepDegrees)
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = 20f,
            Y = 0f,
            HomeX = 0f,
            HomeY = 0f,
            DecisionSeed = seed,
        };

        var destination = NpcRules.RoamDestination(snapshot);
        var bearing = MathF.Atan2(destination.Y, destination.X) * 180f / MathF.PI;

        Assert.Equal(expectedStepDegrees, bearing, 0.5f);
        Assert.InRange(
            CombatRules.Distance(0f, 0f, destination.X, destination.Y),
            NpcRules.MinimumRoamLeg,
            NpcRules.RoamRadius);
    }

    [Fact]
    public void Roaming_closes_a_full_loop_around_home()
    {
        var snapshot = Snapshot(ShipArchetypeCode.Patrol) with
        {
            X = 25f,
            Y = 0f,
            HomeX = 0f,
            HomeY = 0f,
            DecisionSeed = 98,
        };
        var bearings = new List<float>();
        for (var leg = 0; leg < 6; leg++)
        {
            var destination = NpcRules.RoamDestination(snapshot with
            {
                DecisionTick = snapshot.DecisionTick + (ulong)leg * NpcRules.DecisionIntervalTicks,
            });
            bearings.Add((MathF.Atan2(destination.Y, destination.X) * 180f / MathF.PI + 360f) % 360f);
            snapshot = snapshot with { X = destination.X, Y = destination.Y };
        }

        Assert.Equal([60f, 120f, 180f, 240f, 300f, 0f], bearings.Select(bearing => MathF.Round(bearing)));
    }
}
