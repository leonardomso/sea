#if UNITY_EDITOR
using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaRuntimeValidationRulesTests
    {
        [Test]
        public void Combat_observation_stays_inside_cannon_range_and_holds_position()
        {
            Assert.That(SeaRuntimeValidationRules.CombatObservationRange,
                Is.LessThan(60f));
            Assert.That(SeaRuntimeValidationRules.CombatApproachRange,
                Is.LessThan(SeaRuntimeValidationRules.CombatObservationRange));
            Assert.That(SeaRuntimeValidationRules.ShouldHoldPositionBeforeFire(
                distance: 52f,
                targetSelected: true), Is.True);
            Assert.That(SeaRuntimeValidationRules.ShouldHoldPositionBeforeFire(
                distance: 53f,
                targetSelected: true), Is.False);
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
                SeaRuntimeValidationRules.SyntheticFleetPosition(0, center),
                Is.EqualTo(center + new Vector2(-27f, -27f)));
            Assert.That(
                SeaRuntimeValidationRules.SyntheticFleetPosition(99, center),
                Is.EqualTo(center + new Vector2(27f, 27f)));
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
        }

        [Test]
        public void Tactical_probe_can_find_the_seeded_storm_before_it_enters_interest()
        {
            var initial = SeaRuntimeValidationRules.SeededStormPosition(worldTick: 0);
            var afterTenSeconds = SeaRuntimeValidationRules.SeededStormPosition(worldTick: 100);

            Assert.That(initial.x, Is.EqualTo(-72f).Within(0.001f));
            Assert.That(initial.y, Is.EqualTo(3f).Within(0.001f));
            Assert.That(afterTenSeconds.x, Is.EqualTo(-57.734f).Within(0.001f));
            Assert.That(afterTenSeconds.y, Is.EqualTo(7.635f).Within(0.001f));
        }
    }
}
#endif
