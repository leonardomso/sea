using System;
using System.Buffers.Binary;

namespace Sea.Server;

/// <summary>
/// Packing the ships in one chunk into a single row (SEA_5 §12.1).
/// </summary>
/// <remarks>
/// Sixteen bytes a hull: eight for her id, two each for x and y as hundredths of a square, two
/// for her heading in tenths of a degree, and two spare for a status word. A hundredth of a
/// square is a tenth of a metre at any scale a captain can see, and a tenth of a degree is far
/// finer than a sprite can be drawn, so nothing is lost that anyone can look at.
///
/// The position range is what makes this simple: four hundred squares at a hundredth is forty
/// thousand, which fits a <see cref="ushort"/> with room to spare. That is a debt to Phase 1,
/// which fixed the map at four hundred squares; a wider one needs a wider slot, and
/// <c>ChunkBlobRulesTests</c> fails the day it is widened without one.
/// </remarks>
public static class ChunkBlobRules
{
    public const int BytesPerShip = 16;

    private const float PositionScale = 100f;
    private const float HeadingScale = 10f;

    public static void Pack(
        Span<byte> buffer,
        int index,
        ulong entityId,
        float x,
        float y,
        float headingDegrees)
    {
        var slot = buffer.Slice(index * BytesPerShip, BytesPerShip);
        BinaryPrimitives.WriteUInt64LittleEndian(slot, entityId);
        BinaryPrimitives.WriteUInt16LittleEndian(slot[8..], PackPosition(x));
        BinaryPrimitives.WriteUInt16LittleEndian(slot[10..], PackPosition(y));
        BinaryPrimitives.WriteUInt16LittleEndian(
            slot[12..],
            (ushort)MathF.Round(GeometryRules.NormalizeAngle(headingDegrees) * HeadingScale));

        // The status word. Nothing writes it yet; it is here so that adding a flag later is a
        // change to one packer rather than a change to the width of every row on the wire.
        BinaryPrimitives.WriteUInt16LittleEndian(slot[14..], 0);
    }

    public static void Unpack(
        ReadOnlySpan<byte> buffer,
        int index,
        out ulong entityId,
        out float x,
        out float y,
        out float headingDegrees)
    {
        var slot = buffer.Slice(index * BytesPerShip, BytesPerShip);
        entityId = BinaryPrimitives.ReadUInt64LittleEndian(slot);
        x = BinaryPrimitives.ReadUInt16LittleEndian(slot[8..]) / PositionScale;
        y = BinaryPrimitives.ReadUInt16LittleEndian(slot[10..]) / PositionScale;
        headingDegrees = BinaryPrimitives.ReadUInt16LittleEndian(slot[12..]) / HeadingScale;
    }

    /// <summary>
    /// The row a chunk owns, worked out rather than handed out. A chunk's row is rewritten
    /// every tick something in it moves, so the writer must reach it by primary key; an
    /// allocated id would mean a lookup by map and chunk first, every tick, for every chunk.
    /// </summary>
    public static uint RowId(byte mapId, int chunkX, int chunkY)
    {
        if (chunkX < 0 || chunkX >= SpatialRules.ChunkCountPerAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkX));
        }

        if (chunkY < 0 || chunkY >= SpatialRules.ChunkCountPerAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkY));
        }

        return ((uint)mapId << 16) | ((uint)chunkX << 8) | (uint)chunkY;
    }

    /// <summary>
    /// Clamped to the chart before it is scaled. A hull the simulation has somehow put off the
    /// map is drawn at the edge she left by, which is wrong but readable; letting the cast wrap
    /// would draw her at the opposite corner, which is wrong and looks like a teleport.
    /// </summary>
    private static ushort PackPosition(float value) =>
        (ushort)MathF.Round(Math.Clamp(value, WorldRules.MapMin, WorldRules.MapMax) * PositionScale);
}
