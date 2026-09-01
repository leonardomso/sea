namespace Sea.Server;

public readonly struct ChartCell
{
    public ChartCell(int column, int row, float x, float y)
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

public static class ChartCoordinates
{
    private const int FirstLetterValue = 27;
    public const int ColumnCount = 78;
    public const int RowCount = 61;
    public const int MaximumRow = RowCount - 1;
    public const float CellWidth = (WorldRules.MapMax - WorldRules.MapMin) / RowCount;
    public const float CellHeight = (WorldRules.MapMax - WorldRules.MapMin) / ColumnCount;

    public static string ColumnLabel(int column)
    {
        if (column < 0 || column >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        var value = column + FirstLetterValue;
        Span<char> characters = stackalloc char[2];
        var index = characters.Length;
        while (value > 0)
        {
            value--;
            characters[--index] = (char)('A' + value % 26);
            value /= 26;
        }

        return new string(characters[index..]);
    }

    public static bool TryColumnIndex(string? label, out int column)
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

            value = checked(value * 26 + character - 'A' + 1);
        }

        column = value - FirstLetterValue;
        return column >= 0 && column < ColumnCount;
    }

    public static bool TryCellCenter(string? coordinate, out ChartCell cell)
    {
        cell = default;
        if (string.IsNullOrWhiteSpace(coordinate))
        {
            return false;
        }

        var parts = coordinate.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryColumnIndex(parts[0], out var column) ||
            !int.TryParse(parts[1], out var row) ||
            row < 0 ||
            row > MaximumRow)
        {
            return false;
        }

        cell = CellCenter(column, row);
        return true;
    }

    public static ChartCell CellCenter(int column, int row)
    {
        if (column < 0 || column >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (row < 0 || row > MaximumRow)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        return new ChartCell(
            column,
            row,
            WorldRules.MapMin + (row + 0.5f) * CellWidth,
            WorldRules.MapMax - (column + 0.5f) * CellHeight);
    }

    public static string LabelAt(float x, float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        var column = Math.Clamp(
            (int)MathF.Floor((WorldRules.MapMax - y) / CellHeight),
            0,
            ColumnCount - 1);
        var row = Math.Clamp(
            (int)MathF.Floor((x - WorldRules.MapMin) / CellWidth),
            0,
            MaximumRow);
        return $"{ColumnLabel(column)} {row}";
    }
}
