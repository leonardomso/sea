using UnityEngine;

namespace Sea.Client
{
    public static class SeaVolleyPresentationRules
    {
        public static float Progress(ulong firedAtTick, ulong impactAtTick, ulong currentTick)
        {
            if (impactAtTick <= firedAtTick)
            {
                return currentTick >= impactAtTick ? 1f : 0f;
            }

            var elapsed = currentTick <= firedAtTick ? 0ul : currentTick - firedAtTick;
            var duration = impactAtTick - firedAtTick;
            return Mathf.Clamp01((float)elapsed / duration);
        }

        public static Vector3 LocalSideOffset(string side, float distance)
        {
            var direction = string.Equals(
                side,
                "port",
                System.StringComparison.OrdinalIgnoreCase)
                ? -1f
                : 1f;
            return Vector3.right * (direction * Mathf.Abs(distance));
        }

        public static bool IsInsideBroadsideArc(
            Vector2 source,
            float headingDegrees,
            Vector2 target,
            string side,
            float halfArcDegrees = 50f)
        {
            var delta = target - source;
            var targetBearing = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            var sideCenter = headingDegrees + (string.Equals(
                side,
                "port",
                System.StringComparison.OrdinalIgnoreCase) ? -90f : 90f);
            return Mathf.Abs(Mathf.DeltaAngle(sideCenter, targetBearing)) <= halfArcDegrees;
        }
    }
}
