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

    private static readonly Dictionary<byte, LandMask> DeepDraftMasks = BuildDeepDraftMasks();

    /// <summary>
    /// The same chart with the shallows filled in, for a hull that draws too much water to
    /// cross one (<see cref="PortRules.CanCrossShoal"/>). A fourth rate is not stopped at the
    /// edge of a shoal she was routed into; her course goes round it in the first place.
    /// </summary>
    public static LandMask DeepDraftMaskFor(byte mapId) => DeepDraftMasks[mapId];

    /// <summary>The chart a hull of this tier is routed on.</summary>
    public static LandMask NavigableMaskFor(byte mapId, byte tier) =>
        PortRules.CanCrossShoal(tier) ? LandMaskFor(mapId) : DeepDraftMaskFor(mapId);

    /// <summary>
    /// The chart a hull standing here plots her course on. It is the one her draught puts her on,
    /// except when she is already in water that chart calls coast: a current can carry a fourth
    /// rate into a shoal and a crossing can put her out in one, and a search refuses a course that
    /// starts on land, so from there the deep-draft chart would refuse every order she ever gave.
    /// A hull in shallows she should not be in is routed on the open chart until she is clear of
    /// them. She may leave the shallows; she is never sent into them.
    /// </summary>
    public static LandMask RoutingMaskFor(byte mapId, byte tier, float x, float y)
    {
        var mask = NavigableMaskFor(mapId, tier);
        return mask.IsLand(x, y) ? LandMaskFor(mapId) : mask;
    }

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

    /// <summary>
    /// Reads the shallows off the terrain grid rather than carrying a second bit array in the
    /// generated content. The grid and the land mask come out of the same rasterising pass over
    /// the same authored shapes, so a mask built from the one and a mask copied from the other
    /// are the same land by construction -- and a second array on disk would be a third place
    /// for it to be said, which is a third place for it to be said differently.
    /// </summary>
    private static Dictionary<byte, LandMask> BuildDeepDraftMasks()
    {
        var masks = new Dictionary<byte, LandMask>();
        foreach (var map in CreateDefault().Maps)
        {
            var size = map.LandMaskSize;
            var bits = map.LandMaskBits.ToArray();
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (SectorRules.TerrainAt(map, x, y) != TerrainCode.Shallow)
                    {
                        continue;
                    }

                    var index = (y * size) + x;
                    bits[index >> 6] |= 1UL << (index & 63);
                }
            }

            masks[map.MapId] = new LandMask(size, bits);
        }

        return masks;
    }
}
