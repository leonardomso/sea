using System.Globalization;

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

/// <summary>
/// The chart ruler: forty columns lettered A..Z, AA..AN, forty rows numbered
/// 1..40, so one ruler cell is ten squares. Columns run east from the left-hand
/// edge and rows run south from the top, which is the same way the map is
/// stored (SEA_5 §3.1) -- there is no Y-flip here, because the map has no
/// north-up centre-origin left to flip against.
/// </summary>
public static class ChartCoordinates
{
    private const int FirstLetterValue = 1;
    public const int ColumnCount = 40;
    public const int RowCount = 40;

    /// <summary>The highest 0-based row index. A parsed 1-based row label is valid from 1 to
    /// <see cref="RowCount"/>, which is this bound plus one.</summary>
    public const int MaximumRow = RowCount - 1;
    public const float CellWidthSquares = WorldRules.MapSizeSquares / ColumnCount;
    public const float CellHeightSquares = WorldRules.MapSizeSquares / RowCount;

    /// <summary>
    /// How many letters a column label may run to, which caps <see cref="ColumnCount"/> at
    /// 26 + 26*26 = 702. Past that <see cref="ColumnLabel"/> would walk off the front of its
    /// buffer and throw an index error rather than say what was actually wrong, so raise this
    /// first if the ruler ever grows.
    /// </summary>
    private const int MaxColumnLetters = 2;

    public static string ColumnLabel(int column)
    {
        if (column < 0 || column >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        var value = column + FirstLetterValue;
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

    public static bool TryColumnIndex(string? label, out int column)
    {
        column = -1;
        if (string.IsNullOrWhiteSpace(label) || label.Length > MaxColumnLetters)
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

    /// <summary>
    /// Parses a label such as "M12": a bijective-base-26 column letter run
    /// immediately followed by a 1-based row number, with no separator. Rejects
    /// anything else, including the old "M 12" space-separated, 0-based form.
    /// </summary>
    public static bool TryCellCenter(string? coordinate, out ChartCell cell)
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

        if (splitIndex == 0 || splitIndex == trimmed.Length)
        {
            return false;
        }

        var letters = trimmed[..splitIndex];
        var digits = trimmed[splitIndex..];
        if (!TryColumnIndex(letters, out var column) ||
            !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var rowNumber) ||
            rowNumber < 1 ||
            rowNumber - 1 > MaximumRow)
        {
            return false;
        }

        cell = CellCenter(column, rowNumber - 1);
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
            WorldRules.MapMin + (column + 0.5f) * CellWidthSquares,
            WorldRules.MapMin + (row + 0.5f) * CellHeightSquares);
    }

    public static string LabelAt(float x, float y)
    {
        if (!WorldRules.IsInsideMap(x, y))
        {
            throw new ArgumentOutOfRangeException(
                WorldRules.IsInsideMap(x, WorldRules.MapMin) ? nameof(y) : nameof(x),
                $"({x}, {y}) is off the map.");
        }

        // The clamps are not defensive. IsInsideMap is inclusive of MapMax, so the far corner
        // is a legal position, and floor(400 / 10) is 40 -- one past the last cell. Each clamp
        // catches exactly that one input per axis. Deleting them as redundant under the guard
        // above breaks the map edge and nothing else, which is the hardest kind to notice.
        var column = Math.Clamp(
            (int)MathF.Floor((x - WorldRules.MapMin) / CellWidthSquares),
            0,
            ColumnCount - 1);
        var row = Math.Clamp(
            (int)MathF.Floor((y - WorldRules.MapMin) / CellHeightSquares),
            0,
            MaximumRow);
        return $"{ColumnLabel(column)}{row + 1}";
    }
}
