using SpacetimeDB.Types;
using UnityEngine;
using Unity.Profiling;

namespace Sea.Client
{
    public static class SeaMiniMapRules
    {
        public static Vector3 ToWorldPosition(Vector2 normalizedPosition) => new(
            Mathf.Lerp(
                SeaChartCoordinates.MapMinimum,
                SeaChartCoordinates.MapMaximum,
                Mathf.Clamp01(normalizedPosition.x)),
            0f,
            Mathf.Lerp(
                SeaChartCoordinates.MapMaximum,
                SeaChartCoordinates.MapMinimum,
                Mathf.Clamp01(normalizedPosition.y)));

        public static bool TryScreenToWorldPosition(
            Vector2 screenPosition,
            Rect miniMapPixelRect,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (miniMapPixelRect.width <= 0f || miniMapPixelRect.height <= 0f ||
                !miniMapPixelRect.Contains(screenPosition))
            {
                return false;
            }

            worldPosition = ToWorldPosition(new Vector2(
                (screenPosition.x - miniMapPixelRect.x) / miniMapPixelRect.width,
                1f - (screenPosition.y - miniMapPixelRect.y) / miniMapPixelRect.height));
            return true;
        }

        /// <summary>Ground corners of the chart camera footprint, for the minimap marker.</summary>
        public static void ViewportCorners(Vector3 center, Vector2 halfExtents, Vector3[] corners)
        {
            corners[0] = new Vector3(center.x - halfExtents.x, center.y, center.z - halfExtents.y);
            corners[1] = new Vector3(center.x + halfExtents.x, center.y, center.z - halfExtents.y);
            corners[2] = new Vector3(center.x + halfExtents.x, center.y, center.z + halfExtents.y);
            corners[3] = new Vector3(center.x - halfExtents.x, center.y, center.z + halfExtents.y);
        }

        /// <summary>
        /// Converts a UI Toolkit panel rectangle (origin top-left, panel units) into a camera pixel
        /// rectangle (origin bottom-left, screen pixels).
        /// </summary>
        public static Rect ScreenPixelRect(Rect panelBound, float panelHeight, float screenHeight)
        {
            var scale = panelHeight > 0f ? screenHeight / panelHeight : 1f;
            return new Rect(
                panelBound.x * scale,
                screenHeight - (panelBound.y + panelBound.height) * scale,
                panelBound.width * scale,
                panelBound.height * scale);
        }
    }

    /// <summary>
    /// Pure rules for the tilted orthographic chart camera. The visible ground footprint of the
    /// camera is a rectangle of half-extents (zoom * aspect, zoom / sin(tilt)); the view is kept
    /// inside the map so no water past the map edge is ever shown.
    /// </summary>
    public static class SeaChartCameraRules
    {
        public const float DefaultZoom = 45f;
        public const float MinimumZoom = 20f;
        public const float MaximumZoom = 80f;
        public const float TiltDegrees = 55f;

        private const float MapHalfSize =
            (SeaChartCoordinates.MapMaximum - SeaChartCoordinates.MapMinimum) / 2f;

        public static Vector2 ViewHalfExtents(float zoom, float aspect) =>
            new(zoom * aspect, zoom / Mathf.Sin(TiltDegrees * Mathf.Deg2Rad));

        public static float MaximumZoomFor(float aspect) => Mathf.Min(
            MaximumZoom,
            MapHalfSize / Mathf.Max(aspect, 0.01f),
            MapHalfSize * Mathf.Sin(TiltDegrees * Mathf.Deg2Rad));

        public static float ClampZoom(float zoom, float aspect) =>
            Mathf.Clamp(zoom, MinimumZoom, MaximumZoomFor(aspect));

        public static Vector3 PanDelta(
            float horizontal,
            float vertical,
            float unitsPerSecond,
            float deltaSeconds) =>
            new Vector3(horizontal, 0f, vertical) * unitsPerSecond * deltaSeconds;

        public static Vector3 DragDelta(Vector2 screenDelta, float zoom, float pixelHeight)
        {
            var extents = ViewHalfExtents(zoom, 1f) * 2f / Mathf.Max(pixelHeight, 1f);
            return new Vector3(-screenDelta.x * extents.x, 0f, -screenDelta.y * extents.y);
        }

        public static Vector3 ClampCenter(Vector3 center, Vector2 viewHalfExtents) => new(
            ClampAxis(center.x, viewHalfExtents.x),
            center.y,
            ClampAxis(center.z, viewHalfExtents.y));

        private static float ClampAxis(float value, float halfExtent)
        {
            var reach = Mathf.Max(0f, MapHalfSize - halfExtent);
            return Mathf.Clamp(value, -reach, reach);
        }
    }

    public sealed class SeaChartCameraController : MonoBehaviour
    {
        private static readonly ProfilerMarker CameraMarker = new("Sea.Presentation.Camera");

        [SerializeField] private Camera chartCamera;
        [SerializeField] private Camera miniMapCamera;
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private SeaWorldView worldView;
        [SerializeField] private float panSpeed = 45f;
        [SerializeField] private float panSharpness = 10f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private float followSharpness = 6f;

        private readonly SeaChartFollowState follow = new();
        private SeaMiniMapViewportMarker viewportMarker;
        private Vector2 panInput;
        private Vector2 panVelocity;
        private Vector2 dragAnchor;
        private bool isDragging;
        private bool hasInitialCenter;

        public bool IsFollowingPlayer => follow.IsFollowing;
        public Camera MiniMapCamera => miniMapCamera;

        public void Configure(Camera camera, Camera mapCamera = null)
        {
            chartCamera = camera;
            miniMapCamera = mapCamera;
        }

        public void ConfigureDependencies(
            SeaConnectionController connectionController,
            SeaWorldView view)
        {
            connection = connectionController;
            worldView = view;
        }

        private void Awake()
        {
            chartCamera ??= Camera.main;
        }

        // Runs after the world view has placed the ships so the follow never trails a frame.
        private void LateUpdate()
        {
            if (chartCamera == null)
            {
                return;
            }

            using var _ = CameraMarker.Auto();
            var deltaTime = Time.unscaledDeltaTime;
            chartCamera.orthographicSize = SeaChartCameraRules.ClampZoom(
                chartCamera.orthographicSize,
                chartCamera.aspect);

            var hasPlayer = TryGetPlayerPosition(out var playerPosition);
            if (hasPlayer && !hasInitialCenter)
            {
                CenterOn(playerPosition);
                hasInitialCenter = true;
            }

            Pan(deltaTime);
            if (panInput.sqrMagnitude > 0f || isDragging)
            {
                follow.Interrupt();
            }

            if (follow.IsFollowing && hasPlayer)
            {
                SmoothCenterOn(playerPosition, deltaTime);
            }

            UpdateViewportMarker(KeepChartInBounds());
        }

        public void SetPanInput(Vector2 value)
        {
            panInput = Vector2.ClampMagnitude(value, 1f);
            if (panInput.sqrMagnitude > 0f)
            {
                follow.Interrupt();
            }
        }

        public void BeginDrag(Vector2 screenPosition)
        {
            isDragging = true;
            dragAnchor = screenPosition;
            follow.Interrupt();
        }

        public void DragTo(Vector2 screenPosition)
        {
            if (!isDragging || chartCamera == null)
            {
                return;
            }

            chartCamera.transform.position += SeaChartCameraRules.DragDelta(
                screenPosition - dragAnchor,
                chartCamera.orthographicSize,
                chartCamera.pixelHeight);
            dragAnchor = screenPosition;
            KeepChartInBounds();
        }

        public void EndDrag() => isDragging = false;

        public void Zoom(float scroll)
        {
            if (chartCamera != null && !Mathf.Approximately(scroll, 0f))
            {
                chartCamera.orthographicSize = SeaChartCameraRules.ClampZoom(
                    chartCamera.orthographicSize - scroll * zoomSpeed,
                    chartCamera.aspect);
                KeepChartInBounds();
            }
        }

        public void Recenter() => follow.Resume();

        public void ShowChartPosition(Vector3 worldPosition)
        {
            if (chartCamera == null)
            {
                return;
            }

            follow.Interrupt();
            CenterOn(worldPosition);
        }

        public bool TryShowMiniMapPosition(Vector2 screenPosition)
        {
            if (miniMapCamera == null || !SeaMiniMapRules.TryScreenToWorldPosition(
                    screenPosition,
                    miniMapCamera.pixelRect,
                    out var worldPosition))
            {
                return false;
            }

            ShowChartPosition(worldPosition);
            return true;
        }

        private void Pan(float deltaTime)
        {
            var zoomScale = chartCamera.orthographicSize / SeaChartCameraRules.DefaultZoom;
            panVelocity = Vector2.Lerp(
                panVelocity,
                panInput * (panSpeed * zoomScale),
                1f - Mathf.Exp(-panSharpness * deltaTime));
            if (panVelocity.sqrMagnitude < 0.0001f)
            {
                panVelocity = Vector2.zero;
                return;
            }

            chartCamera.transform.position += SeaChartCameraRules.PanDelta(
                panVelocity.x,
                panVelocity.y,
                1f,
                deltaTime);
        }

        private bool TryGetPlayerShip(out Ship ship)
        {
            ship = null;
            if (connection?.Connection == null || !connection.HasIdentity)
            {
                return false;
            }

            var ownership = connection.Connection.Db.PlayerOwnership.Owner
                .Find(connection.LocalIdentity);
            if (ownership == null)
            {
                return false;
            }

            ship = connection.Connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
            return ship != null;
        }

        private bool TryGetPlayerPosition(out Vector3 position)
        {
            if (worldView != null && worldView.TryGetPlayerPresentationPosition(out position))
            {
                position.y = 0f;
                return true;
            }

            if (TryGetPlayerShip(out var ship))
            {
                position = new Vector3(ship.PositionX, 0f, ship.PositionY);
                return true;
            }

            position = default;
            return false;
        }

        private void CenterOn(Vector3 target)
        {
            var delta = CenterDelta(target);
            chartCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
        }

        private void SmoothCenterOn(Vector3 target, float deltaTime)
        {
            var delta = CenterDelta(target);
            var interpolation = 1f - Mathf.Exp(-followSharpness * deltaTime);
            chartCamera.transform.position += new Vector3(delta.x, 0f, delta.z) * interpolation;
        }

        private Vector3 CenterDelta(Vector3 target) =>
            SeaChartCameraRules.ClampCenter(target, ViewHalfExtents()) - CurrentChartCenter();

        private Vector2 ViewHalfExtents() =>
            SeaChartCameraRules.ViewHalfExtents(chartCamera.orthographicSize, chartCamera.aspect);

        // Returns the chart center after the correction so the frame needs no second raycast.
        private Vector3 KeepChartInBounds()
        {
            var center = CurrentChartCenter();
            var delta = SeaChartCameraRules.ClampCenter(center, ViewHalfExtents()) - center;
            if (delta.sqrMagnitude > 0.0001f)
            {
                var correction = new Vector3(delta.x, 0f, delta.z);
                chartCamera.transform.position += correction;
                center += correction;
            }

            return center;
        }

        private void UpdateViewportMarker(Vector3 chartCenter)
        {
            if (miniMapCamera == null)
            {
                return;
            }

            if (viewportMarker == null)
            {
                viewportMarker = new SeaMiniMapViewportMarker();
                chartCamera.cullingMask &= ~(1 << SeaMiniMapViewportMarker.MiniMapOnlyLayer);
            }

            viewportMarker.Show(chartCenter, ViewHalfExtents());
        }

        private Vector3 CurrentChartCenter()
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var centerRay = chartCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            return plane.Raycast(centerRay, out var distance)
                ? centerRay.GetPoint(distance)
                : Vector3.zero;
        }
    }
}
