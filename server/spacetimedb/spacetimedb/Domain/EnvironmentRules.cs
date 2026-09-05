namespace Sea.Server;

/// <summary>One storm, laid out for a weather band: where it starts and which way it drifts.</summary>
public readonly record struct StormLayout(float CentreX, float CentreY, float DriftDirectionDegrees);

/// <summary>
/// The weather: wind direction and storm layout, both on the tick clock rather
/// than the wall clock, so replaying a command log blows the same wind and
/// lays out the same storms (SEA_5 §5.2, §12.5, as amended).
/// </summary>
public static class EnvironmentRules
{
    /// <summary>
    /// Eight hours at 10 Hz. The band comes from the world tick counter rather
    /// than the wall clock, so replaying a command log blows the same wind
    /// (SEA_5 §5.2, as amended).
    /// </summary>
    public const ulong WindBandTicks = 288000UL;

    /// <summary>A storm is this wide. It matches SpatialRules.MaximumWorldInfluenceRadiusSquares,
    /// which is what bounds the chunk query that finds one.</summary>
    public const float StormRadiusSquares = 40f;

    /// <summary>How fast a storm drifts across the chart.</summary>
    public const float StormDriftSquaresPerSecond = 0.5f;

    /// <summary>SEA_5 §5.2: no map carries more than two storms at once.</summary>
    public const int MaximumStormsPerMap = 2;

    /// <summary>
    /// How many tries a storm gets to land its centre on open water before it
    /// settles for the middle of the map. The map is mostly sea, so this is
    /// never close to exhausted in practice.
    /// </summary>
    private const int MaximumPlacementAttempts = 64;

    private const double RollScale = 16_777_216d;

    public static ulong WindBand(ulong tick) => tick / WindBandTicks;

    /// <summary>
    /// The wind's bearing for one band. Strength is not rolled: SEA_5 §5.1
    /// fixes it at 0.10 for every map and every band, so there is nothing here
    /// but a direction.
    /// </summary>
    public static float WindForBand(ulong seed, ulong band)
    {
        var state = Mix(seed ^ Mix(band));
        return (float)((state >> 40) / (double)(1UL << 24) * 360d);
    }

    /// <summary>
    /// Nought to two storms for one map, laid out the same way whenever the same
    /// seed and band come round (SEA_5 §5.2, §12.5).
    /// </summary>
    public static IReadOnlyList<StormLayout> StormsForBand(ulong seed, ulong band, byte mapId)
    {
        var state = Mix(seed);
        state = Mix(state ^ band);
        state = Mix(state ^ mapId);

        var count = (int)(Unit(state) * (MaximumStormsPerMap + 1));
        var mask = ContentCatalog.LandMaskFor(mapId);
        var storms = new List<StormLayout>(count);
        for (var index = 0; index < count; index++)
        {
            state = Mix(state ^ (ulong)index);
            storms.Add(NextStorm(ref state, mask));
        }

        return storms;
    }

    /// <summary>
    /// The velocity a set of <paramref name="strength"/> squares per second on
    /// bearing <paramref name="directionDegrees"/> imparts. A northward set carries
    /// a hull up the screen, so its Y component is negative (SEA_5 §3.3).
    /// </summary>
    /// <remarks>
    /// This is <see cref="GeometryRules.Direction"/> scaled, and it has to stay that
    /// way: the second component used to be a bare <c>CosDegrees</c>, which is
    /// north-positive, so every current zone pushed south where the content said
    /// north. One place turns a bearing into a vector.
    /// </remarks>
    public static (float X, float Y) DirectionalVelocity(
        float directionDegrees,
        float strength)
    {
        var (x, y) = GeometryRules.Direction(directionDegrees);
        return (x * strength, y * strength);
    }

    /// <summary>
    /// Picks one storm's centre and drift, retrying against the land mask so a
    /// storm does not spawn sitting on an island, and falling back to the map's
    /// middle -- itself open water on every map the game ships -- if the map is
    /// unusually crowded.
    /// </summary>
    private static StormLayout NextStorm(ref ulong state, LandMask mask)
    {
        var minimum = WorldRules.MapMin + StormRadiusSquares;
        var maximum = WorldRules.MapMax - StormRadiusSquares;
        var span = maximum - minimum;
        var centreX = minimum + (span / 2f);
        var centreY = minimum + (span / 2f);
        for (var attempt = 0; attempt < MaximumPlacementAttempts; attempt++)
        {
            state = Mix(state);
            var x = minimum + ((float)Unit(state) * span);
            state = Mix(state);
            var y = minimum + ((float)Unit(state) * span);
            if (!mask.IsLand(x, y))
            {
                centreX = x;
                centreY = y;
                break;
            }
        }

        state = Mix(state);
        var drift = (float)(Unit(state) * 360d);
        return new StormLayout(centreX, centreY, drift);
    }

    private static double Unit(ulong state) => (state >> 40) / RollScale;

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
