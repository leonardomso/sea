using System;
using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    public sealed class SeaLocalShipPredictionTests
    {
        // A sloop rated the way the seed content rates one: twenty-four units of way, twelve of
        // acceleration either side of it, ninety degrees of helm a second.
        private static readonly SeaSailingParameters Sloop = new(24f, 12f, 12f, 90f);

        private const float Tick = SeaLocalShipPrediction.DefaultStepSeconds;

        private static SeaPredictedMotion Predict(
            SeaSailingState state,
            Vector3 destination,
            float seconds,
            bool hasCourse = true,
            bool isStopping = false,
            SeaSailingParameters? parameters = null) =>
            SeaLocalShipPrediction.Predict(
                state,
                destination,
                hasCourse,
                isStopping,
                parameters ?? Sloop,
                Tick,
                seconds);

        [Test]
        public void A_ship_lying_still_gets_under_way_on_the_first_tick_of_a_new_course()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 90f, 0f),
                new Vector3(100f, 0f, 0f),
                Tick);

            Assert.That(
                predicted.Position.x,
                Is.EqualTo(0.06f).Within(0.001f),
                "Twelve units of acceleration over a tick is 1.2 of way, and the server " +
                "integrates on the average of the two speeds: half of 1.2 for a tenth of a " +
                "second. A ship that does not move here is a click that does nothing.");
        }

        [Test]
        public void A_ship_building_way_covers_the_ground_acceleration_says_she_should()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 90f, 0f),
                new Vector3(500f, 0f, 0f),
                SeaLocalShipPrediction.MaximumPredictionSeconds);

            Assert.That(
                predicted.Position.x,
                Is.EqualTo(1.5f).Within(0.001f),
                "Half of twelve times half a second squared.");
        }

        [Test]
        public void A_ship_under_way_is_carried_forward_along_her_own_heading()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 90f, 24f),
                new Vector3(500f, 0f, 0f),
                0.2f);

            Assert.That(predicted.Position.x, Is.EqualTo(4.8f).Within(0.001f));
            Assert.That(predicted.Position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(predicted.HeadingDegrees, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void A_ship_turns_toward_her_destination_no_faster_than_her_helm_allows()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 0f, 10f),
                new Vector3(100f, 0f, 0f),
                0.5f,
                parameters: new SeaSailingParameters(24f, 12f, 12f, 60f));

            Assert.That(
                predicted.HeadingDegrees,
                Is.EqualTo(30f).Within(0.001f),
                "Half a second at sixty degrees a second is thirty degrees, not the full turn.");
        }

        [Test]
        public void A_hard_turn_costs_a_ship_her_way_and_a_straight_course_does_not()
        {
            var straight = Predict(
                new SeaSailingState(Vector3.zero, 90f, 12f),
                new Vector3(100f, 0f, 0f),
                Tick);
            var swinging = Predict(
                new SeaSailingState(Vector3.zero, 0f, 12f),
                new Vector3(100f, 0f, 0f),
                Tick);

            Assert.That(
                swinging.Position.magnitude,
                Is.LessThan(straight.Position.magnitude),
                "A hull broadside to her course makes no way with her sail, which is why " +
                "coming about slows her down.");
        }

        [Test]
        public void A_prediction_never_sails_past_the_destination_the_server_is_steering_to()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 90f, 24f),
                new Vector3(1f, 0f, 0f),
                0.5f);

            Assert.That(predicted.Position.x, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void A_ship_told_to_stop_carries_her_way_off_rather_than_freezing()
        {
            var predicted = Predict(
                new SeaSailingState(Vector3.zero, 90f, 10f),
                Vector3.zero,
                Tick,
                hasCourse: false,
                isStopping: true);

            Assert.That(
                predicted.Position.x,
                Is.EqualTo(0.94f).Within(0.001f),
                "Ten units of way losing twelve a second still covers ground for a tick.");
        }

        [Test]
        public void A_ship_with_no_course_and_no_way_to_shed_stays_where_the_server_put_her()
        {
            var predicted = Predict(
                new SeaSailingState(new Vector3(5f, 0f, 5f), 45f, 0f),
                new Vector3(50f, 0f, 50f),
                0.3f,
                hasCourse: false);

            Assert.That(predicted.Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(predicted.HeadingDegrees, Is.EqualTo(45f));
        }

        [Test]
        public void Prediction_stops_reckoning_once_the_server_has_gone_quiet()
        {
            var half = Predict(
                new SeaSailingState(Vector3.zero, 90f, 24f),
                new Vector3(500f, 0f, 0f),
                SeaLocalShipPrediction.MaximumPredictionSeconds);
            var far = Predict(
                new SeaSailingState(Vector3.zero, 90f, 24f),
                new Vector3(500f, 0f, 0f),
                5f);

            Assert.That(far.Position.x, Is.EqualTo(half.Position.x).Within(0.001f));
        }

        [Test]
        public void A_negative_or_unreal_elapsed_time_is_rejected_rather_than_guessed_at()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Predict(
                new SeaSailingState(Vector3.zero, 0f, 1f), Vector3.one, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Predict(
                new SeaSailingState(Vector3.zero, 0f, 1f), Vector3.one, float.NaN));
        }

        [Test]
        public void A_small_correction_is_eased_in_rather_than_snapped()
        {
            var eased = SeaLocalShipPrediction.Reconcile(
                Vector3.zero, new Vector3(1f, 0f, 0f), 1f / 60f);

            Assert.That(eased.x, Is.GreaterThan(0f));
            Assert.That(eased.x, Is.LessThan(1f), "A correction is absorbed over several frames.");
        }

        [Test]
        public void A_correction_too_large_to_be_drift_is_taken_at_once()
        {
            var eased = SeaLocalShipPrediction.Reconcile(
                Vector3.zero,
                new Vector3(SeaLocalShipPrediction.SnapDistance + 1f, 0f, 0f),
                1f / 60f);

            Assert.That(
                eased.x,
                Is.EqualTo(SeaLocalShipPrediction.SnapDistance + 1f).Within(0.001f),
                "A respawn is not drift; easing across it would sail the hull through the map.");
        }

        [Test]
        public void A_frame_with_no_elapsed_time_takes_the_prediction_as_it_stands()
        {
            Assert.That(
                SeaLocalShipPrediction.Reconcile(Vector3.zero, new Vector3(1f, 0f, 0f), 0f).x,
                Is.EqualTo(1f));
        }
    }
}
