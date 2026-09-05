using UnityEngine;
using SpacetimeDB.Types;

namespace Sea.Client
{
    /// <summary>
    /// The course lines drawn over the water. The player's own is always drawn and one other
    /// ship's is drawn while she is selected, so a crowded chart is a chart and not a cobweb.
    /// </summary>
    /// <remarks>
    /// Two renderers rather than a pool keyed by ship: the rule above caps the drawn courses at
    /// two, so a pool would be a dictionary that never held a third entry.
    ///
    /// A route changes only when the server issues a new one, so the points are rebuilt on a
    /// change of version and not on a change of frame. <c>DrawnNothing</c> stands for "no course
    /// at all", which version zero does not: a fresh route really is version zero.
    /// </remarks>
    public sealed partial class SeaWorldView
    {
        private const long DrawnNothing = -1L;

        // Just clear of the water and just under the selection rings, which sit at
        // WaterSurfaceHeight + 0.025: a course is painted on the sea, not over the disc that
        // marks the hull sailing it.
        private const float RouteLineHeight = WaterSurfaceHeight + 0.012f;
        private const float RouteLineWidth = 0.12f;

        private static readonly Color LocalRouteColor = new(0.2f, 0.9f, 0.35f, 0.5f);
        private static readonly Color FocusRouteColor = new(1f, 0.85f, 0.25f, 0.38f);

        private LineRenderer localRouteLine;
        private LineRenderer focusRouteLine;
        private long localRouteVersion = DrawnNothing;
        private long focusRouteVersion = DrawnNothing;
        private ulong focusRouteEntityId;

        private void UpdateRouteLines()
        {
            localRouteVersion = DrawRoute(
                ref localRouteLine,
                "Local Course Line",
                LocalRouteColor,
                playerEntityId,
                localRouteVersion);

            // Only a ship that is both selected and actually on the chart. A target that has
            // sailed out of vision has no presentation to read a course beside.
            var focusEntityId =
                localShip != null && localShip.TargetEntityId != 0 &&
                entities.ContainsKey(localShip.TargetEntityId)
                    ? localShip.TargetEntityId
                    : 0UL;
            if (focusEntityId != focusRouteEntityId)
            {
                focusRouteEntityId = focusEntityId;
                focusRouteVersion = DrawnNothing - 1L;
            }

            focusRouteVersion = DrawRoute(
                ref focusRouteLine,
                "Selected Course Line",
                FocusRouteColor,
                focusEntityId,
                focusRouteVersion);
        }

        private long DrawRoute(
            ref LineRenderer line,
            string name,
            Color color,
            ulong entityId,
            long drawnVersion)
        {
            var route = FindRoute(entityId);
            var version = route == null ? DrawnNothing : route.Version;
            if (version == drawnVersion || (version == DrawnNothing && line == null))
            {
                return version;
            }

            line ??= CreateRouteLine(name, color);
            var points = SeaRouteView.BuildLine(ToChartRoute(route), RouteLineHeight);

            // One point is not a line: a ship on her last waypoint has arrived, and a mark left
            // under her bow would read as a course she is still to sail.
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.gameObject.SetActive(points.Length > 1);
            return version;
        }

        private ShipRoute FindRoute(ulong entityId) =>
            entityId == 0 || connection == null
                ? null
                : connection.Connection?.Db.ShipRoute.EntityId.Find(entityId);

        private static LineRenderer CreateRouteLine(string name, Color color)
        {
            var lineObject = new GameObject(name);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = SeaMaterialFactory.CreateTransparent(color);
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 0;
            line.startWidth = RouteLineWidth;
            line.endWidth = RouteLineWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lineObject.SetActive(false);
            return line;
        }
    }
}
