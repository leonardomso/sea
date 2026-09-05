using System;
using UnityEngine;

namespace Sea.Client
{
    /// <summary>A drawn hull: where she is in the scene and which way her bow points.</summary>
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
    /// Dead reckoning for the ship the player steers, in chart squares. Remote ships are
    /// interpolated a render delay behind the server, which is right for them and wrong for the
    /// local ship: it turns every click into a visible wait. This one walks the route the server
    /// sent her, at the speed the server says she is making, with the rule the server walks it
    /// with (SEA_5 12.2).
    /// </summary>
    /// <remarks>
    /// Running the real rule rather than an approximation of it is the whole point. A model that
    /// guesses does not fail by being slightly off; it fails by disagreeing with the server about
    /// what the ship is doing, so the correction never settles and the hull is permanently being
    /// tugged. Since SEA_5 4.2 removed inertia there is nothing left to approximate: a route, a
    /// speed and a clock are the whole of it.
    /// <para>
    /// The speed is the one the server sent, not the hull's rated figure. Wind, storms, damage
    /// and slows are all already in it, and reading the rating instead is what drew a ship in a
    /// storm ahead of herself and pulled her back on every update.
    /// </para>
    /// </remarks>
    public sealed class SeaLocalShipPrediction
    {
        /// <summary>
        /// How far past the last thing the server said reckoning is still reckoning rather than
        /// guessing. The caller owns the clock, so this is the budget it spends against.
        /// </summary>
        public const float MaximumPredictionSeconds = 0.5f;

        /// <summary>
        /// A route turns a corner instantly, which is correct and looks wrong. The drawn heading
        /// catches up over four hundred milliseconds; the position is never smoothed, because
        /// that is the thing the server is authoritative about (SEA_5 6.2).
        /// </summary>
        public const float HeadingCatchUpSeconds = 0.4f;

        /// <summary>
        /// How far the drawn hull may be from the server's before she is moved rather than eased.
        /// SEA_5 12.3 sets this at one square: below it a captain cannot see the difference, above
        /// it she can, and easing a two-square error would leave the hull wrong for most of a
        /// second. Straight-line movement makes the second case rare -- only a lost packet or a
        /// course we have not heard about yet opens a gap that wide.
        /// </summary>
        public const float SnapToleranceSquares = 1.0f;

        /// <summary>
        /// How long an error under the tolerance takes to close. Short enough that it is gone
        /// before the next server tick, slow enough that it reads as the ship settling rather
        /// than as a jump.
        /// </summary>
        public const float ErrorEaseSeconds = 0.2f;

        private Vector2[] route;
        private uint routeVersion;
        private int waypointIndex;
        private float speedSquaresPerSecond;
        private float serverHeadingDegrees;
        private float headingCatchUpDegreesPerSecond;
        private Vector2 correction;
        private bool hasServerUpdate;

        /// <summary>Where the hull is drawn, in chart squares.</summary>
        public Vector2 Position { get; private set; }

        /// <summary>The bearing she is drawn on, which lags the server's while a corner is
        /// being turned and never lags it by more than <see cref="HeadingCatchUpSeconds"/>.</summary>
        public float DrawnHeadingDegrees { get; private set; }

        /// <summary>Whether there is anywhere left for her to go.</summary>
        public bool HasRoute => route != null && waypointIndex < route.Length - 1;

        /// <summary>
        /// Takes everything the server has just said about her. A new route version is a new
        /// course and she is put on it; the same version is the same course, so the route is
        /// already the one being walked and only the disagreement about where along it she is
        /// needs settling.
        /// </summary>
        public void OnServerUpdate(
            Vector2 position,
            float headingDegrees,
            float effectiveSpeed,
            Vector2[] route,
            uint routeVersion)
        {
            if (!float.IsFinite(effectiveSpeed) || effectiveSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveSpeed));
            }

            speedSquaresPerSecond = effectiveSpeed;
            AimHeading(headingDegrees);
            if (!hasServerUpdate || routeVersion != this.routeVersion)
            {
                this.route = route;
                this.routeVersion = routeVersion;
                waypointIndex = 0;
                correction = Vector2.zero;
                Position = position;
                hasServerUpdate = true;
                return;
            }

            SettleAgainst(position);
        }

        /// <summary>Sails her forward by one frame.</summary>
        public void Advance(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f)
            {
                return;
            }

            var step = SeaRouteRules.Advance(
                route,
                waypointIndex,
                Position,
                DrawnHeadingDegrees,
                speedSquaresPerSecond * seconds);
            if (step.WaypointIndex != waypointIndex)
            {
                // She has turned a corner. The server's last bearing is now the leg behind her,
                // so the bearing to catch up to is the one the route itself gives.
                AimHeading(step.HeadingDegrees);
            }

            waypointIndex = step.WaypointIndex;
            Position = step.Position + TakeCorrection(seconds);
            DrawnHeadingDegrees = SeaGeometry.NormalizeAngle(Mathf.MoveTowardsAngle(
                DrawnHeadingDegrees,
                serverHeadingDegrees,
                headingCatchUpDegreesPerSecond * seconds));
        }

        /// <summary>
        /// Points the catch-up at a new bearing and works out how fast it has to turn to arrive
        /// in <see cref="HeadingCatchUpSeconds"/>. The rate is fixed when the bearing changes
        /// rather than recomputed every frame, so the turn is even instead of easing out into a
        /// long crawl.
        /// </summary>
        private void AimHeading(float headingDegrees)
        {
            serverHeadingDegrees = SeaGeometry.NormalizeAngle(headingDegrees);
            if (!hasServerUpdate)
            {
                // Nothing to catch up from. Easing out of a heading of zero would swing every
                // ship in the fleet round from north the moment she is first drawn.
                DrawnHeadingDegrees = serverHeadingDegrees;
                headingCatchUpDegreesPerSecond = 0f;
                return;
            }

            headingCatchUpDegreesPerSecond =
                Mathf.Abs(Mathf.DeltaAngle(DrawnHeadingDegrees, serverHeadingDegrees)) /
                HeadingCatchUpSeconds;
        }

        /// <summary>
        /// Decides what to do about the server disagreeing with where she is drawn: put her
        /// there, or keep her place and fold the difference into the next few frames.
        /// </summary>
        private void SettleAgainst(Vector2 position)
        {
            var error = position - (Position + correction);
            if (error.sqrMagnitude >= SnapToleranceSquares * SnapToleranceSquares)
            {
                Position = position;
                correction = Vector2.zero;
                return;
            }

            correction += error;
        }

        /// <summary>How much of the outstanding disagreement this frame absorbs.</summary>
        private Vector2 TakeCorrection(float seconds)
        {
            if (correction == Vector2.zero)
            {
                return Vector2.zero;
            }

            var share = Mathf.Min(1f, seconds / ErrorEaseSeconds);
            var applied = correction * share;
            correction -= applied;
            return applied;
        }
    }
}
