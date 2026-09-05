using System.Collections.Generic;

namespace Sea.Client
{
    /// <summary>
    /// Reading a chunk row's packed ships (SEA_5 §12.1).
    /// </summary>
    /// <remarks>
    /// The mirror of the server's <c>ChunkBlobRules</c>: sixteen bytes a hull, little-endian,
    /// eight for her id, two each for x and y as hundredths of a square, two for her heading in
    /// tenths of a degree, and two spare. The numbers are written out here rather than shared,
    /// because there is nothing to share them through, so <c>SeaChunkBlobTests</c> packs the
    /// bytes by hand: the day the server widens a field this side fails instead of drawing every
    /// ship on the map in the wrong place.
    ///
    /// Nothing is allocated to read a chunk. A crowded chunk is unpacked every tick it changes,
    /// and a reader that allocated per ship would hand the collector a bag of garbage a second.
    /// </remarks>
    public static class SeaChunkBlob
    {
        public const int BytesPerShip = 16;

        private const float PositionScale = 100f;
        private const float HeadingScale = 10f;

        /// <summary>
        /// How many ships can actually be read out of this payload: what the row says is in it,
        /// held down to what arrived. A slot the bytes do not reach would be read as whatever
        /// was left over and drawn as a ship.
        /// </summary>
        public static int Count(IReadOnlyList<byte> payload, int shipCount)
        {
            if (payload == null || shipCount <= 0)
            {
                return 0;
            }

            var whole = payload.Count / BytesPerShip;
            return shipCount < whole ? shipCount : whole;
        }

        public static bool TryUnpack(
            IReadOnlyList<byte> payload,
            int index,
            out ulong entityId,
            out float x,
            out float y,
            out float headingDegrees)
        {
            entityId = 0UL;
            x = 0f;
            y = 0f;
            headingDegrees = 0f;
            var offset = index * BytesPerShip;
            if (payload == null || index < 0 || offset + BytesPerShip > payload.Count)
            {
                return false;
            }

            entityId = ReadEntityId(payload, offset);
            x = ReadUnsigned(payload, offset + 8) / PositionScale;
            y = ReadUnsigned(payload, offset + 10) / PositionScale;
            headingDegrees = ReadUnsigned(payload, offset + 12) / HeadingScale;
            return true;
        }

        private static ulong ReadEntityId(IReadOnlyList<byte> payload, int offset)
        {
            var value = 0UL;
            for (var shift = 0; shift < 64; shift += 8)
            {
                value |= (ulong)payload[offset] << shift;
                offset++;
            }

            return value;
        }

        private static ushort ReadUnsigned(IReadOnlyList<byte> payload, int offset) =>
            (ushort)(payload[offset] | (payload[offset + 1] << 8));
    }
}
