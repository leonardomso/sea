using System;
using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaSailingState
    {
        public SeaSailingState(Vector3 position, float headingDegrees, float speed)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            Speed = speed;
        }

        public Vector3 Position { get; }

        public float HeadingDegrees { get; }

        public float Speed { get; }
    }

    public readonly struct SeaSailingParameters
    {
        public SeaSailingParameters(
            float maximumSpeed,
            float acceleration,
            float deceleration,
            float turnRateDegrees)
        {
            MaximumSpeed = maximumSpeed;
            Acceleration = acceleration;
            Deceleration = deceleration;
            TurnRateDegrees = turnRateDegrees;
        }

        public float MaximumSpeed { get; }

        public float Acceleration { get; }

        public float Deceleration { get; }

        public float TurnRateDegrees { get; }
    }

    public readonly struct SeaSailingStep
    {
        public SeaSailingStep(
            Vector3 position,
            float headingDegrees,
            float speed,
            bool isMoving,
            bool arrived)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            Speed = speed;
            IsMoving = isMoving;
            Arrived = arrived;
        }

        public Vector3 Position { get; }

        public float HeadingDegrees { get; }

        public float Speed { get; }

        public bool IsMoving { get; }

        public bool Arrived { get; }
    }

    /// <summary>
    /// How a hull answers her helm, in the client's own terms. This is a deliberate mirror of the
    /// server's SailingRules.StepTowardHeading: the same acceleration, the same braking curve into
    /// a destination, the same loss of thrust while the bow is still swinging onto a new course,
    /// the same arrival test. Any place the two drift apart the local ship is drawn somewhere the
    /// server will not agree with, and the correction that follows is what a captain reads as the
    /// ship behaving oddly. Change this only alongside the server rule.
    /// </summary>
    public static class SeaSailingRules
    {
        public static SeaSailingStep Step(
            SeaSailingState state,
            Vector3 destination,
            bool stopping,
            SeaSailingParameters parameters,
            float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            var deltaX = destination.x - state.Position.x;
            var deltaZ = destination.z - state.Position.z;
            var remainingSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            var heading = ResolveHeading(
                state.HeadingDegrees,
                DesiredHeading(deltaX, deltaZ, remainingSquared),
                remainingSquared,
                stopping,
                parameters.TurnRateDegrees * deltaSeconds,
                out var thrustAlignment);

            var alignedMaximumSpeed = parameters.MaximumSpeed * thrustAlignment;
            var targetSpeed = stopping
                ? 0f
                : BrakingLimitedSpeed(
                    alignedMaximumSpeed,
                    parameters.Deceleration,
                    remainingSquared);
            var speedChange = targetSpeed > state.Speed
                ? parameters.Acceleration * deltaSeconds
                : parameters.Deceleration * deltaSeconds;
            var speed = Mathf.MoveTowards(state.Speed, targetSpeed, speedChange);

            // The server integrates on the average of the two speeds rather than the new one, so
            // a hull just cast off covers half a tick's ground, not a full one. Reckoning it any
            // other way puts the bow ahead of the server on the very first tick of every voyage.
            var travel = (state.Speed + speed) * 0.5f * deltaSeconds;
            var radians = heading * Mathf.Deg2Rad;
            var directionX = Mathf.Sin(radians);
            var directionZ = Mathf.Cos(radians);

            if (!stopping &&
                ((remainingSquared <= Square(Mathf.Max(0.05f, travel)) &&
                  speed <= parameters.Deceleration * deltaSeconds) ||
                 ((travel * travel) >= remainingSquared && thrustAlignment >= 0.95f)))
            {
                return new SeaSailingStep(
                    new Vector3(destination.x, state.Position.y, destination.z),
                    heading,
                    0f,
                    false,
                    true);
            }

            var position = new Vector3(
                state.Position.x + (directionX * travel),
                state.Position.y,
                state.Position.z + (directionZ * travel));
            var moving = speed > 0.001f || (!stopping && remainingSquared > 0.0025f);
            return new SeaSailingStep(position, heading, speed, moving, false);
        }

        private static float DesiredHeading(float deltaX, float deltaZ, float remainingSquared) =>
            remainingSquared <= 0.000001f
                ? 0f
                : Mathf.Atan2(deltaX, deltaZ) * Mathf.Rad2Deg;

        // A ship makes way with the component of her sail that points where she is going. Broadside
        // to the course she makes none at all, which is why a hard turn costs speed and a gentle
        // one barely does.
        private static float ResolveHeading(
            float currentHeading,
            float desiredHeading,
            float remainingSquared,
            bool stopping,
            float maximumTurn,
            out float thrustAlignment)
        {
            thrustAlignment = 1f;
            if (stopping || remainingSquared <= 0.000001f)
            {
                return currentHeading;
            }

            var heading = Mathf.MoveTowardsAngle(currentHeading, desiredHeading, maximumTurn);
            var headingError = Mathf.DeltaAngle(heading, desiredHeading);
            thrustAlignment = Mathf.Max(0f, Mathf.Cos(headingError * Mathf.Deg2Rad));
            return heading;
        }

        // Full sail until the ship is inside the distance she needs to shed her way, then only as
        // much speed as she can still lose before the destination. This is what stops a hull from
        // sailing past her mark and being dragged back.
        private static float BrakingLimitedSpeed(
            float maximumSpeed,
            float deceleration,
            float remainingSquared)
        {
            if (deceleration <= 0f)
            {
                return maximumSpeed;
            }

            var brakingDistance = maximumSpeed * maximumSpeed / (2f * deceleration);
            if (remainingSquared >= brakingDistance * brakingDistance)
            {
                return maximumSpeed;
            }

            return Mathf.Sqrt(
                Mathf.Max(0f, 2f * deceleration * Mathf.Sqrt(remainingSquared)));
        }

        private static float Square(float value) => value * value;
    }
}
