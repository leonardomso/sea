namespace Sea.Server;

public static class SpatialRules
{
    public const float ChunkSize = 25f;
    public const int ChunkCountPerAxis = 8;

    public static int ChunkCoordinate(float position)
    {
        if (!float.IsFinite(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var coordinate = (int)MathF.Floor((position - WorldRules.MapMin) / ChunkSize);
        return Math.Clamp(coordinate, 0, ChunkCountPerAxis - 1);
    }
}
