using System;
using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    public sealed class SeaLocalShipPredictionTests
    {
        [Test]
        public void A_ship_under_way_is_carried_forward_along_its_own_heading()
        {
            var predicted = SeaLocalShipPrediction.Predict(
                position: Vector3.zero,
                headingDegrees: 90f,
                speed: 24f,
                destination: new Vector3(100f, 0f, 0f),
                hasCourse: true,
                turnRateDegrees: 0f,
                seconds: 0.25f);

            Assert.That(predicted.Position.x, Is.EqualTo(6f).Within(0.001f));
            Assert.That(predicted.Position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(predicted.HeadingDegrees, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void A_ship_turns_toward_its_destination_no_faster_than_its_helm_allows()
        {
            var predicted = SeaLocalShipPrediction.Predict(
                position: Vector3.zero,
                headingDegrees: 0f,
                speed: 10f,
                destination: new Vector3(100f, 0f, 0f),
                hasCourse: true,
                turnRateDegrees: 60f,
                seconds: 0.5f);

            Assert.That(
                predicted.HeadingDegrees,
                Is.EqualTo(30f).Within(0.001f),
                "Half a second at sixty degrees a second is thirty degrees, not the full turn.");
        }

        [Test]
        public void A_prediction_never_sails_past_the_destination_the_server_is_steering_to()
        {
            var predicted = SeaLocalShipPrediction.Predict(
                position: Vector3.zero,
                headingDegrees: 90f,
                speed: 100f,
                destination: new Vector3(3f, 0f, 0f),
                hasCourse: true,
                turnRateDegrees: 0f,
                seconds: 0.5f);

            Assert.That(predicted.Position.x, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void A_ship_without_a_course_or_without_way_on_stays_where_the_server_put_it()
        {
            var stopped = SeaLocalShipPrediction.Predict(
                new Vector3(5f, 0f, 5f), 45f, 0f, new Vector3(50f, 0f, 50f), true, 90f, 0.3f);
            var adrift = SeaLocalShipPrediction.Predict(
                new Vector3(5f, 0f, 5f), 45f, 24f, new Vector3(50f, 0f, 50f), false, 90f, 0.3f);

            Assert.That(stopped.Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(adrift.Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
        }

        [Test]
        public void Prediction_stops_reckoning_once_the_server_has_gone_quiet()
        {
            var half = SeaLocalShipPrediction.Predict(
                Vector3.zero, 90f, 24f, new Vector3(500f, 0f, 0f), true, 0f, 0.5f);
            var far = SeaLocalShipPrediction.Predict(
                Vector3.zero, 90f, 24f, new Vector3(500f, 0f, 0f), true, 0f, 5f);

            Assert.That(far.Position.x, Is.EqualTo(half.Position.x).Within(0.001f));
        }

        [Test]
        public void A_negative_or_unreal_elapsed_time_is_rejected_rather_than_guessed_at()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SeaLocalShipPrediction.Predict(
                Vector3.zero, 0f, 1f, Vector3.one, true, 1f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SeaLocalShipPrediction.Predict(
                Vector3.zero, 0f, 1f, Vector3.one, true, 1f, float.NaN));
        }

        [Test]
        public void A_small_correction_is_eased_in_rather_than_snapped()
        {
            var rendered = new Vector3(0f, 0f, 0f);
            var predicted = new Vector3(1f, 0f, 0f);

            var eased = SeaLocalShipPrediction.Reconcile(rendered, predicted, 1f / 60f);

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
