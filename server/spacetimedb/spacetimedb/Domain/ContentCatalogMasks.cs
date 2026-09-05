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
