namespace Sea.Server;

/// <summary>
/// The land masks, one per map, built once from the generated content and then read-only.
/// </summary>
/// <remarks>
/// This lives beside the other hand-written Domain files rather than under Content/: the domain
/// test project compiles Domain/*.cs flat and non-recursively, so anything under Content/ is
/// invisible to it. ContentCatalog.g.cs is generated as `public static partial class
/// ContentCatalog`, which is what makes this the other half of the same type.
/// </remarks>
public static partial class ContentCatalog
{
    private static readonly Dictionary<byte, LandMask> Masks = BuildMasks();

    /// <summary>
    /// The land mask for a map. Every reader shares this same instance for the life of the
    /// process; see <see cref="LandMask"/> for why that is safe.
    /// </summary>
    public static LandMask LandMaskFor(byte mapId) => Masks[mapId];

    private static readonly Dictionary<(byte MapId, MapEdge Edge), byte> ExitsByEdge = BuildExits();

    /// <summary>
    /// The chart beyond one border of one map, or null where that border is a coast (SEA_5
    /// §10.2). Read once a tick per hull standing in a band, so it is a lookup rather than a
    /// walk over the chart's exit list.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="MapEdge.None"/> is open water, not a border. A caller asking about it has lost
    /// track of where the hull is, and answering "nowhere" would read the same as a dead coast.
    /// </exception>
    public static byte? ExitFor(byte mapId, MapEdge edge)
    {
        if (edge == MapEdge.None)
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Open water is not a border.");
        }

        return ExitsByEdge.TryGetValue((mapId, edge), out var toMapId) ? toMapId : null;
    }

    private static Dictionary<(byte, MapEdge), byte> BuildExits()
    {
        var exits = new Dictionary<(byte, MapEdge), byte>();
        foreach (var map in CreateDefault().Maps)
        {
            foreach (var exit in map.Exits)
            {
                exits[(map.MapId, MapEdgeRules.Parse(exit.Edge))] = exit.ToMapId;
            }
        }

        return exits;
    }

    private static Dictionary<byte, LandMask> BuildMasks()
    {
        var masks = new Dictionary<byte, LandMask>();
        foreach (var map in CreateDefault().Maps)
        {
            masks[map.MapId] = new LandMask(map.LandMaskSize, map.LandMaskBits.ToArray());
        }

        return masks;
    }
}
