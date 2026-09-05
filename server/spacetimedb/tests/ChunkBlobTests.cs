using System.Collections.Generic;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

/// <summary>
/// The blob a chunk keeps, edited in place. Every hull in the chunk is in it, moving or at
/// rest, because a client draws what the blob says and a ship that dropped out of it when she
/// stopped would vanish off the chart.
/// </summary>
public sealed class ChunkBlobTests
{
    [Fact]
    public void AShipAddedToAnEmptyChunkIsInIt()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(7UL, 12.5f, 30.25f, 90f);

        Assert.Equal(1, blob.Count);
        Assert.True(blob.IsDirty);
        Assert.True(blob.TryRead(7UL, out var x, out var y, out var heading));
        Assert.Equal(12.5f, x, 2);
        Assert.Equal(30.25f, y, 2);
        Assert.Equal(90f, heading, 1);
    }

    [Fact]
    public void AShipAlreadyInTheChunkIsMovedRatherThanAdded()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(7UL, 12.5f, 30.25f, 90f);
        blob.Set(9UL, 1f, 2f, 3f);
        blob.Set(7UL, 40f, 50f, 180f);

        Assert.Equal(2, blob.Count);
        Assert.True(blob.TryRead(7UL, out var x, out _, out _));
        Assert.Equal(40f, x, 2);
    }

    [Fact]
    public void AHullThatHasNotMovedFarEnoughToPackDifferentlyLeavesTheBlobClean()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(7UL, 12.5f, 30.25f, 90f);
        var payload = blob.Payload;
        blob.MarkPublished();

        // A thousandth of a square is under the hundredth the blob keeps, so the bytes are
        // the same bytes and the row must not be rewritten: this is what stops a chunk full
        // of ships holding station from costing a write every tick.
        blob.Set(7UL, 12.5004f, 30.2496f, 90.04f);

        Assert.False(blob.IsDirty);
        Assert.Same(payload, blob.Payload);
    }

    [Fact]
    public void AHullThatMovedAHundredthOfASquareDirtiesTheBlob()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(7UL, 12.5f, 30.25f, 90f);
        blob.MarkPublished();

        blob.Set(7UL, 12.51f, 30.25f, 90f);

        Assert.True(blob.IsDirty);
    }

    [Fact]
    public void AShipThatLeavesIsTakenOutAndTheRestStay()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(1UL, 1f, 1f, 0f);
        blob.Set(2UL, 2f, 2f, 0f);
        blob.Set(3UL, 3f, 3f, 0f);
        blob.MarkPublished();

        Assert.True(blob.Remove(2UL));

        Assert.Equal(2, blob.Count);
        Assert.True(blob.IsDirty);
        Assert.False(blob.TryRead(2UL, out _, out _, out _));
        Assert.True(blob.TryRead(1UL, out _, out _, out _));
        Assert.True(blob.TryRead(3UL, out var x, out _, out _));
        Assert.Equal(3f, x, 2);
    }

    [Fact]
    public void TakingOutAShipThatWasNeverInItChangesNothing()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(1UL, 1f, 1f, 0f);
        blob.MarkPublished();

        Assert.False(blob.Remove(99UL));

        Assert.Equal(1, blob.Count);
        Assert.False(blob.IsDirty);
    }

    [Fact]
    public void TheListIsTrimmedToTheShipsInIt()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        blob.Set(1UL, 1f, 1f, 0f);
        blob.Set(2UL, 2f, 2f, 0f);
        blob.Remove(1UL);

        Assert.Equal(1, blob.Count);
        Assert.Equal(ChunkBlobRules.BytesPerShip, blob.Payload.Count);
    }

    [Fact]
    public void AChunkReadBackOffTheWireIsTheChunkThatWasWritten()
    {
        var written = new ChunkBlob(new List<byte>(), 0);
        for (var index = 0; index < 30; index++)
        {
            written.Set((ulong)index, index * 3f, 400f - index, index * 11f);
        }

        var read = new ChunkBlob(written.Payload, written.Count);

        Assert.Equal(30, read.Count);
        Assert.True(read.TryRead(17UL, out var x, out var y, out var heading));
        Assert.Equal(51f, x, 2);
        Assert.Equal(383f, y, 2);
        Assert.Equal(187f, heading, 1);
    }

    [Fact]
    public void ManyShipsMovingInOneChunkStillCostOneRow()
    {
        var blob = new ChunkBlob(new List<byte>(), 0);
        for (var index = 0; index < 40; index++)
        {
            blob.Set((ulong)index, index, index, 0f);
        }

        blob.MarkPublished();
        for (var index = 0; index < 40; index++)
        {
            blob.Set((ulong)index, index + 1f, index, 0f);
        }

        Assert.True(blob.IsDirty);
        Assert.Equal(40, blob.Count);
        Assert.Equal(40 * ChunkBlobRules.BytesPerShip, blob.Payload.Count);
    }

    [Fact]
    public void AChunkWithNoRowYetKnowsItHasToBeInsertedRatherThanUpdated()
    {
        var fresh = new ChunkBlob(null, 0);
        Assert.False(fresh.IsStored);

        fresh.Set(1, 10f, 10f, 0f);
        fresh.MarkPublished();
        Assert.True(fresh.IsStored);
        Assert.False(fresh.IsDirty);
    }

    [Fact]
    public void AChunkReadBackOffAStoredRowIsNotInsertedAgain()
    {
        Assert.True(new ChunkBlob(new List<byte>(), 0).IsStored);
    }
}
