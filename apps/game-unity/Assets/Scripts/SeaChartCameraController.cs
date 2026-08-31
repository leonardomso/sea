using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaChartCameraRules
    {
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
    }

    public sealed class SeaChartCameraController : MonoBehaviour
    {
        [SerializeField] private Camera chartCamera;
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private float panSpeed = 45f;
        [SerializeField] private float zoomSpeed = 8f;

        private Vector2 panInput;
        private bool centeredOnPlayer;

        private void Awake()
        {
            chartCamera ??= Camera.main;
            connection ??= FindFirstObjectByType<SeaConnectionController>();
        }

        private void Update()
        {
            if (chartCamera == null)
            {
                return;
            }

            if (!centeredOnPlayer && TryGetPlayerShip(out var playerShip))
            {
                CenterOn(new Vector3(playerShip.PositionX, 0f, playerShip.PositionY));
                centeredOnPlayer = true;
            }

            if (panInput.sqrMagnitude > 0f)
            {
                var zoomScale = chartCamera.orthographicSize / 45f;
                chartCamera.transform.position += SeaChartCameraRules.PanDelta(
                    panInput.x,
                    panInput.y,
                    panSpeed * zoomScale,
                    Time.unscaledDeltaTime);
            }
        }

        public void SetPanInput(Vector2 value)
        {
            panInput = Vector2.ClampMagnitude(value, 1f);
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
            if (TryGetPlayerShip(out var ship))
            {
                CenterOn(new Vector3(ship.PositionX, 0f, ship.PositionY));
                centeredOnPlayer = true;
            }
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

        private void CenterOn(Vector3 target)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var centerRay = chartCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (!plane.Raycast(centerRay, out var distance))
            {
                return;
            }

            var currentCenter = centerRay.GetPoint(distance);
            var delta = target - currentCenter;
            chartCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
        }
    }
}
