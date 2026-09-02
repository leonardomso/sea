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
    }

    public static class SeaChartCameraRules
    {
        public const float DefaultZoom = 45f;
        public const float MinimumZoom = 20f;
        public const float MaximumZoom = 80f;

        public static float ClampZoom(float zoom) =>
            Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);

        public static Vector3 PanDelta(
            float horizontal,
            float vertical,
            float unitsPerSecond,
            float deltaSeconds) =>
            new Vector3(horizontal, 0f, vertical) * unitsPerSecond * deltaSeconds;

        public static Vector3 ClampCenter(Vector3 center) => new(
            Mathf.Clamp(center.x, SeaChartCoordinates.MapMinimum, SeaChartCoordinates.MapMaximum),
            center.y,
            Mathf.Clamp(center.z, SeaChartCoordinates.MapMinimum, SeaChartCoordinates.MapMaximum));
    }

    public sealed class SeaChartCameraController : MonoBehaviour
    {
        private static readonly ProfilerMarker CameraMarker = new("Sea.Presentation.Camera");

        [SerializeField] private Camera chartCamera;
        [SerializeField] private Camera miniMapCamera;
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private SeaWorldView worldView;
        [SerializeField] private float panSpeed = 45f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private float followSharpness = 12f;

        private Vector2 panInput;
        private bool hasInitialCenter;
        private bool isFollowingPlayer = true;

        public bool IsFollowingPlayer => isFollowingPlayer;
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

        private void Update()
        {
            if (chartCamera == null)
            {
                return;
            }

            using var _ = CameraMarker.Auto();
            if (TryGetPlayerPosition(out var playerPosition))
            {
                if (!hasInitialCenter)
                {
                    CenterOn(playerPosition);
                    hasInitialCenter = true;
                }
                else if (isFollowingPlayer)
                {
                    SmoothCenterOn(playerPosition, Time.unscaledDeltaTime);
                }
            }

            if (panInput.sqrMagnitude > 0f)
            {
                var zoomScale = chartCamera.orthographicSize / SeaChartCameraRules.DefaultZoom;
                chartCamera.transform.position += SeaChartCameraRules.PanDelta(
                    panInput.x,
                    panInput.y,
                    panSpeed * zoomScale,
                    Time.unscaledDeltaTime);
                KeepChartInBounds();
            }
        }

        public void SetPanInput(Vector2 value)
        {
            panInput = Vector2.ClampMagnitude(value, 1f);
            if (panInput.sqrMagnitude > 0f)
            {
                isFollowingPlayer = false;
            }
        }

        public void Zoom(float scroll)
        {
            if (chartCamera != null && !Mathf.Approximately(scroll, 0f))
            {
                chartCamera.orthographicSize = SeaChartCameraRules.ClampZoom(
                    chartCamera.orthographicSize - scroll * zoomSpeed);
            }
        }

        public void Recenter()
        {
            isFollowingPlayer = true;
        }

        public void ShowChartPosition(Vector3 worldPosition)
        {
            if (chartCamera == null)
            {
                return;
            }

            isFollowingPlayer = false;
            CenterOn(worldPosition);
            KeepChartInBounds();
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

        private Vector3 CenterDelta(Vector3 target)
        {
            target = SeaChartCameraRules.ClampCenter(target);
            var plane = new Plane(Vector3.up, Vector3.zero);
            var centerRay = chartCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (!plane.Raycast(centerRay, out var distance))
            {
                return Vector3.zero;
            }

            var currentCenter = centerRay.GetPoint(distance);
            return target - currentCenter;
        }

        private void KeepChartInBounds()
        {
            var delta = CenterDelta(CurrentChartCenter());
            if (delta.sqrMagnitude > 0.0001f)
            {
                chartCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
            }
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
