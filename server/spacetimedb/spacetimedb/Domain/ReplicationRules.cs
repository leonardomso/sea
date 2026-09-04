namespace Sea.Server;

/// <summary>
/// When a sailing ship is worth putting on the wire. A client draws a ship it has not heard
/// from in a while by carrying her on at the speed and bearing her last two snapshots imply,
/// so a ship holding her course is already drawn where she is and another row would say
/// nothing new. A snapshot goes out when that reckoning has drifted far enough to see, and
/// on a slow heartbeat besides, so no row is ever stale enough to look abandoned.
/// </summary>
public static class ReplicationRules
{
    // A tenth of a square: a fraction of a hull, and under what the eye picks out on the
    // chart at any zoom a player has.
    public const float PositionToleranceUnits = 1f;
    public const float HeadingToleranceDegrees = 2f;
    public const ulong HeartbeatTicks = 10;

    public static bool ShouldPublish(
        PublishedMotion published,
        float positionX,
        float positionY,
        float headingDegrees,
        ulong tick)
    {
        if (published.Tick == 0 || tick <= published.Tick ||
            tick - published.Tick >= HeartbeatTicks)
        {
            return true;
        }

        var elapsed = (float)(tick - published.Tick);
        var driftX = positionX - (published.PositionX + published.VelocityX * elapsed);
        var driftY = positionY - (published.PositionY + published.VelocityY * elapsed);
        return driftX * driftX + driftY * driftY >
                PositionToleranceUnits * PositionToleranceUnits ||
            MathF.Abs(HeadingDelta(headingDegrees, published.HeadingDegrees)) >
                HeadingToleranceDegrees;
    }

    /// <summary>
    /// The reckoning a client will do from a snapshot taken now: the velocity it infers from
    /// this row and the one before it, which is what the drift above is measured against.
    /// </summary>
    public static PublishedMotion Publish(
        PublishedMotion published,
        float positionX,
        float positionY,
        float headingDegrees,
        ulong tick)
    {
        var elapsed = published.Tick == 0 || tick <= published.Tick
            ? 0f
            : tick - published.Tick;
        return new PublishedMotion
        {
            Tick = tick,
            PositionX = positionX,
            PositionY = positionY,
            HeadingDegrees = headingDegrees,
            VelocityX = elapsed > 0f ? (positionX - published.PositionX) / elapsed : 0f,
            VelocityY = elapsed > 0f ? (positionY - published.PositionY) / elapsed : 0f,
        };
    }

    private static float HeadingDelta(float headingDegrees, float otherDegrees)
    {
        var delta = (headingDegrees - otherDegrees) % 360f;
        if (delta > 180f)
        {
            delta -= 360f;
        }
        else if (delta < -180f)
        {
            delta += 360f;
        }

        return delta;
    }
}

/// <summary>
/// The last movement snapshot a ship put on the wire, and the velocity a client reads out of
/// it. Carried on the movement shard, never published.
/// </summary>
public struct PublishedMotion
{
    public ulong Tick;
    public float PositionX;
    public float PositionY;
    public float HeadingDegrees;
    public float VelocityX;
    public float VelocityY;
}
