using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Sea.Server;

/// <summary>
/// One chunk's ships, packed, edited in place (SEA_5 §12.1).
/// </summary>
/// <remarks>
/// Every hull in the chunk is in the blob, moving or at rest. A ship dropped from it when she
/// stopped would vanish off every other captain's chart, so a hull leaves only when she leaves
/// the chunk, sinks or logs out.
///
/// The edit is against the row's own <see cref="List{T}"/> through
/// <see cref="CollectionsMarshal.AsSpan{T}"/>, so a chunk that is written back is written back
/// as the list it was read as, with no copy in between. <see cref="IsDirty"/> is set only when
/// the packed bytes actually change: a chunk of ships holding station packs to what it packed
/// last tick and costs no write at all, which is most of what this table is for.
/// </remarks>
public sealed class ChunkBlob
{
    private readonly List<byte> payload;

#pragma warning disable MA0016 // The row's own List<byte> is edited in place; see the remarks.
    public ChunkBlob(List<byte>? payload, int count)
    {
        IsStored = payload is not null;
        this.payload = payload ?? new List<byte>();
        Count = Math.Clamp(count, 0, this.payload.Count / ChunkBlobRules.BytesPerShip);
    }

    /// <summary>The number of slots in <see cref="Payload"/> that are a ship.</summary>
    public int Count { get; private set; }

    /// <summary>Whether anything has changed since the row was last written.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>The row's own list. Only the first <see cref="Count"/> slots are ships.</summary>
    public List<byte> Payload => payload;
#pragma warning restore MA0016

    /// <summary>
    /// Whether the table already holds this chunk's row. A blob built from a row is stored; one
    /// built from nothing is a chunk nobody has ever sailed into, and has to be inserted the
    /// first time it is written rather than updated.
    /// </summary>
    public bool IsStored { get; private set; }

    /// <summary>Called after the row has been written, so the next tick starts clean.</summary>
    public void MarkPublished()
    {
        IsDirty = false;
        IsStored = true;
    }

    /// <summary>
    /// Where a hull is now. She is added if the chunk has not seen her and moved if it has.
    /// </summary>
    public void Set(ulong entityId, float x, float y, float headingDegrees)
    {
        Span<byte> slot = stackalloc byte[ChunkBlobRules.BytesPerShip];
        ChunkBlobRules.Pack(slot, 0, entityId, x, y, headingDegrees);

        var index = IndexOf(entityId);
        if (index < 0)
        {
            index = Count;
            CollectionsMarshal.SetCount(payload, (Count + 1) * ChunkBlobRules.BytesPerShip);
            Count++;
            IsDirty = true;
        }

        var target = SlotAt(index);
        if (slot.SequenceEqual(target))
        {
            return;
        }

        slot.CopyTo(target);
        IsDirty = true;
    }

    /// <summary>
    /// Takes a hull out. The last slot is moved into the hole rather than the tail being
    /// shuffled down: nothing reads the blob in order, so a swap is the whole cost.
    /// </summary>
    public bool Remove(ulong entityId)
    {
        var index = IndexOf(entityId);
        if (index < 0)
        {
            return false;
        }

        var last = Count - 1;
        if (index != last)
        {
            SlotAt(last).CopyTo(SlotAt(index));
        }

        Count = last;
        CollectionsMarshal.SetCount(payload, Count * ChunkBlobRules.BytesPerShip);
        IsDirty = true;
        return true;
    }

    public bool TryRead(ulong entityId, out float x, out float y, out float headingDegrees)
    {
        var index = IndexOf(entityId);
        if (index < 0)
        {
            x = 0f;
            y = 0f;
            headingDegrees = 0f;
            return false;
        }

        ChunkBlobRules.Unpack(CollectionsMarshal.AsSpan(payload), index, out _, out x, out y, out headingDegrees);
        return true;
    }

    private int IndexOf(ulong entityId)
    {
        var slots = CollectionsMarshal.AsSpan(payload);
        for (var index = 0; index < Count; index++)
        {
            if (ChunkBlobRules.EntityIdAt(slots, index) == entityId)
            {
                return index;
            }
        }

        return -1;
    }

    private Span<byte> SlotAt(int index) => CollectionsMarshal
        .AsSpan(payload)
        .Slice(index * ChunkBlobRules.BytesPerShip, ChunkBlobRules.BytesPerShip);
}
