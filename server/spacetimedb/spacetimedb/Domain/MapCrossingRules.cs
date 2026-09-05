namespace Sea.Server;

/// <summary>
/// The prompt a hull is raised when she comes up against a border, and where answering it
/// would put her (SEA_5 §10.2).
/// </summary>
/// <remarks>
/// A crossing is offered, never taken: reaching the band holds her inside it and asks, and
/// the question is only worth asking where the border leads somewhere. Working the arrival
/// point out here rather than authoring it means a captain comes out of the new chart at the
/// point along its edge she left the old one at, so the crossing reads as sailing on.
/// </remarks>
public static class MapCrossingRules
{
    /// <summary>A standing crossing: which chart, through which border, and where she lands.</summary>
    public readonly record struct CrossingOffer(byte ToMapId, MapEdge Edge, float SpawnX, float SpawnY);

    /// <summary>
    /// What a hull comes out of a crossing with, and what she comes out without (SEA_5 §10.2).
    /// </summary>
    /// <remarks>
    /// Her course was plotted against a land mask that does not apply on the new chart, and
    /// sailing it there would take her through an island she cannot see, so it goes. Her target
    /// is sixty squares away on a chart she is no longer on, and whatever was stuck to her was
    /// stuck to her there; both go with the course. Her heading is not named here because it is
    /// the one thing she keeps: she puts out of the new chart pointing the way she came into it.
    /// </remarks>
    public readonly record struct Arrival(
        byte MapId,
        float PositionX,
        float PositionY,
        int ChunkX,
        int ChunkY,
        bool HasRoute,
        ulong TargetEntityId,
        bool IsEngaged,
        byte MovementStatusMask,
        float MovementSlowMagnitude,
        byte EnvironmentExposureCode);

    /// <summary>
    /// The crossing a hull held against <paramref name="edge"/> is offered, or null where that
    /// border leads nowhere and she is only held.
    /// </summary>
    /// <param name="mapId">The chart she is on now.</param>
    /// <param name="edge">The border she came up against.</param>
    /// <param name="heldX">Where the hold left her, which is where she puts out from.</param>
    /// <param name="heldY">The same, on the other axis.</param>
    public static CrossingOffer? Offer(byte mapId, MapEdge edge, float heldX, float heldY)
    {
        if (edge == MapEdge.None || ContentCatalog.ExitFor(mapId, edge) is not byte toMapId)
        {
            return null;
        }

        // The axis that runs along the border she left through: her place on it is the one
        // thing about the crossing she chose, so it is the one thing that is carried over.
        var alongAxis = edge is MapEdge.North or MapEdge.South ? heldX : heldY;
        var (spawnX, spawnY) = MapEdgeRules.ArrivalPoint(edge, alongAxis);
        return new CrossingOffer(toMapId, edge, spawnX, spawnY);
    }

    /// <inheritdoc cref="Arrive(byte, float, float)"/>
    public static Arrival Arrive(CrossingOffer offer) =>
        Arrive(offer.ToMapId, offer.SpawnX, offer.SpawnY);

    /// <summary>
    /// The state a hull is in the instant she answers the prompt. Written down here rather than
    /// in the reducer that applies it so that what a crossing costs her is one thing a reader
    /// can check against §10.2, and one thing a test can pin without a live module.
    /// </summary>
    public static Arrival Arrive(byte toMapId, float spawnX, float spawnY) => new(
        toMapId,
        spawnX,
        spawnY,
        SpatialRules.ChunkCoordinate(spawnX),
        SpatialRules.ChunkCoordinate(spawnY),
        HasRoute: false,
        TargetEntityId: 0UL,
        IsEngaged: false,
        MovementStatusMask: 0,
        MovementSlowMagnitude: 0f,
        EnvironmentExposureCode: 0);
}
