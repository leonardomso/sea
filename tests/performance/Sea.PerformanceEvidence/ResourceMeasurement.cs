using System.Globalization;

namespace Sea.Performance;

public sealed record ResourceMeasurement(
    double NormalizedCpuPercent,
    double MemoryGrowthPercent)
{
    private static readonly string[] MemoryUnits = ["GiB", "MiB", "KiB", "B"];

    public static ResourceMeasurement FromDockerStats(
        IEnumerable<string> lines,
        int processorCount)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processorCount);
        var samples = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Parse)
            .ToArray();
        if (samples.Length < 2)
        {
            throw new InvalidDataException("Container resource evidence is incomplete.");
        }

        var cpu = samples.Average(sample => sample.CpuPercent) / processorCount;
        var windowSize = Math.Max(1, samples.Length / 5);
        var initialMemory = samples.Take(windowSize).Average(sample => sample.MemoryBytes);
        var finalMemory = samples.TakeLast(windowSize).Average(sample => sample.MemoryBytes);
        var growth = Math.Max(0, (finalMemory - initialMemory) / initialMemory * 100);
        return new ResourceMeasurement(cpu, growth);
    }

    private static ResourceSample Parse(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !double.TryParse(
                parts[0].TrimEnd('%'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var cpu))
        {
            throw new InvalidDataException($"Invalid Docker resource sample: {line}");
        }

        var memoryText = parts[1].Split(' ', 2)[0];
        var unit = MemoryUnits.FirstOrDefault(candidate =>
                memoryText.EndsWith(candidate, StringComparison.Ordinal)) ??
            throw new InvalidDataException($"Invalid Docker memory sample: {line}");
        var valueText = memoryText[..^unit.Length];
        if (!double.TryParse(
                valueText,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var memory))
        {
            throw new InvalidDataException($"Invalid Docker memory sample: {line}");
        }

        var multiplier = unit switch
        {
            "B" => 1d,
            "KiB" => 1024d,
            "MiB" => 1024d * 1024,
            "GiB" => 1024d * 1024 * 1024,
            _ => throw new InvalidDataException($"Unsupported memory unit: {line}"),
        };
        return new ResourceSample(cpu, memory * multiplier);
    }
}
