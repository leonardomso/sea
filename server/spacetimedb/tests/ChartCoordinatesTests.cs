using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ChartCoordinatesTests
{
    [Fact]
    public void TheRulerIsFortyByForty()
    {
        Assert.Equal(40, ChartCoordinates.ColumnCount);
        Assert.Equal(40, ChartCoordinates.RowCount);
        Assert.Equal(10f, ChartCoordinates.CellWidthSquares, 4);
        Assert.Equal(10f, ChartCoordinates.CellHeightSquares, 4);
    }

    [Fact]
    public void ATopLeftPositionIsCellAOne()
    {
        Assert.Equal("A1", ChartCoordinates.LabelAt(0.5f, 0.5f));
    }

    [Fact]
    public void ABottomRightPositionIsTheLastCell()
    {
        Assert.Equal("AN40", ChartCoordinates.LabelAt(399.5f, 399.5f));
    }

    [Fact]
    public void ACellCentreRoundTripsBackToItsLabel()
    {
        Assert.True(ChartCoordinates.TryCellCenter("M12", out var cell));

        // The label round trip alone passes with the column and the row swapped, because the
        // ruler is square. Naming both indices is what pins which is which: M is the
        // thirteenth column counting from A at zero, and the rows are labelled from one.
        Assert.Equal(12, cell.Column);
        Assert.Equal(11, cell.Row);
        Assert.Equal(125f, cell.X, 3);
        Assert.Equal(115f, cell.Y, 3);
        Assert.Equal("M12", ChartCoordinates.LabelAt(cell.X, cell.Y));
    }

    // FirstLetterValue moved from 27 to 1, and both ColumnLabel and TryColumnIndex read it.
    // A single-letter column such as "M" cannot reach the bijective-base-26 carry from Z into
    // AA, so this pins the carry explicitly, round-tripping both directions.
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(39, "AN")]
    public void ColumnLabelsCarryPastZAndBackAgain(int column, string label)
    {
        Assert.Equal(label, ChartCoordinates.ColumnLabel(column));
        Assert.True(ChartCoordinates.TryColumnIndex(label, out var parsed));
        Assert.Equal(column, parsed);
    }

    // TryCellCenter's parser was rewritten from splitting on whitespace to splitting letters
    // from digits, so it needs its own negative coverage rather than inheriting the old one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A0")]      // row 0: rows are 1-based, there is no row zero
    [InlineData("AN41")]    // row 41: one past the last row (40)
    [InlineData("AO1")]     // column AO (40): one past the last column (AN, 39)
    [InlineData("12M")]     // digits before letters
    [InlineData("M 12")]    // the old space-separated form is deliberately no longer accepted
    [InlineData("AA")]      // a column with no row at all
    [InlineData("AA nope")]
    [InlineData("AA -1")]
    [InlineData("M12extra")]
    public void InvalidCellCoordinatesAreRejected(string? coordinate)
    {
        Assert.False(ChartCoordinates.TryCellCenter(coordinate, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AO")]      // one past the last column
    [InlineData("AAA")]     // longer than MaxColumnLetters
    [InlineData("A!")]
    [InlineData("ZZ")]      // a legal base-26 run, but off a forty-column ruler
    public void InvalidColumnLabelsAreRejected(string? label)
    {
        Assert.False(ChartCoordinates.TryColumnIndex(label, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ChartCoordinates.ColumnCount)]
    public void ColumnIndexesOffTheRulerAreRejected(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinates.ColumnLabel(column));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartCoordinates.CellCenter(column, 0));
    }

    [Fact]
    public void BothMapCornersCarryALabel()
    {
        // IsInsideMap is inclusive of MapMax, so the far corner is a legal position and
        // floor(400 / 10) is 40 -- one past the last cell. The clamps inside LabelAt exist
        // for exactly these two inputs; without them the far corner throws.
        Assert.Equal("A1", ChartCoordinates.LabelAt(WorldRules.MapMin, WorldRules.MapMin));
        Assert.Equal("AN40", ChartCoordinates.LabelAt(WorldRules.MapMax, WorldRules.MapMax));
    }

    [Fact]
    public void APositionOffTheMapNamesTheAxisThatIsOff()
    {
        Assert.Equal("y", Assert.Throws<ArgumentOutOfRangeException>(
            () => ChartCoordinates.LabelAt(50f, 900f)).ParamName);
        Assert.Equal("x", Assert.Throws<ArgumentOutOfRangeException>(
            () => ChartCoordinates.LabelAt(900f, 50f)).ParamName);
    }

    // The old scheme crossed x/y into row/column and Y-flipped the result. Asserting
    // CellWidthSquares/CellHeightSquares equal 10 (above) does not catch that, because both are
    // 10 whether or not the axes are crossed. These two assert the crossing behaviourally: moving
    // one cell east must move the column and hold the row, and moving one cell south must move
    // the row and hold the column.
    [Fact]
    public void MovingTenSquaresEastAdvancesTheColumnNotTheRow()
    {
        Assert.Equal("A1", ChartCoordinates.LabelAt(5f, 5f));
        Assert.Equal("B1", ChartCoordinates.LabelAt(15f, 5f));
    }

    [Fact]
    public void MovingTenSquaresSouthAdvancesTheRowNotTheColumn()
    {
        Assert.Equal("A1", ChartCoordinates.LabelAt(5f, 5f));
        Assert.Equal("A2", ChartCoordinates.LabelAt(5f, 15f));
    }
}
