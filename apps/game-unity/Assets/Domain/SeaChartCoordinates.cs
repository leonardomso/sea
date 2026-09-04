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

        /// <summary>Zero-based, counted east from the western edge. The spoken label is
        /// this index lettered; the server's ChartCell.Column carries the same number.</summary>
        public int Column { get; }

        /// <summary>Zero-based, counted south from the northern edge. The spoken label is
        /// one more than this, because rows are spoken from one.</summary>
        public int Row { get; }

        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// The chart grid a captain reads. The world is four hundred squares on a side and a
    /// square is one world unit; the ruler groups ten of them to a label, so it is forty cells
    /// on a side and a coordinate spoken on the sea names a cell, not a square. Both are
    /// measured in the same ground the server measures ranges in. The origin is the top-left
    /// corner; x grows east and y grows south, so there is no flip between a ruler row and the
    /// world's own y axis.
    ///
    /// This is the client's copy of the server's <c>ChartCoordinates</c>. It cannot reference
    /// it -- the Unity assembly does not see Sea.Server -- so the two are kept in step by
    /// hand, and <c>SeaClientHotPathTests</c> pins a sample of labels against the strings the
    /// server produces. Change one and change the other. They spoke different languages until
    /// this was written: this side answered "13-12" where the server answered "M12".
    /// </summary>
    public static class SeaChartCoordinates
    {
        public const int ColumnCount = 40;
        public const int RowCount = 40;
        public const float MapMinimum = 0f;
        public const float MapMaximum = 400f;

        // One per axis, derived from that axis's own count. They are both ten while the ruler
        // is square, so nothing here can tell a single shared constant from these two -- which
        // is exactly why the server split them, and why this side follows rather than waiting
        // for a non-square ruler to expose it.
        public const float CellWidthSquares = (MapMaximum - MapMinimum) / ColumnCount;
        public const float CellHeightSquares = (MapMaximum - MapMinimum) / RowCount;

        /// <summary>How many letters a column label may run to. See the server's
        /// ChartCoordinates.MaxColumnLetters; it caps ColumnCount at 702.</summary>
        private const int MaxColumnLetters = 2;

        public static bool TryCellCenter(string coordinate, out SeaChartCell cell)
        {
            cell = default;
            if (string.IsNullOrWhiteSpace(coordinate))
            {
                return false;
            }

            var trimmed = coordinate.Trim();
            var splitIndex = 0;
            while (splitIndex < trimmed.Length && char.IsLetter(trimmed[splitIndex]))
            {
                splitIndex++;
            }

            if (splitIndex == 0 || splitIndex == trimmed.Length ||
                !TryColumnIndex(trimmed[..splitIndex], out var column) ||
                !int.TryParse(
                    trimmed[splitIndex..],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var spokenRow) ||
                spokenRow < 1 || spokenRow > RowCount)
            {
                return false;
            }

            var row = spokenRow - 1;
            cell = new SeaChartCell(
                column,
                row,
                MapMinimum + (column + 0.5f) * CellWidthSquares,
                MapMinimum + (row + 0.5f) * CellHeightSquares);
            return true;
        }

        public static string LabelAt(float x, float y) =>
            $"{ColumnLabelAt(ColumnIndexAt(x))}{RowLabelAt(RowIndexAt(y))}";

        /// <summary>Zero-based column index, counted east from the western edge.</summary>
        public static int ColumnIndexAt(float x) =>
            Math.Clamp((int)Math.Floor((x - MapMinimum) / CellWidthSquares), 0, ColumnCount - 1);

        /// <summary>Zero-based row index, counted south from the northern edge.</summary>
        public static int RowIndexAt(float y) =>
            Math.Clamp((int)Math.Floor((y - MapMinimum) / CellHeightSquares), 0, RowCount - 1);

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

        /// <summary>Bijective base 26: A..Z, then AA..AN. Column 0 is "A".</summary>
        private static bool TryColumnIndex(string label, out int column)
        {
            column = -1;
            if (label.Length > MaxColumnLetters)
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

            column = value - 1;
            return column >= 0 && column < ColumnCount;
        }

        private static string ColumnLabel(int column)
        {
            var value = column + 1;
            Span<char> characters = stackalloc char[MaxColumnLetters];
            var index = characters.Length;
            while (value > 0)
            {
                value--;
                characters[--index] = (char)('A' + value % 26);
                value /= 26;
            }

            return new string(characters[index..]);
        }

        private static string[] BuildColumnLabels()
        {
            var labels = new string[ColumnCount];
            for (var index = 0; index < labels.Length; index++)
            {
                labels[index] = ColumnLabel(index);
            }

            return labels;
        }

        private static string[] BuildRowLabels()
        {
            var labels = new string[RowCount];
            for (var index = 0; index < labels.Length; index++)
            {
                labels[index] = (index + 1).ToString(CultureInfo.InvariantCulture);
            }

            return labels;
        }

        private static readonly string[] ColumnLabels = BuildColumnLabels();
        private static readonly string[] RowLabels = BuildRowLabels();
    }
}
