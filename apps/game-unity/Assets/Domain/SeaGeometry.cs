using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// The client's copy of the compass the whole game reckons in: zero is north, ninety is
    /// east, and a bearing grows clockwise. Chart y grows south, so north is the smaller y and
    /// every bearing here is read with the y term negated.
    /// </summary>
    /// <remarks>
    /// This mirrors <c>GeometryRules</c> on the server and must not drift from it. The Unity
    /// assembly cannot reference the module, so the two are kept in step by hand and by
    /// <c>SeaRouteRulesTests</c>, which asserts the same numbers the server's own tests do.
    /// <para>
    /// A drawn ship needs no correction to match: <c>SeaChartCoordinates.ToWorld</c> puts north
    /// at the larger world z, Unity's yaw zero points at +z, and yaw grows clockwise, so a
    /// bearing is already a yaw.
    /// </para>
    /// </remarks>
    public static class SeaGeometry
    {
        /// <summary>
        /// The bearing from one point to another, with the caller's own answer for when the two
        /// are the same point. The fallback is required rather than defaulted for the reason
        /// <c>GeometryRules.HeadingTo</c> gives on the server: there is no sensible bearing to
        /// nowhere, and a silent zero would swing a stationary hull round to face north.
        /// </summary>
        public static float HeadingTo(Vector2 from, Vector2 to, float fallbackDegrees)
        {
            var delta = to - from;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return NormalizeAngle(fallbackDegrees);
            }

            return NormalizeAngle(Mathf.Atan2(delta.x, 0f - delta.y) * Mathf.Rad2Deg);
        }

        /// <summary>A bearing read back into zero up to but not including three hundred and
        /// sixty, whichever way round it arrived.</summary>
        public static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
