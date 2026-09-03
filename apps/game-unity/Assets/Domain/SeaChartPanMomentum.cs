using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// The chart camera's WASD glide. Pan input ramps the velocity up and releasing it
    /// lets the velocity coast down, so scouting ahead never feels like the camera is
    /// snapping between stops. The glide is a separate concern from the follow state:
    /// recentering on the ship has to stop the glide as well, or the leftover velocity
    /// keeps pushing the chart away from the ship the follow is pulling it back to.
    /// </summary>
    public sealed class SeaChartPanMomentum
    {
        // Below this the glide is under a millimetre per frame; keeping it alive would
        // only make the follow fight a drift the player cannot see.
        private const float RestingSpeedSquared = 0.0001f;

        public Vector2 Velocity { get; private set; }

        public bool IsGliding => Velocity != Vector2.zero;

        public Vector2 Advance(
            Vector2 panInput,
            float unitsPerSecond,
            float sharpness,
            float deltaSeconds)
        {
            Velocity = Vector2.Lerp(
                Velocity,
                panInput * unitsPerSecond,
                1f - Mathf.Exp(-sharpness * deltaSeconds));
            if (Velocity.sqrMagnitude < RestingSpeedSquared)
            {
                Velocity = Vector2.zero;
            }

            return Velocity;
        }

        public void Stop() => Velocity = Vector2.zero;
    }
}
