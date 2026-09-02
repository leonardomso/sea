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
    }
}
