using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class SpatialRulesTests
{
    [Fact]
    public void TheChunkGridCoversTheWholeMap()
    {
        Assert.Equal(
            WorldRules.MapSizeSquares,
            SpatialRules.ChunkSizeSquares * SpatialRules.ChunkCountPerAxis);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(49.9f, 0)]
    [InlineData(50f, 1)]
    [InlineData(399.9f, 7)]
    [InlineData(400f, 7)]
    public void AChunkCoordinateNeverLeavesTheGrid(float position, int expected)
    {
        Assert.Equal(expected, SpatialRules.ChunkCoordinate(position));
    }

    [Fact]
    public void AShipSeesEveryChunkItsViewCanReach()
    {
        var bounds = SpatialRules.BoundsAround(200f, 200f, RangeRules.SubscriptionRadiusSquares);

        Assert.Equal(2, bounds.MinX);
        Assert.Equal(5, bounds.MaxX);
        Assert.Equal(2, bounds.MinY);
        Assert.Equal(5, bounds.MaxY);
    }

    [Fact]
    public void SpatialBoundsClampToTheGridAndRejectNonFiniteInput()
    {
        // A radius or a segment span that overshoots the map still clamps to the
        // grid rather than producing an out-of-range chunk index.
        Assert.Equal(new ChunkBounds(0, 7, 0, 7), SpatialRules.BoundsAround(200f, 200f, 5000f));
        Assert.Equal(
            new ChunkBounds(0, 7, 0, 7),
            SpatialRules.BoundsForSegment(-50f, -50f, 450f, 450f, 20f));

        // A course sailed north-west, so both spans run backwards. The bounds are
        // normalized rather than handed over inverted, and this is the only case that
        // says so: every other segment here ascends, and dropping the Min/Max would
        // still clamp to the same grid corners and stay green.
        Assert.Equal(
            new ChunkBounds(1, 6, 1, 6),
            SpatialRules.BoundsForSegment(340f, 340f, 60f, 60f, 0f));

        // Non-finite input is rejected rather than silently clamped. Every Simulation
        // call site -- SailingSystem, RespawnSystem, SimulationTick, WorldSeed -- feeds
        // a raw ship position straight in, and (int)MathF.Floor(NaN) would answer chunk
        // 0 without a word.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpatialRules.BoundsAround(0f, 0f, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpatialRules.ChunkCoordinate(float.PositiveInfinity));
    }
}
