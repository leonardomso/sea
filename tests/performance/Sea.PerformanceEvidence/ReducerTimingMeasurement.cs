using System.Globalization;

namespace Sea.Performance;

public sealed record ReducerTimingMeasurement(
    double P95Milliseconds,
    double P99Milliseconds,
    IReadOnlyList<ReducerTimingResult> Reducers)
{
    public static ReducerTimingMeasurement Calculate(
        IEnumerable<ReducerTimingSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var results = series.Select(CalculateSeries).ToArray();
        if (results.Length == 0)
        {
            throw new InvalidDataException("No reducer timing series were supplied.");
        }

        return new ReducerTimingMeasurement(
            results.Max(result => result.P95Microseconds) / 1000,
            results.Max(result => result.P99Microseconds) / 1000,
            results);
    }

    public static IReadOnlyList<double> ParseLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => double.TryParse(
                    line,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value) && double.IsFinite(value) && value >= 0
                ? value
                : throw new InvalidDataException($"Invalid reducer timing sample: {line}"))
            .ToArray();
    }

    private static ReducerTimingResult CalculateSeries(ReducerTimingSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (string.IsNullOrWhiteSpace(series.Name) || series.MinimumSamples < 1 ||
            series.Microseconds.Count < series.MinimumSamples)
        {
            throw new InvalidDataException(
                $"Reducer {series.Name} has {series.Microseconds.Count} samples; " +
                $"{series.MinimumSamples} are required.");
        }

        var ordered = series.Microseconds.Order().ToArray();
        return new ReducerTimingResult(
            series.Name,
            ordered.Length,
            Percentile(ordered, 95),
            Percentile(ordered, 99));
    }

    private static double Percentile(double[] ordered, int percent)
    {
        var rank = (ordered.Length * percent + 99) / 100;
        return ordered[Math.Max(1, rank) - 1];
    }
}
