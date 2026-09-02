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
        Assert.True(raider.DestinationX > 0f);
        Assert.Equal(NpcActionKind.SetCourse, gunship.Action);
        Assert.True(gunship.DestinationX < 0f);
    }

    [Fact]
    public void Npc_stops_its_previous_course_after_entering_the_combat_range_band()
    {
        var decision = NpcRules.Decide(Snapshot(ShipArchetypeCode.Patrol) with
        {
            TargetEntityId = 42,
            TargetAvailable = true,
            DistanceToTarget = 45f,
            TargetX = 45f,
            HasCourse = true,
        });

        Assert.Equal(NpcActionKind.StopCourse, decision.Action);
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
        var first = NpcRules.RoamDestination(seed: 99, decisionTick: 500);
        var replay = NpcRules.RoamDestination(seed: 99, decisionTick: 500);

        Assert.Equal(first, replay);
        Assert.InRange(first.X, WorldRules.MapMin, WorldRules.MapMax);
        Assert.InRange(first.Y, WorldRules.MapMin, WorldRules.MapMax);
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
        Assert.Equal(expected, NpcRules.ShouldSearchForTarget(targetAvailable, aggroRange));
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
}
