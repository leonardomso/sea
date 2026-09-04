using UnityEngine;
using UnityEngine.UIElements;

namespace Sea.Client
{
    public sealed partial class SeaHudController
    {
        /// <summary>
        /// The rulers carry one slot per square of the map, so a chart pulled all the way out
        /// reads 1 to 20 on both edges. Closer in, several slots fall inside the same square;
        /// only the first of them is written, which puts the number at the square's own edge
        /// instead of repeating it across the view.
        /// </summary>
        private void UpdateCoordinateRulers()
        {
            if (chartCamera == null)
            {
                return;
            }

            var previous = -1;
            for (var index = 0; index < topCoordinateLabels.Length; index++)
            {
                var viewportX = 0.04f + 0.74f * index / (topCoordinateLabels.Length - 1);
                var column = TryChartPoint(new Vector2(viewportX, 0.96f), out var point)
                    ? SeaChartCoordinates.ColumnIndexAt(point.x)
                    : -1;
                Write(topCoordinateLabels[index], SeaChartCoordinates.ColumnLabelAt, column, previous);
                previous = column;
            }

            previous = -1;
            for (var index = 0; index < leftCoordinateLabels.Length; index++)
            {
                var viewportY = 0.92f - 0.76f * index / (leftCoordinateLabels.Length - 1);
                var row = TryChartPoint(new Vector2(0.03f, viewportY), out var point)
                    ? SeaChartCoordinates.RowIndexAt(point.z)
                    : -1;
                Write(leftCoordinateLabels[index], SeaChartCoordinates.RowLabelAt, row, previous);
                previous = row;
            }
        }

        private static void Write(
            Label label,
            System.Func<int, string> labelAt,
            int square,
            int previous)
        {
            if (label == null)
            {
                return;
            }

            label.text = square >= 0 && square != previous ? labelAt(square) : string.Empty;
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
    }
}
