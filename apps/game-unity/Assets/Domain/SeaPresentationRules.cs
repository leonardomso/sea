using UnityEngine;

namespace Sea.Client
{
    public enum SeaPresentationPlatform : byte
    {
        Other = 0,
        MacOS = 1,
        WebGL = 2,
    }

    public static class SeaPresentationRules
    {
        public const float VisibleDistance = 120f;

        /// <summary>
        /// How far the captain can see, in squares. Mirrors the server's
        /// <c>RangeRules.ViewDistanceSquares</c>: the fog must clear exactly as far as the
        /// server is willing to tell this client about, or the sea past the subscription
        /// draws as open water with nothing in it.
        /// </summary>
        /// <remarks>
        /// This read 110 as a mirror of a <c>WorldRules.VisionRadius</c> that no longer
        /// exists; 110 was world units, which is eleven squares, and the constant kept the
        /// number after the unit went away. It has been an orphan pointing at a deleted
        /// server rule ever since.
        /// </remarks>
        public const float VisionRadius = 60f;

        public static int VisibleShipLimit(SeaPresentationPlatform platform) => platform switch
        {
            SeaPresentationPlatform.WebGL => 100,
            _ => 250,
        };

        public static SeaPresentationPlatform CurrentPlatform() => Application.platform switch
        {
            RuntimePlatform.WebGLPlayer => SeaPresentationPlatform.WebGL,
            RuntimePlatform.OSXPlayer => SeaPresentationPlatform.MacOS,
            _ => SeaPresentationPlatform.Other,
        };

        public static bool IsVisible(float distance, bool isRelevantEndpoint) =>
            isRelevantEndpoint || distance <= VisibleDistance;

        public static bool IsInVision(float distance) => distance <= VisionRadius;
    }
}
