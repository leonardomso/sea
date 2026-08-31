using System;

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
        public const int ColumnCount = 78;
        public const int RowCount = 61;
        public const float MapMinimum = -100f;
        public const float MapMaximum = 100f;
        public const float CellWidth = (MapMaximum - MapMinimum) / ColumnCount;
        public const float CellHeight = (MapMaximum - MapMinimum) / RowCount;

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
                MapMinimum + (column + 0.5f) * CellWidth,
                MapMinimum + (row + 0.5f) * CellHeight);
            return true;
        }

        public static string LabelAt(float x, float y)
        {
            var column = Math.Clamp(
                (int)Math.Floor((x - MapMinimum) / CellWidth),
                0,
                ColumnCount - 1);
            var row = Math.Clamp(
                (int)Math.Floor((y - MapMinimum) / CellHeight),
                0,
                RowCount - 1);
            return $"{ColumnLabel(column)} {row}";
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

            column = value - 1;
            return column >= 0 && column < ColumnCount;
        }

        private static string ColumnLabel(int column)
        {
            var value = column + 1;
            var label = string.Empty;
            while (value > 0)
            {
                value--;
                label = (char)('A' + value % 26) + label;
                value /= 26;
            }

            return label;
        }
    }
}
