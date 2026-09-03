using System;
using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaPredictedMotion
    {
        public SeaPredictedMotion(Vector3 position, float headingDegrees)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
        }

        public Vector3 Position { get; }

        public float HeadingDegrees { get; }
    }

    /// <summary>
    /// Dead reckoning for the ship the player steers. Remote ships are interpolated a render
    /// delay behind the server, which is right for them and wrong for the local ship: it turns
    /// every click into a visible wait. The local ship is instead carried forward from its newest
    /// snapshot along the course the server already agreed to, and the difference against the
    /// next snapshot is absorbed rather than snapped.
    /// </summary>
    public static class SeaLocalShipPrediction
    {
        // Past half a second the prediction is guessing rather than reckoning: the server has
        // missed five ticks, and running further ahead only makes the correction worse.
        public const float MaximumPredictionSeconds = 0.5f;

        // Error decays by 1 - exp(-k * dt), so at 14 a correction is nine tenths gone in 165ms:
        // fast enough to stay honest, slow enough that nobody sees the ship jump.
        public const float ReconcileSharpness = 14f;

        // A gap this wide is not drift, it is a respawn, a teleport, or a rejected course.
        // Easing across it would sail the hull through the map.
        public const float SnapDistance = 15f;

        public static SeaPredictedMotion Predict(
            Vector3 position,
            float headingDegrees,
            float speed,
            Vector3 destination,
            bool hasCourse,
            float turnRateDegrees,
            float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            var elapsed = Mathf.Min(seconds, MaximumPredictionSeconds);
            var remaining = new Vector3(destination.x - position.x, 0f, destination.z - position.z);
            var distance = remaining.magnitude;
            if (!hasCourse || speed <= 0f || distance <= 0.001f || elapsed <= 0f)
            {
                return new SeaPredictedMotion(position, headingDegrees);
            }

            var desiredHeading = Mathf.Atan2(remaining.x, remaining.z) * Mathf.Rad2Deg;
            var heading = Mathf.MoveTowardsAngle(
                headingDegrees,
                desiredHeading,
                Mathf.Max(0f, turnRateDegrees) * elapsed);

            // Never sail past the destination the server is steering to: overshooting it would
            // be corrected backwards on the very next snapshot, which reads as a stutter.
            var travel = Mathf.Min(speed * elapsed, distance);
            var radians = heading * Mathf.Deg2Rad;
            var predicted = new Vector3(
                position.x + (Mathf.Sin(radians) * travel),
                position.y,
                position.z + (Mathf.Cos(radians) * travel));
            return new SeaPredictedMotion(predicted, heading);
        }

        public static Vector3 Reconcile(Vector3 rendered, Vector3 predicted, float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                return predicted;
            }

            var error = predicted - rendered;
            if (error.sqrMagnitude >= SnapDistance * SnapDistance)
            {
                return predicted;
            }

            return rendered + (error * (1f - Mathf.Exp(-ReconcileSharpness * deltaSeconds)));
        }
    }
}
