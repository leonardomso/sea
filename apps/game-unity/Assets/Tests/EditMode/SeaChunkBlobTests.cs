using System.Collections.Generic;
using NUnit.Framework;
using Sea.Client;

public sealed class SeaChunkBlobTests
{
    [Test]
    public void AnEmptyChunkHasNoShipsInIt()
    {
        Assert.That(SeaChunkBlob.Count(new List<byte>(), 0), Is.EqualTo(0));
        Assert.That(SeaChunkBlob.Count(null, 7), Is.EqualTo(0));
    }

    [Test]
    public void AShipComesBackOutOfTheBytesTheServerPacked()
    {
        var payload = Packed((42UL, 123.45f, 67.89f, 271.3f));

        Assert.That(SeaChunkBlob.Count(payload, 1), Is.EqualTo(1));
        Assert.That(
            SeaChunkBlob.TryUnpack(payload, 0, out var entityId, out var x, out var y, out var heading),
            Is.True);
        Assert.That(entityId, Is.EqualTo(42UL));
        Assert.That(x, Is.EqualTo(123.45f).Within(0.005f));
        Assert.That(y, Is.EqualTo(67.89f).Within(0.005f));
        Assert.That(heading, Is.EqualTo(271.3f).Within(0.05f));
    }

    [Test]
    public void EachShipKeepsHerOwnSlot()
    {
        var payload = Packed(
            (1UL, 10f, 20f, 0f),
            (2UL, 30f, 40f, 90f),
            (3UL, 399.99f, 0.01f, 359.9f));

        Assert.That(SeaChunkBlob.Count(payload, 3), Is.EqualTo(3));
        SeaChunkBlob.TryUnpack(payload, 2, out var entityId, out var x, out var y, out var heading);
        Assert.That(entityId, Is.EqualTo(3UL));
        Assert.That(x, Is.EqualTo(399.99f).Within(0.005f));
        Assert.That(y, Is.EqualTo(0.01f).Within(0.005f));
        Assert.That(heading, Is.EqualTo(359.9f).Within(0.05f));
    }

    // The count the row carries is what the server says is in it; a payload that arrived short
    // is trusted only as far as it actually reaches, because reading past it would be a slot of
    // whatever the last chunk left behind drawn as a ship.
    [Test]
    public void AShortPayloadIsTrustedOnlyAsFarAsItReaches()
    {
        var payload = Packed((1UL, 10f, 20f, 0f));
        payload.RemoveAt(payload.Count - 1);

        Assert.That(SeaChunkBlob.Count(payload, 1), Is.EqualTo(0));
        Assert.That(
            SeaChunkBlob.TryUnpack(payload, 0, out _, out _, out _, out _),
            Is.False);
    }

    [Test]
    public void ASlotPastTheEndIsNotAShip()
    {
        var payload = Packed((1UL, 10f, 20f, 0f));

        Assert.That(SeaChunkBlob.TryUnpack(payload, 1, out _, out _, out _, out _), Is.False);
        Assert.That(SeaChunkBlob.TryUnpack(payload, -1, out _, out _, out _, out _), Is.False);
    }

    // The server packs with ChunkBlobRules; this is the same sixteen bytes written out by hand,
    // so the day one side changes width the other side fails rather than drawing rubbish.
    private static List<byte> Packed(params (ulong EntityId, float X, float Y, float Heading)[] ships)
    {
        var payload = new List<byte>(ships.Length * SeaChunkBlob.BytesPerShip);
        foreach (var ship in ships)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                payload.Add((byte)(ship.EntityId >> shift));
            }

            AddUnsigned(payload, (ushort)System.Math.Round(ship.X * 100f));
            AddUnsigned(payload, (ushort)System.Math.Round(ship.Y * 100f));
            AddUnsigned(payload, (ushort)System.Math.Round(ship.Heading * 10f));
            AddUnsigned(payload, 0);
        }

        return payload;
    }

    private static void AddUnsigned(List<byte> payload, ushort value)
    {
        payload.Add((byte)value);
        payload.Add((byte)(value >> 8));
    }
}
