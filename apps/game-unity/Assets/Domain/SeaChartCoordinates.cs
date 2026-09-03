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

        public int Column { get; }
        public int Row { get; }
        public float X { get; }
        public float Y { get; }
    }

    public static class SeaChartCoordinates
    {
        private const int FirstLetterValue = 27;
        public const int ColumnCount = 78;
        public const int RowCount = 61;
        public const float MapMinimum = -100f;
        public const float MapMaximum = 100f;
        public const float CellWidth = (MapMaximum - MapMinimum) / RowCount;
        public const float CellHeight = (MapMaximum - MapMinimum) / ColumnCount;

        public static bool TryCellCenter(string coordinate, out SeaChartCell cell)
        {
            cell = default;
            if (string.IsNullOrWhiteSpace(coordinate))
            {
                return false;
            }

            var parts = coordinate.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !TryColumn(parts[0], out var column) ||
                !int.TryParse(parts[1], out var row) ||
                row < 0 ||
                row >= RowCount)
            {
                return false;
            }

            cell = new SeaChartCell(
                column,
                row,
                MapMinimum + (row + 0.5f) * CellWidth,
                MapMaximum - (column + 0.5f) * CellHeight);
            return true;
        }

        public static string LabelAt(float x, float y) =>
            $"{ColumnLabelAt(ColumnIndexAt(y))} {RowLabelAt(RowIndexAt(x))}";

        public static int ColumnIndexAt(float y) =>
            Math.Clamp((int)Math.Floor((MapMaximum - y) / CellHeight), 0, ColumnCount - 1);

        public static int RowIndexAt(float x) =>
            Math.Clamp((int)Math.Floor((x - MapMinimum) / CellWidth), 0, RowCount - 1);

        // The chart rulers relabel whenever the camera moves, so the fixed label set is
        // built once and shared instead of being formatted on every frame.
        public static string ColumnLabelAt(int column) => ColumnLabels[column];

        public static string RowLabelAt(int row) => RowLabels[row];

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

        private static bool TryColumn(string label, out int column)
        {
            column = -1;
            if (string.IsNullOrWhiteSpace(label) || label.Length > 2)
            {
                return false;
            }

            var value = 0;
            foreach (var character in label.ToUpperInvariant())
            {
                if (character < 'A' || character > 'Z')
                {
                    return false;
                }

                value = value * 26 + character - 'A' + 1;
            }

            column = value - FirstLetterValue;
            return column >= 0 && column < ColumnCount;
        }

        private static string ColumnLabel(int column)
        {
            var value = column + FirstLetterValue;
            var label = string.Empty;
            while (value > 0)
            {
                value--;
                label = (char)('A' + value % 26) + label;
                value /= 26;
            }

            return label;
        }

        private static string[] BuildColumnLabels()
        {
            var labels = new string[ColumnCount];
            for (var column = 0; column < labels.Length; column++)
            {
                labels[column] = ColumnLabel(column);
            }

            return labels;
        }

        private static string[] BuildRowLabels()
        {
            var labels = new string[RowCount];
            for (var row = 0; row < labels.Length; row++)
            {
                labels[row] = row.ToString(CultureInfo.InvariantCulture);
            }

            return labels;
        }

        private static readonly string[] ColumnLabels = BuildColumnLabels();
        private static readonly string[] RowLabels = BuildRowLabels();
    }
}
