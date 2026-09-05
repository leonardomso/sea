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
}
