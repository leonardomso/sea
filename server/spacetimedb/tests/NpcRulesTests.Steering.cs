using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The two clocks that decide when an enemy is allowed to ask for a course.
/// Plotting one is A* across a four-hundred-square grid; deciding one is
/// arithmetic. Nothing here changes where she goes, only how often she is
/// allowed to work it out again.
/// </summary>
public sealed class NpcSteeringTests
{
    private const float ShipX = 200f;
    private const float ShipY = 200f;

    [Fact]
    public void A_hunter_replots_her_chase_when_her_clock_is_up()
    {
        var decision = NpcRules.Decide(Chasing() with
        {
            Tick = 40UL,
            NextReplanTick = 40UL,
        });

        Assert.Equal(NpcActionKind.SetCourse, decision.Action);
    }

    [Fact]
    public void A_hunter_holds_the_course_she_has_until_it_is()
    {
        var decision = NpcRules.Decide(Chasing() with
        {
            Tick = 39UL,
            NextReplanTick = 40UL,
        });

        Assert.Equal(NpcActionKind.Hold, decision.Action);
    }

    /// <summary>
    /// The point of the clock: five decisions a second, each of which would
    /// otherwise plot, become two.
    /// </summary>
    [Fact]
    public void A_second_of_chasing_costs_two_courses_and_not_five()
    {
        var plotted = 0;
        var nextReplanTick = 0UL;
        for (var tick = 0UL; tick < 10UL; tick += NpcRules.DecisionIntervalTicks)
        {
            var decision = NpcRules.Decide(Chasing() with
            {
                Tick = tick,
                NextReplanTick = nextReplanTick,
            });
            if (decision.Action != NpcActionKind.SetCourse)
            {
                continue;
            }

            plotted++;
            nextReplanTick = tick + NpcMovementRules.ReplanIntervalTicks;
        }

        Assert.Equal(2, plotted);
    }

    [Fact]
    public void An_idle_enemy_waits_out_her_loiter_before_picking_a_new_leg()
    {
        var waiting = NpcRules.Decide(Idle() with
        {
            Tick = 99UL,
            NextWanderTick = 100UL,
        });
        var ready = NpcRules.Decide(Idle() with
        {
            Tick = 100UL,
            NextWanderTick = 100UL,
        });

        Assert.Equal(NpcActionKind.Hold, waiting.Action);
        Assert.Equal(NpcActionKind.SetCourse, ready.Action);
    }

    /// <summary>
    /// Only the leg an idle ship picks for herself restarts the loiter. A course
    /// plotted at a target is a chase, and a chase is not loitering.
    /// </summary>
    [Fact]
    public void Only_a_patrol_leg_renews_the_loiter()
    {
        var roam = NpcRules.Decide(Idle());
        var chase = NpcRules.Decide(Chasing());

        Assert.True(roam.RenewsWander);
        Assert.False(chase.RenewsWander);
    }

    /// <summary>
    /// Both clocks read zero on a hull that has never plotted anything, so her
    /// first course is not held back by a clock that has not started.
    /// </summary>
    [Fact]
    public void A_hull_that_has_never_plotted_anything_plots_at_once()
    {
        Assert.Equal(NpcActionKind.SetCourse, NpcRules.Decide(Idle()).Action);
        Assert.Equal(NpcActionKind.SetCourse, NpcRules.Decide(Chasing()).Action);
    }

    private static NpcSnapshot Idle() => NpcRulesTests.Snapshot();

    // Eighty squares out on a hull that holds forty-eight, so she is chasing rather than
    // sitting: SEA_5 §11.4 only ever asks for a course when the target is further off than
    // the range she holds, and a hull that is already inside it holds what she has.
    private static NpcSnapshot Chasing() => NpcRulesTests.Snapshot() with
    {
        TargetEntityId = 42,
        TargetAvailable = true,
        DistanceToTarget = 80f,
        TargetX = ShipX + 80f,
        TargetY = ShipY,
    };
}
