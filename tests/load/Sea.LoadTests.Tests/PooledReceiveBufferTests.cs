using SpacetimeDB;
using Xunit;

namespace Sea.LoadTests.Tests;

public sealed class PooledReceiveBufferTests
{
    [Fact]
    public void NetworkParserDoesNotReserveOneThreadPerConnection()
    {
        Assert.False(ClientResourcePolicy.UsesDedicatedMessageParserThread);
    }

    [Fact]
    public void StartsWithSmallPooledBuffer()
    {
        using var buffer = new PooledReceiveBuffer();

        Assert.InRange(
            buffer.Capacity,
            PooledReceiveBuffer.InitialCapacity,
            PooledReceiveBuffer.RetainedCapacity);
    }

    [Fact]
    public void GrowsOnlyAfterCurrentCapacityIsFilled()
    {
        using var buffer = new PooledReceiveBuffer();
        var initialCapacity = buffer.Capacity;

        buffer.Advance(initialCapacity);

        Assert.True(buffer.EnsureWritableCapacity());
        Assert.True(buffer.Capacity > initialCapacity);
    }

    [Fact]
    public void CompleteMessageCopiesWrittenBytesAndResetsBuffer()
    {
        using var buffer = new PooledReceiveBuffer();
        var segment = buffer.WritableSegment;
        segment.Array![segment.Offset] = 17;
        segment.Array[segment.Offset + 1] = 23;
        buffer.Advance(2);

        var message = buffer.CompleteMessage();

        Assert.Equal(new byte[] { 17, 23 }, message);
        Assert.Equal(0, buffer.WritableSegment.Offset);
    }

    [Fact]
    public void CompleteMessageReleasesOversizedRetainedBuffer()
    {
        using var buffer = new PooledReceiveBuffer();
        while (buffer.Capacity <= PooledReceiveBuffer.RetainedCapacity)
        {
            buffer.Advance(buffer.WritableSegment.Count);
            Assert.True(buffer.EnsureWritableCapacity());
        }

        buffer.CompleteMessage();

        Assert.InRange(
            buffer.Capacity,
            PooledReceiveBuffer.InitialCapacity,
            PooledReceiveBuffer.RetainedCapacity);
    }

    [Fact]
    public void AdvanceRejectsWritesPastAvailableCapacity()
    {
        using var buffer = new PooledReceiveBuffer();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            buffer.Advance(buffer.Capacity + 1));
    }
}
