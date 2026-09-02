namespace Sea.LoadTests;

public sealed record LatencyPercentiles(double P95Milliseconds, double P99Milliseconds)
{
    public static LatencyPercentiles Calculate(IEnumerable<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var ordered = samples.Order().ToArray();
        return ordered.Length == 0
            ? new LatencyPercentiles(0, 0)
            : new LatencyPercentiles(At(ordered, 0.95), At(ordered, 0.99));
    }

    private static double At(double[] samples, double percentile)
    {
        var rank = (int)Math.Ceiling(samples.Length * percentile);
        return samples[Math.Clamp(rank - 1, 0, samples.Length - 1)];
    }
}
