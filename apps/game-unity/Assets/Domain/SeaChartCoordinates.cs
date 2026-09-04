using System;
using System.Globalization;
using UnityEngine;

namespace Sea.Client
{
    public readonly struct SeaChartCell
    {
        public SeaChartCell(int column, int row, float x, float y)
        {
            Column = column;
            Row = row;
            X = x;
            Y = y;
        }

        /// <summary>One-based, counted east from the western edge.</summary>
        public int Column { get; }

        /// <summary>One-based, counted south from the northern edge.</summary>
        public int Row { get; }

        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// The chart grid a captain reads. It is the map's own square grid and nothing else: the
    /// world is four hundred squares on a side and a square is one world unit, grouped ten
    /// squares to a ruler label, so a coordinate spoken on the sea means the same ground the
    /// server measures ranges in. The origin is the top-left corner; x grows east and y grows
    /// south, so there is no flip between a ruler row and the world's own y axis.
    /// </summary>
    public static class SeaChartCoordinates
    {
        public const int ColumnCount = 40;
        public const int RowCount = 40;
        public const float MapMinimum = 0f;
        public const float MapMaximum = 400f;
        public const float SquareSize = (MapMaximum - MapMinimum) / ColumnCount;

        private static readonly char[] Separators = { ' ', '-', ',', ':' };

        public static bool TryCellCenter(string coordinate, out SeaChartCell cell)
        {
            cell = default;
            if (string.IsNullOrWhiteSpace(coordinate))
            {
                return false;
            }

            var parts = coordinate.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !TryIndex(parts[0], ColumnCount, out var column) ||
                !TryIndex(parts[1], RowCount, out var row))
            {
                return false;
            }

            cell = new SeaChartCell(
                column + 1,
                row + 1,
                MapMinimum + (column + 0.5f) * SquareSize,
                MapMinimum + (row + 0.5f) * SquareSize);
            return true;
        }

        public static string LabelAt(float x, float y) =>
            $"{ColumnLabelAt(ColumnIndexAt(x))}-{RowLabelAt(RowIndexAt(y))}";

        /// <summary>Zero-based column index, counted east from the western edge.</summary>
        public static int ColumnIndexAt(float x) =>
            Math.Clamp((int)Math.Floor((x - MapMinimum) / SquareSize), 0, ColumnCount - 1);

        /// <summary>Zero-based row index, counted south from the northern edge.</summary>
        public static int RowIndexAt(float y) =>
            Math.Clamp((int)Math.Floor((y - MapMinimum) / SquareSize), 0, RowCount - 1);

        // The chart rulers relabel whenever the camera moves, so the fixed label set is
        // built once and shared instead of being formatted on every frame.
        public static string ColumnLabelAt(int column) => Labels[column];

        public static string RowLabelAt(int row) => Labels[row];

        public static Vector2 ClampToMap(Vector2 position) => new(
            Mathf.Clamp(position.x, MapMinimum, MapMaximum),
            Mathf.Clamp(position.y, MapMinimum, MapMaximum));

        public static bool IsBlockedDestination(
            Vector2 position,
            Vector2 blockerCenter,
            float blockerRadius)
        {
            var radius = blockerRadius + 0.5f;
            return (position - blockerCenter).sqrMagnitude <= radius * radius;
        }

        private static bool TryIndex(string label, int count, out int index)
        {
            index = -1;
            if (!int.TryParse(
                    label,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var spoken) ||
                spoken < 1 ||
                spoken > count)
            {
                return false;
            }

            index = spoken - 1;
            return true;
        }

        private static string[] BuildLabels()
        {
            var labels = new string[Math.Max(ColumnCount, RowCount)];
            for (var index = 0; index < labels.Length; index++)
            {
                labels[index] = (index + 1).ToString(CultureInfo.InvariantCulture);
            }

            return labels;
        }

        private static readonly string[] Labels = BuildLabels();
    }
}
