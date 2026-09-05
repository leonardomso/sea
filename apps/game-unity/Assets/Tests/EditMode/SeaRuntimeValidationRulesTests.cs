#if UNITY_EDITOR
using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaRuntimeValidationRulesTests
    {
        [Test]
        public void Combat_observation_stays_inside_cannon_range()
        {
            Assert.That(SeaRuntimeValidationRules.CombatObservationRange,
                Is.LessThan(60f));
            Assert.That(SeaRuntimeValidationRules.CombatApproachRange,
                Is.LessThan(SeaRuntimeValidationRules.CombatObservationRange));
        }

        // Chart bearings: north is the smaller y, so a target ten squares up the chart is
        // due north and answers 0. The first and last cases read the other way round, which
        // is the compass this file's own SeededStormPosition never used.
        [TestCase(0f, 0f, 10f, 180f)]
        [TestCase(0f, 10f, 0f, 90f)]
        [TestCase(90f, -10f, 0f, -90f)]
        [TestCase(180f, 0f, -10f, 0f)]
        public void Combat_probe_steers_at_the_target_because_every_gun_bears(
            float headingDegrees,
            float targetX,
            float targetY,
            float expectedHeading)
        {
            var decision = SeaRuntimeValidationRules.PlanFire(
                Vector2.zero,
                headingDegrees,
                new Vector2(targetX, targetY));

            Assert.That(decision.CanFire, Is.True);
            Assert.That(
                Mathf.DeltaAngle(expectedHeading, decision.DesiredHeadingDegrees),
                Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Combat_probe_will_not_fire_on_a_target_sitting_on_its_own_hull()
        {
            // No bearing exists, so there is no heading to hold and nothing to shoot at; the
            // probe keeps the heading it already had rather than snapping to zero.
            var decision = SeaRuntimeValidationRules.PlanFire(Vector2.zero, 37f, Vector2.zero);

            Assert.That(decision.CanFire, Is.False);
            Assert.That(decision.DesiredHeadingDegrees, Is.EqualTo(37f));
        }

        [Test]
        public void The_runtime_probe_reads_a_launched_volley_from_the_magazine()
        {
            // Milestone 1 hands out unlimited ammunition, so a spent round is no longer evidence.
            Assert.That(
                SeaRuntimeValidationRules.HasLaunchedVolley(3, 2, 100, 100),
                Is.True,
                "a ready volley left the racks");
            Assert.That(
                SeaRuntimeValidationRules.HasLaunchedVolley(3, 3, 100, 140),
                Is.True,
                "the module stamped a newer shot tick");
            Assert.That(
                SeaRuntimeValidationRules.HasLaunchedVolley(3, 3, 100, 100),
                Is.False,
                "nothing on the hull moved, so nothing was fired");
            Assert.That(
                SeaRuntimeValidationRules.HasLaunchedVolley(2, 3, 100, 100),
                Is.False,
                "a reload finishing is not a shot");
        }

        [TestCase(0, 100, true)]
        [TestCase(1, 100, true)]
        [TestCase(99, 100, true)]
        [TestCase(100, 100, false)]
        [TestCase(101, 100, false)]
        public void Performance_probe_restores_a_fleet_removed_by_subscription_reset(
            int visibleCount,
            int requiredCount,
            bool expected)
        {
            Assert.That(
                SeaRuntimeValidationRules.ShouldRestoreSyntheticFleet(
                    visibleCount,
                    requiredCount),
                Is.EqualTo(expected));
        }

        [Test]
        public void Performance_fleet_is_centered_on_the_current_camera()
        {
            var center = new Vector2(140f, -85f);

            Assert.That(
                SeaRuntimeValidationRules.SyntheticFleetPosition(0, 100, center),
                Is.EqualTo(center + new Vector2(-27f, -27f)));
            Assert.That(
                SeaRuntimeValidationRules.SyntheticFleetPosition(99, 100, center),
                Is.EqualTo(center + new Vector2(27f, 27f)));
        }

        [Test]
        public void Macos_performance_fleet_keeps_all_250_ships_visible()
        {
            var center = Vector2.zero;

            for (var index = 0; index < 250; index++)
            {
                var position = SeaRuntimeValidationRules.SyntheticFleetPosition(
                    index,
                    250,
                    center);
                Assert.That(
                    SeaPresentationRules.IsVisible(position.magnitude, false),
                    Is.True,
                    $"Synthetic ship {index} was outside the presentation radius.");
            }
        }

        [TestCase(true, 2f, 6f, 0f, false, false, true)]
        [TestCase(true, 2f, 6f, 3f, true, true, true)]
        [TestCase(true, 2f, 6f, 6f, true, true, false)]
        [TestCase(false, 2f, 6f, 0f, false, false, false)]
        public void Runtime_movement_accepts_progressive_or_completed_stop(
            bool stopRequested,
            float travelled,
            float speedBeforeStop,
            float currentSpeed,
            bool isMoving,
            bool isStopping,
            bool expected)
        {
            Assert.That(
                SeaRuntimeValidationRules.HasObservedStop(
                    stopRequested,
                    travelled,
                    speedBeforeStop,
                    currentSpeed,
                    isMoving,
                    isStopping),
                Is.EqualTo(expected));
        }

        [Test]
        public void Client_performance_evidence_requires_every_budget()
        {
            var evidence = new SeaClientPerformanceEvidence
            {
                platform = "OSXPlayer",
                recordedAtUtc = "2026-09-02T00:00:00.0000000Z",
                visibleShips = 250,
                frameP95Milliseconds = 16.7f,
                frameP99Milliseconds = 25f,
                idleBytesPerFrame = 0,
                poolsStable = true,
                runtimeErrors = 0,
                missingAssets = 0,
            };

            Assert.That(evidence.MeetsBudget(250), Is.True);

            evidence.idleBytesPerFrame = 1;
            Assert.That(evidence.MeetsBudget(250), Is.False);
        }

        [Test]
        public void Presentation_benchmark_sails_without_a_live_world()
        {
            Assert.That(
                SeaRuntimeValidationRules.ShouldConnectOnStart(
                    connectOnStart: true,
                    presentationPerformanceRequested: false),
                Is.True);
            Assert.That(
                SeaRuntimeValidationRules.ShouldConnectOnStart(
                    connectOnStart: true,
                    presentationPerformanceRequested: true),
                Is.False);
            Assert.That(
                SeaRuntimeValidationRules.ShouldConnectOnStart(
                    connectOnStart: false,
                    presentationPerformanceRequested: false),
                Is.False);
        }

        [Test]
        public void Runtime_arguments_support_command_line_and_webgl_query_values()
        {
            var arguments = new[] { "game", "-seaProfile", "captain-1" };
            var url = "http://127.0.0.1:4173/?seaPresentationPerformanceTest=1" +
                "&seaProfile=web%20captain";

            Assert.That(
                SeaRuntimeArguments.Has(
                    "-seaPresentationPerformanceTest",
                    arguments,
                    url),
                Is.True);
            Assert.That(
                SeaRuntimeArguments.Value("-seaProfile", arguments, url),
                Is.EqualTo("captain-1"));
            Assert.That(
                SeaRuntimeArguments.Value("-missing", arguments, url),
                Is.Null);
        }

        [Test]
        public void Runtime_evidence_requires_each_requested_scenario()
        {
            var evidence = new SeaRuntimeScenarioEvidence
            {
                movementRequired = true,
                combatRequired = true,
                progressionRequired = false,
                tacticalRequired = false,
                movementObserved = true,
                combatObserved = false,
            };

            Assert.That(evidence.IsComplete(), Is.False);

            evidence.combatObserved = true;
            Assert.That(evidence.IsComplete(), Is.True);
        }

        [Test]
        public void Combat_probe_subscribes_to_npcs_without_loading_player_ships()
        {
            Assert.That(
                SeaRuntimeValidationRules.RuntimeNpcSubscriptionQuery,
                Is.EqualTo("SELECT * FROM ship WHERE faction_code = 2"));
            Assert.That(
                SeaRuntimeValidationRules.RuntimeNpcSubscriptionQuery,
                Does.Not.Contain("faction_code = 1"));
            Assert.That(
                SeaRuntimeValidationRules.RuntimeMovementSubscriptionQuery,
                Is.EqualTo("SELECT * FROM ship_movement WHERE is_active = true"));
        }

        [Test]
        public void Tactical_probe_can_find_the_seeded_storm_before_it_enters_interest()
        {
            var initial = SeaRuntimeValidationRules.SeededStormPosition(worldTick: 0);
            var afterTenSeconds = SeaRuntimeValidationRules.SeededStormPosition(worldTick: 100);

            // Where maps.json stands the storm, and where a bearing of 72 carries it in ten
            // seconds at half a square a second: east and a little north, so y falls.
            Assert.That(initial.x, Is.EqualTo(56f).Within(0.001f));
            Assert.That(initial.y, Is.EqualTo(206f).Within(0.001f));
            Assert.That(afterTenSeconds.x, Is.EqualTo(60.755f).Within(0.001f));
            Assert.That(afterTenSeconds.y, Is.EqualTo(204.455f).Within(0.001f));
        }

        [TestCase(true, true, 0, true)]
        [TestCase(false, true, 0, false)]
        [TestCase(true, false, 0, false)]
        [TestCase(true, true, 1, false)]
        [TestCase(true, true, 3, false)]
        public void Tactical_commands_wait_for_an_operational_ship(
            bool active,
            bool alive,
            byte modeCode,
            bool expected)
        {
            Assert.That(
                SeaRuntimeValidationRules.CanIssueTacticalCommand(active, alive, modeCode),
                Is.EqualTo(expected));
        }

        [TestCase(false, 10f, 11.9f, false)]
        [TestCase(false, 10f, 12f, true)]
        [TestCase(true, 10f, 20f, false)]
        public void Tactical_probe_retries_an_unobserved_authoritative_command(
            bool observed,
            float requestedAt,
            float now,
            bool expected)
        {
            Assert.That(
                SeaRuntimeValidationRules.ShouldRetryTacticalCommand(
                    observed,
                    requestedAt,
                    now),
                Is.EqualTo(expected));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        public void Tactical_damage_requires_authoritative_storm_exposure(
            byte exposureCode,
            bool expected)
        {
            Assert.That(
                SeaRuntimeValidationRules.HasStormExposure(exposureCode),
                Is.EqualTo(expected));
        }
    }
}
#endif
