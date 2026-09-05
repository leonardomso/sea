using UnityEngine;

namespace Sea.Client
{
    /// <summary>Turning a route into a line to draw (SEA_5 §4.3).</summary>
    /// <remarks>
    /// A route is already the polyline: there is nothing to smooth, because the ship really
    /// does turn each corner instantly. Drawing a curve here would show a captain a course her
    /// ship is not following.
    ///
    /// The points go through <see cref="SeaChartCoordinates.ToWorld"/> rather than being read
    /// straight into x and z. Chart y grows south and Unity z grows north, so a waypoint is
    /// reflected about the middle of the map exactly as the hull following it is; without the
    /// reflection every drawn course is a mirror image of the one the ship is sailing.
    /// </remarks>
    public static class SeaRouteView
    {
        private static readonly Vector3[] Empty = new Vector3[0];

        public static Vector3[] BuildLine(Vector2[] route, float height)
        {
            if (route == null || route.Length == 0)
            {
                return Empty;
            }

            var points = new Vector3[route.Length];
            for (var index = 0; index < route.Length; index++)
            {
                points[index] = SeaChartCoordinates.ToWorld(route[index].x, route[index].y, height);
            }

            return points;
        }
    }
}
