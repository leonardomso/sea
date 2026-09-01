using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaHudController
    {
        private void UpdateCoordinateRulers()
        {
            if (chartCamera == null)
            {
                return;
            }

            for (var index = 0; index < topCoordinateLabels.Length; index++)
            {
                var viewportX = 0.04f + 0.74f * index / (topCoordinateLabels.Length - 1);
                if (TryChartPoint(new Vector2(viewportX, 0.96f), out var point))
                {
                    topCoordinateLabels[index].text = SeaChartCoordinates.LabelAt(point.x, point.z)
                        .Split(' ')[1];
                }
            }

            for (var index = 0; index < leftCoordinateLabels.Length; index++)
            {
                var viewportY = 0.16f + 0.76f * index / (leftCoordinateLabels.Length - 1);
                if (TryChartPoint(new Vector2(0.03f, viewportY), out var point))
                {
                    leftCoordinateLabels[leftCoordinateLabels.Length - 1 - index].text =
                        SeaChartCoordinates.LabelAt(point.x, point.z).Split(' ')[0];
                }
            }
        }

        private bool TryChartPoint(Vector2 viewportPosition, out Vector3 point)
        {
            var ray = chartCamera.ViewportPointToRay(viewportPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private static float RemainingSeconds(ulong readyTick, ulong currentTick, uint tickRate) =>
            readyTick <= currentTick ? 0f : (float)(readyTick - currentTick) / tickRate;
    }
}
