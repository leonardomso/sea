using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// The ring that answers a click on the water. It replaces the course line that used to hang
    /// between the ship and its destination for the whole voyage: that line told the player
    /// nothing they could not read from the bow, and it is what read as "a weird line on the sea".
    /// A ping confirms the order at the moment it is given and then gets out of the way.
    /// </summary>
    public static class SeaClickPingRules
    {
        public const float DurationSeconds = 0.6f;
        public const float StartRadius = 0.8f;
        public const float EndRadius = 4f;
        public const float PeakAlpha = 0.85f;
        public const int Segments = 40;

        public static bool IsAlive(float elapsedSeconds) =>
            elapsedSeconds >= 0f && elapsedSeconds < DurationSeconds;

        public static float Progress(float elapsedSeconds) =>
            Mathf.Clamp01(elapsedSeconds / DurationSeconds);

        // The ring opens quickly and then eases, the way a splash does; a linear expansion reads
        // as a mechanical animation rather than an impact on the water.
        public static float RadiusAt(float elapsedSeconds) =>
            Mathf.Lerp(StartRadius, EndRadius, Mathf.Sqrt(Progress(elapsedSeconds)));

        // Fades out on a square so the ring is still solid while the eye is drawn to it and gone
        // before it becomes another permanent mark on the water.
        public static float AlphaAt(float elapsedSeconds)
        {
            var remaining = 1f - Progress(elapsedSeconds);
            return PeakAlpha * remaining * remaining;
        }

        public static Vector3 SegmentPosition(Vector3 center, int index, float radius)
        {
            var angle = index * Mathf.PI * 2f / Segments;
            return new Vector3(
                center.x + (Mathf.Cos(angle) * radius),
                center.y,
                center.z + (Mathf.Sin(angle) * radius));
        }
    }
}
