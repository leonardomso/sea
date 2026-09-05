using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaRouteStep
    {
        public SeaRouteStep(Vector2 position, float headingDegrees, int waypointIndex, bool arrived)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            WaypointIndex = waypointIndex;
            Arrived = arrived;
        }

        public Vector2 Position { get; }

        public float HeadingDegrees { get; }

        /// <summary>Which leg she is on: the index of the waypoint behind her.</summary>
        public int WaypointIndex { get; }

        public bool Arrived { get; }
    }

    /// <summary>
    /// Walking a route, in the client's own terms and in chart squares, which is the ground the
    /// server measures in. This is a deliberate mirror of the server's <c>RouteRules.Advance</c>
    /// and must stay identical to it: anywhere the two disagree the local ship is drawn where
    /// the server will not agree she is, and the correction a captain sees is what reads as the
    /// hull behaving oddly.
    /// </summary>
    /// <remarks>
    /// SEA_5 4.2 says there is no inertia, which is why this is twenty-odd lines where the old
    /// mirror was two hundred. There is no acceleration to match, no braking curve to match and
    /// no turning circle to match, so there is almost nothing left to get wrong. A corner is
    /// turned inside the step that reaches it and no distance is lost doing it, exactly as on
    /// the server: a hull that gave up her leftover travel at every corner would fall a little
    /// further behind on every bend of a long course.
    /// </remarks>
    public static class SeaRouteRules
    {
        /// <summary>Two points closer than this are the same point, and the leg between them is
        /// skipped rather than divided by.</summary>
        private const float SamePointSquares = 0.000001f;

        public static SeaRouteStep Advance(
            Vector2[] route,
            int waypointIndex,
            Vector2 position,
            float headingDegrees,
            float travelDistance)
        {
            var finished = route == null || waypointIndex >= route.Length - 1;
            if (finished || travelDistance <= 0f)
            {
                return new SeaRouteStep(position, headingDegrees, waypointIndex, finished);
            }

            var index = waypointIndex;
            var remaining = travelDistance;
            var heading = headingDegrees;
            while (remaining > 0f && index < route.Length - 1)
            {
                var target = route[index + 1];
                var toTarget = target - position;
                var distance = toTarget.magnitude;
                if (distance * distance <= SamePointSquares)
                {
                    index++;
                    continue;
                }

                heading = SeaGeometry.HeadingTo(position, target, heading);
                if (distance > remaining)
                {
                    position += toTarget * (remaining / distance);
                    return new SeaRouteStep(position, heading, index, false);
                }

                position = target;
                remaining -= distance;
                index++;
            }

            return new SeaRouteStep(position, heading, index, index >= route.Length - 1);
        }
    }
}
