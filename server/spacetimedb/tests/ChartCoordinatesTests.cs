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
    [InlineData("")]
    [InlineData("A0")]      // row 0: rows are 1-based, there is no row zero
    [InlineData("AN41")]    // row 41: one past the last row (40)
    [InlineData("AO1")]     // column AO (40): one past the last column (AN, 39)
    [InlineData("12M")]     // digits before letters
    [InlineData("M 12")]    // the old space-separated form is deliberately no longer accepted
    public void InvalidCellCoordinatesAreRejected(string coordinate)
    {
        Assert.False(ChartCoordinates.TryCellCenter(coordinate, out _));
    }

    // Landmine 4: the old scheme crossed x/y into row/column and Y-flipped the result. Asserting
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
