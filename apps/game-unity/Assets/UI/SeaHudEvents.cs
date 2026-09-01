using UnityEngine;
using Unity.Profiling;

namespace Sea.Client
{
    public sealed partial class SeaHudController
    {
        private static readonly ProfilerMarker HudMarker = new("Sea.UI.Hud");
        private static readonly ProfilerMarker MinimapMarker = new("Sea.UI.MinimapAndRulers");
        private readonly SeaDirtyState hudDirty = new();
        private SeaConnectionController hudConnection;
        private Vector3 previousCameraPosition = new(float.PositiveInfinity, 0f, 0f);
        private float previousCameraSize = float.PositiveInfinity;

        private void BindHudEvents(SeaConnectionController next)
        {
            if (hudConnection == next)
            {
                return;
            }

            if (hudConnection != null)
            {
                hudConnection.HudStateChanged -= HandleHudStateChanged;
            }

            hudConnection = next;
            if (hudConnection != null)
            {
                hudConnection.HudStateChanged += HandleHudStateChanged;
            }
        }

        private void HandleHudStateChanged() => hudDirty.Mark();

        private bool CameraRulersChanged()
        {
            if (chartCamera == null)
            {
                return false;
            }

            var position = chartCamera.transform.position;
            var size = chartCamera.orthographicSize;
            if ((position - previousCameraPosition).sqrMagnitude < 0.0001f &&
                Mathf.Approximately(size, previousCameraSize))
            {
                return false;
            }

            previousCameraPosition = position;
            previousCameraSize = size;
            return true;
        }
    }
}
