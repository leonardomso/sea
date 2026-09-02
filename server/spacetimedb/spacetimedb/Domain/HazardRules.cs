namespace Sea.Server;

public static class HazardRules
{
    public static byte ExposureMask(WorldObjectCode kind) => kind switch
    {
        WorldObjectCode.Storm => 1,
        WorldObjectCode.Shoal => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static byte SetExposure(byte current, WorldObjectCode kind, bool exposed)
    {
        var mask = ExposureMask(kind);
        return exposed
            ? (byte)(current | mask)
            : (byte)(current & ~mask);
    }

    public static bool HasExposure(byte current, WorldObjectCode kind) =>
        (current & ExposureMask(kind)) != 0;
}
