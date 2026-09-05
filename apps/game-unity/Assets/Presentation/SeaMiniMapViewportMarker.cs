using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// Outline of the main chart camera's footprint, drawn only on the minimap so the player
    /// can see which part of the map the chart is showing.
    /// </summary>
    public sealed class SeaMiniMapViewportMarker
    {
        public const int MiniMapOnlyLayer = 9;
        private const float Height = 14f;
        private const float LineWidth = 2.4f;
        private static readonly Color OutlineColor = new(0.96f, 0.84f, 0.52f, 0.9f);

        private readonly LineRenderer outline;
        private readonly Vector3[] corners = new Vector3[4];
        private bool shown;
        private Vector3 shownCenter;
        private Vector2 shownHalfExtents;

        public SeaMiniMapViewportMarker()
        {
            var marker = new GameObject("Chart Viewport") { layer = MiniMapOnlyLayer };
            outline = marker.AddComponent<LineRenderer>();
            outline.sharedMaterial = SeaMaterialFactory.CreateTransparent(OutlineColor);
            outline.loop = true;
            outline.positionCount = corners.Length;
            outline.startWidth = LineWidth;
            outline.endWidth = LineWidth;
            outline.useWorldSpace = true;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Redraws the outline; false when the footprint has not moved.</summary>
        public bool Show(Vector3 center, Vector2 halfExtents)
        {
            if (shown && center == shownCenter && halfExtents == shownHalfExtents)
            {
                return false;
            }

            shown = true;
            shownCenter = center;
            shownHalfExtents = halfExtents;
            SeaMiniMapRules.ViewportCorners(new Vector3(center.x, Height, center.z), halfExtents, corners);
            outline.SetPositions(corners);
            return true;
        }
    }
}
