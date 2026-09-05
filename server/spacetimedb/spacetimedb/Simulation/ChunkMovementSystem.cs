using Sea.Server;
using SpacetimeDB;

public static partial class Module
{
    // Where every hull on a chart is, one row per chunk (SEA_5 12.1). A client subscribes to
    // these rows instead of to a movement row per ship, so a crowded chunk costs sixteen bytes a
    // hull on the wire and one row change a tick however many of them are under way.
    //
    // The blob is read-modify-write, not rebuilt: the movement shard carries only the hulls that
    // are moving, and a ship dropped from the chunk the tick she came to rest would vanish off
    // every other captain's chart. So a hull is put in when she arrives, moved while she sails,
    // and taken out only when she leaves the chunk, sinks or logs out.
    //
    // Two ways in. The movement phase edits through TickWorld, which holds each chunk it has
    // touched and writes the dirty ones once at the end of the phase; everything else - a ship
    // spawning, respawning, sinking or crossing a border - edits the row directly, because it
    // happens rarely and outside the movement loop. The dispatcher runs every direct writer
    // before the movement phase, so the cache never opens on a row that is about to change
    // underneath it.

    private static ChunkBlob ReadChunkBlob(ReducerContext ctx, uint id) =>
        ctx.Db.ChunkMovement.Id.Find(id) is ChunkMovement row
            ? new ChunkBlob(row.Payload, row.ShipCount)
            : new ChunkBlob(null, 0);

    private static void WriteChunkBlob(ReducerContext ctx, uint id, ChunkBlob blob, ulong tick)
    {
        var row = new ChunkMovement
        {
            Id = id,
            MapId = ChunkBlobRules.MapIdOf(id),
            ChunkX = (byte)ChunkBlobRules.ChunkXOf(id),
            ChunkY = (byte)ChunkBlobRules.ChunkYOf(id),
            ShipCount = (ushort)blob.Count,
            Tick = tick,
            Payload = blob.Payload,
        };
        if (blob.IsStored)
        {
            ctx.Db.ChunkMovement.Id.Update(row);
        }
        else
        {
            ctx.Db.ChunkMovement.Insert(row);
        }

        blob.MarkPublished();
    }

    /// <summary>
    /// Puts a hull where she is in her chunk's row, straight away. For the paths that move a
    /// ship without sailing her: spawning, respawning and arriving on another chart.
    /// </summary>
    private static void EnterChunk(ReducerContext ctx, Ship ship, ulong tick)
    {
        var id = ChunkBlobRules.RowId(ship.MapId, ship.ChunkX, ship.ChunkY);
        var blob = ReadChunkBlob(ctx, id);
        blob.Set(ship.EntityId, ship.PositionX, ship.PositionY, ship.HeadingDegrees);
        if (blob.IsDirty)
        {
            WriteChunkBlob(ctx, id, blob, tick);
        }
    }

    /// <summary>Takes a hull out of a chunk she is no longer in, straight away.</summary>
    private static void LeaveChunk(
        ReducerContext ctx,
        byte mapId,
        int chunkX,
        int chunkY,
        ulong entityId,
        ulong tick)
    {
        var id = ChunkBlobRules.RowId(mapId, chunkX, chunkY);
        if (ctx.Db.ChunkMovement.Id.Find(id) is not ChunkMovement row)
        {
            return;
        }

        var blob = new ChunkBlob(row.Payload, row.ShipCount);
        if (blob.Remove(entityId))
        {
            WriteChunkBlob(ctx, id, blob, tick);
        }
    }

    /// <summary>
    /// Keeps the chunk rows honest about a ship whose movement row has just been rewritten.
    /// </summary>
    /// <remarks>
    /// A hull that is neither active nor alive is taken out and not put back: her wreck is not a
    /// ship anyone should be drawing. She comes back in when she respawns, which rewrites the
    /// same row through the same path.
    /// </remarks>
    private static void SyncChunkMembership(
        ReducerContext ctx,
        Ship ship,
        ShipMovement? previous,
        ulong tick)
    {
        var belongs = ship.IsActive && ship.IsAlive;
        if (previous is ShipMovement was &&
            (!belongs ||
             was.MapId != ship.MapId ||
             was.ChunkX != ship.ChunkX ||
             was.ChunkY != ship.ChunkY))
        {
            LeaveChunk(ctx, was.MapId, was.ChunkX, was.ChunkY, ship.EntityId, tick);
        }

        if (belongs)
        {
            EnterChunk(ctx, ship, tick);
        }
    }
}
