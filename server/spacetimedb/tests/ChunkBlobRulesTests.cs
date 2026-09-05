using System;
using System.Collections.Generic;
using Sea.Server;
using Xunit;

namespace Sea.Server.Tests;

public sealed class ChunkBlobRulesTests
{
    [Fact]
    public void AShipPacksIntoSixteenBytes()
    {
        Assert.Equal(16, ChunkBlobRules.BytesPerShip);
    }

    [Fact]
    public void WhatIsPackedComesBackOut()
    {
        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, entityId: 4242UL, x: 123.5f, y: 76.25f, headingDegrees: 47f);

        ChunkBlobRules.Unpack(buffer, 0, out var entityId, out var x, out var y, out var heading);

        Assert.Equal(4242UL, entityId);
        Assert.Equal(123.5f, x, 2);
        Assert.Equal(76.25f, y, 2);
        Assert.Equal(47f, heading, 1);
    }

    [Fact]
    public void PositionKeepsAHundredthOfASquare()
    {
        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, 1UL, 399.99f, 0.01f, 359.9f);
        ChunkBlobRules.Unpack(buffer, 0, out _, out var x, out var y, out var heading);

        Assert.Equal(399.99f, x, 2);
        Assert.Equal(0.01f, y, 2);
        Assert.Equal(359.9f, heading, 1);
    }

    [Fact]
    public void TheWholeMapFitsTheSlotAtThatPrecision()
    {
        // A ushort holds 65,535 and the far corner packs to 40,000. This is the assumption the
        // whole scheme rests on, so it is pinned rather than left in a comment: a wider map
        // would wrap the corner round to the origin without any other test noticing.
        Assert.True(WorldRules.MapMax * 100f <= ushort.MaxValue);

        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, 1UL, WorldRules.MapMax, WorldRules.MapMax, 0f);
        ChunkBlobRules.Unpack(buffer, 0, out _, out var x, out var y, out _);

        Assert.Equal(WorldRules.MapMax, x, 2);
        Assert.Equal(WorldRules.MapMax, y, 2);
    }

    [Fact]
    public void APositionOffTheChartIsHeldAtTheEdgeRatherThanWrapped()
    {
        var buffer = new byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, 1UL, 900f, -40f, 0f);
        ChunkBlobRules.Unpack(buffer, 0, out _, out var x, out var y, out _);

        Assert.Equal(WorldRules.MapMax, x, 2);
        Assert.Equal(WorldRules.MapMin, y, 2);
    }

    [Fact]
    public void EachShipKeepsHerOwnSlot()
    {
        var buffer = new byte[3 * ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(buffer, 0, 7UL, 1f, 2f, 3f);
        ChunkBlobRules.Pack(buffer, 2, 9UL, 4f, 5f, 6f);

        ChunkBlobRules.Unpack(buffer, 0, out var first, out _, out _, out _);
        ChunkBlobRules.Unpack(buffer, 2, out var third, out var x, out _, out _);

        Assert.Equal(7UL, first);
        Assert.Equal(9UL, third);
        Assert.Equal(4f, x, 2);
    }

    [Fact]
    public void AChunkHasOneRowIdAndNoTwoChunksShareIt()
    {
        var seen = new HashSet<uint>();
        for (var mapId = 0; mapId < 4; mapId++)
        {
            for (var chunkX = 0; chunkX < SpatialRules.ChunkCountPerAxis; chunkX++)
            {
                for (var chunkY = 0; chunkY < SpatialRules.ChunkCountPerAxis; chunkY++)
                {
                    Assert.True(seen.Add(ChunkBlobRules.RowId((byte)mapId, chunkX, chunkY)));
                }
            }
        }

        Assert.Equal(4 * 64, seen.Count);
        Assert.Equal(ChunkBlobRules.RowId(1, 4, 6), ChunkBlobRules.RowId(1, 4, 6));
    }

    [Fact]
    public void AFullChunkOfShipsPacksAndUnpacksWithNoAllocation()
    {
        var buffer = new byte[64 * ChunkBlobRules.BytesPerShip];
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 64; index++)
        {
            ChunkBlobRules.Pack(buffer, index, (ulong)index, index, index, index);
            ChunkBlobRules.Unpack(buffer, index, out _, out _, out _, out _);
        }

        Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ARowIdSaysWhichChunkItIs()
    {
        // The writer reaches a chunk by key; the flush at the end of the tick has only the key
        // and has to write the map and chunk columns back out of it.
        var id = ChunkBlobRules.RowId(3, 5, 7);
        Assert.Equal(3, ChunkBlobRules.MapIdOf(id));
        Assert.Equal(5, ChunkBlobRules.ChunkXOf(id));
        Assert.Equal(7, ChunkBlobRules.ChunkYOf(id));
    }

    [Fact]
    public void EveryChunkOnEveryMapComesBackOutOfItsOwnRowId()
    {
        for (byte mapId = 0; mapId < 4; mapId++)
        {
            for (var chunkX = 0; chunkX < SpatialRules.ChunkCountPerAxis; chunkX++)
            {
                for (var chunkY = 0; chunkY < SpatialRules.ChunkCountPerAxis; chunkY++)
                {
                    var id = ChunkBlobRules.RowId(mapId, chunkX, chunkY);
                    Assert.Equal(mapId, ChunkBlobRules.MapIdOf(id));
                    Assert.Equal(chunkX, ChunkBlobRules.ChunkXOf(id));
                    Assert.Equal(chunkY, ChunkBlobRules.ChunkYOf(id));
                }
            }
        }
    }
}
