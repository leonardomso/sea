using UnityEngine;

namespace Sea.Client
{
    public enum SeaPresentationPlatform : byte
    {
        Other = 0,
        MacOS = 1,
        WebGL = 2,
    }

    public enum SeaPresentationLevel : byte
    {
        Hidden = 0,
        Distant = 1,
        Medium = 2,
        Near = 3,
    }

    public static class SeaPresentationRules
    {
        public const float NearDistance = 38f;
        public const float MediumDistance = 76f;
        public const float DistantDistance = 120f;

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

        public static SeaPresentationLevel LevelFor(float distance, bool isRelevantEndpoint)
        {
            if (isRelevantEndpoint && distance > DistantDistance)
            {
                return SeaPresentationLevel.Distant;
            }

            if (distance <= NearDistance)
            {
                return SeaPresentationLevel.Near;
            }

            if (distance <= MediumDistance)
            {
                return SeaPresentationLevel.Medium;
            }

            return distance <= DistantDistance
                ? SeaPresentationLevel.Distant
                : SeaPresentationLevel.Hidden;
        }
    }
}
