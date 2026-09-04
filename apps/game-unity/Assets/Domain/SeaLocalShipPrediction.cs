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
    /// Dead reckoning for the ship the player steers. Remote ships are interpolated a render delay
    /// behind the server, which is right for them and wrong for the local ship: it turns every
    /// click into a visible wait. The local ship is instead sailed forward from her newest
    /// snapshot by the same rule the server sails her with, one server tick at a time, and the
    /// difference against the next snapshot is absorbed rather than snapped.
    ///
    /// Running the real rule rather than an approximation of it is the whole point. A simpler
    /// model - constant speed, no braking, nothing from rest - does not fail by being slightly
    /// off; it fails by disagreeing with the server about what the ship is doing, so the
    /// correction never settles and the hull is permanently being tugged.
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

        // The server advances a ship in whole ticks, so the reckoning matches it best when it
        // does the same and spends only the remainder on a partial step.
        public const float DefaultStepSeconds = 0.1f;

        private const float ShortestStepSeconds = 1f / 120f;

        public static SeaPredictedMotion Predict(
            SeaSailingState state,
            Vector3 destination,
            bool hasCourse,
            bool isStopping,
            SeaSailingParameters parameters,
            float secondsPerStep,
            float seconds)
        {
            if (!float.IsFinite(seconds) || seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            var remaining = Mathf.Min(seconds, MaximumPredictionSeconds);

            // A ship with no course and no way to shed is where the server left her. Note that a
            // stopping ship still reckons: she is carrying way off, and freezing her there is the
            // stutter at the end of every voyage.
            if (remaining <= 0f || (!hasCourse && !isStopping))
            {
                return new SeaPredictedMotion(state.Position, state.HeadingDegrees);
            }

            var step = float.IsFinite(secondsPerStep)
                ? Mathf.Max(ShortestStepSeconds, secondsPerStep)
                : DefaultStepSeconds;
            var position = state.Position;
            var heading = state.HeadingDegrees;
            var speed = state.Speed;
            while (remaining > 0f)
            {
                var slice = Mathf.Min(step, remaining);
                var advanced = SeaSailingRules.Step(
                    new SeaSailingState(position, heading, speed),
                    destination,
                    isStopping,
                    parameters,
                    slice);
                position = advanced.Position;
                heading = advanced.HeadingDegrees;
                speed = advanced.Speed;
                remaining -= slice;
                if (advanced.Arrived || !advanced.IsMoving)
                {
                    break;
                }
            }

            return new SeaPredictedMotion(position, heading);
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
