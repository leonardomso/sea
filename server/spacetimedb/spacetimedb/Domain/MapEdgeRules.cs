namespace Sea.Server;

/// <summary>Which border of the chart a hull is standing in, if any.</summary>
public enum MapEdge : byte
{
    None = 0,
    North = 1,
    East = 2,
    South = 3,
    West = 4,
}

/// <summary>Sailing off one chart and onto the next (SEA_5 §10.2).</summary>
/// <remarks>
/// The band is six squares wide, which is about a second of sailing, so a
/// captain who meant to cross has crossed and one who was following the coast
/// has time to turn. She arrives at the same place along the far edge she left
/// at, so the crossing reads as continuing rather than as being moved.
/// <para>
/// The chart has (0,0) at its top-left with x growing east and y growing south,
/// and a heading of 0 is north, which is -Y. So the north edge is y = 0, not
/// y = <see cref="WorldRules.MapMax"/>, and sailing north off the top of this
/// chart lands her against the <em>bottom</em> of the chart above.
/// </para>
/// </remarks>
public static class MapEdgeRules
{
    /// <summary>How wide the crossing band is, in squares, measured in from the border.</summary>
    public const float BandSquares = 6f;

    /// <summary>
    /// How far in she appears on the new map. Larger than the band, so arriving
    /// does not put her straight back into a crossing and bounce her between
    /// two charts.
    /// </summary>
    public const float SpawnInsetSquares = 8f;

    /// <summary>
    /// The border she is standing in, or <see cref="MapEdge.None"/> in open water.
    /// A corner belongs to whichever border is nearer; an exact tie goes north,
    /// then west, so the answer is one edge and always the same one.
    /// </summary>
    public static MapEdge EdgeAt(float x, float y)
    {
        var toNorth = y - WorldRules.MapMin;
        var toSouth = WorldRules.MapMax - y;
        var toWest = x - WorldRules.MapMin;
        var toEast = WorldRules.MapMax - x;
        var nearest = MathF.Min(MathF.Min(toNorth, toSouth), MathF.Min(toWest, toEast));
        if (nearest >= BandSquares)
        {
            return MapEdge.None;
        }

        if (toNorth <= nearest)
        {
            return MapEdge.North;
        }

        if (toWest <= nearest)
        {
            return MapEdge.West;
        }

        return toSouth <= nearest ? MapEdge.South : MapEdge.East;
    }

    /// <summary>
    /// Where she appears on the map she has sailed onto. Crossing north puts her
    /// near the southern edge of the map above, at the same distance along it.
    /// </summary>
    /// <param name="crossed">The border of the old chart she left through.</param>
    /// <param name="alongAxis">
    /// Her position on the axis that runs along that border: x for the north and
    /// south borders, y for the east and west ones.
    /// </param>
    /// <remarks>
    /// <see cref="MapEdge.None"/> is not a crossing and has no arrival point, so
    /// it is rejected rather than answered with a position no captain sailed to.
    /// Callers already know which border fired.
    /// </remarks>
    public static (float X, float Y) ArrivalPoint(MapEdge crossed, float alongAxis)
    {
        const float inset = SpawnInsetSquares;
        return crossed switch
        {
            MapEdge.North => (alongAxis, WorldRules.MapMax - inset),
            MapEdge.South => (alongAxis, WorldRules.MapMin + inset),
            MapEdge.West => (WorldRules.MapMax - inset, alongAxis),
            MapEdge.East => (WorldRules.MapMin + inset, alongAxis),
            _ => throw new ArgumentOutOfRangeException(nameof(crossed), crossed, "Not a crossing."),
        };
    }

    /// <summary>
    /// The border a hull arrives against, having left through <paramref name="crossed"/>.
    /// Sailing east off one chart puts her against the west border of the next, so this is the
    /// edge she would sail back out through to come home.
    /// </summary>
    public static MapEdge Opposite(MapEdge crossed) => crossed switch
    {
        MapEdge.North => MapEdge.South,
        MapEdge.South => MapEdge.North,
        MapEdge.West => MapEdge.East,
        MapEdge.East => MapEdge.West,
        _ => throw new ArgumentOutOfRangeException(nameof(crossed), crossed, "Not a crossing."),
    };

    /// <summary>
    /// An edge authored by name in maps.json. The names are the compass, lower case, and an
    /// unknown one is a content error rather than an edge that quietly leads nowhere: a typo in
    /// a chart's exits would otherwise strand a captain with no way of telling why.
    /// </summary>
    public static MapEdge Parse(string edge)
    {
        if (!TryParse(edge, out var parsed))
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Not a border of the chart.");
        }

        return parsed;
    }

    /// <summary>
    /// The same reading, for the content check, which has to collect a sentence about a bad
    /// name rather than throw on it: an author wants every mistake in one report, not the
    /// first one.
    /// </summary>
    public static bool TryParse(string edge, out MapEdge parsed)
    {
        switch (edge)
        {
            case "north":
                parsed = MapEdge.North;
                return true;
            case "east":
                parsed = MapEdge.East;
                return true;
            case "south":
                parsed = MapEdge.South;
                return true;
            case "west":
                parsed = MapEdge.West;
                return true;
            default:
                parsed = MapEdge.None;
                return false;
        }
    }

    /// <summary>The name a border is authored under, so a report reads the way the file does.</summary>
    public static string NameOf(MapEdge edge) => edge switch
    {
        MapEdge.North => "north",
        MapEdge.East => "east",
        MapEdge.South => "south",
        MapEdge.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Not a border of the chart."),
    };

    /// <summary>
    /// Where a hull is put when she reaches a border that leads nowhere: just
    /// inside the band, not on the line. Stopping her dead on the edge would let
    /// her sit in a crossing that never fires; this reads as a coast.
    /// </summary>
    public static (float X, float Y) HoldInside(float x, float y) =>
        (Math.Clamp(x, WorldRules.MapMin + BandSquares, WorldRules.MapMax - BandSquares),
         Math.Clamp(y, WorldRules.MapMin + BandSquares, WorldRules.MapMax - BandSquares));

    /// <summary>
    /// Whether a hull is still lying against a border she was held at.
    /// </summary>
    /// <remarks>
    /// <see cref="HoldInside"/> puts her exactly on the band's inner line and
    /// <see cref="EdgeAt"/> calls that line open water, so the crossing fires once rather
    /// than every tick she waits there. This is the other half of that arrangement: the
    /// prompt she was raised stands while she is on the line, and the only thing that takes
    /// her off it is sailing. The comparison is exact because the number is exact -- the
    /// hold writes the bound itself, not a position that drifted towards it.
    /// </remarks>
    public static bool IsHeldAgainst(float x, float y, MapEdge edge) => edge switch
    {
        MapEdge.North => y == WorldRules.MapMin + BandSquares,
        MapEdge.South => y == WorldRules.MapMax - BandSquares,
        MapEdge.West => x == WorldRules.MapMin + BandSquares,
        MapEdge.East => x == WorldRules.MapMax - BandSquares,
        _ => false,
    };
}
