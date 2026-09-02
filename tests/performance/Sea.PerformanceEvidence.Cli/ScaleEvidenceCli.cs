using System.Globalization;
using System.Text.Json;
using Sea.Performance;

namespace Sea.PerformanceEvidence.Cli;

public static class ScaleEvidenceCli
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static int Run(string[] arguments)
    {
        if (arguments.Length < 8)
        {
            throw new InvalidDataException("Scale evidence arguments are incomplete.");
        }

        var expectedClients = PositiveInteger(arguments[2], "clients");
        var expectedActive = NonNegativeInteger(arguments[3], "active clients");
        var processorCount = PositiveInteger(arguments[5], "processor count");
        var load = ReadLoad(arguments[1]);
        LoadEvidenceContract.Validate(load, expectedClients, expectedActive);

        var resources = ResourceMeasurement.FromDockerStats(
            File.ReadLines(arguments[4]),
            processorCount);
        var timings = ReducerTimingMeasurement.Calculate(
            arguments.Skip(7).Select(ReadTimingSeries));
        var server = new ServerMeasurement(
            1,
            expectedActive,
            expectedClients - expectedActive,
            timings.P95Milliseconds,
            timings.P99Milliseconds,
            resources.NormalizedCpuPercent,
            resources.MemoryGrowthPercent);
        Write(arguments[6], server);

        var loadEvidence = new LoadEvidence(
            load.AttemptedClients,
            load.ConnectedClients,
            load.RetainedClients,
            server.ActiveShips,
            server.DormantShips,
            server.TickP95Milliseconds,
            server.TickP99Milliseconds,
            load.CommandAckP95Milliseconds,
            load.CommandAckP99Milliseconds,
            server.ServerCpuPercent,
            load.LoadRunnerCpuPercent,
            server.MemoryGrowthPercent,
            load.FailedClients);
        var verdict = PerformanceBudget.EvaluateLoadPerformance(loadEvidence);
        WriteSummary(timings, load, server, verdict);
        return verdict.Passed ? 0 : 1;
    }

    private static LoadClientMeasurement ReadLoad(string path)
    {
        return PerformanceEvidenceDocument.DeserializeFragment<LoadClientMeasurement>(
            File.ReadAllText(path));
    }

    private static ReducerTimingSeries ReadTimingSeries(string specification)
    {
        var parts = specification.Split(':', 3);
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidDataException(
                $"Invalid reducer timing specification: {specification}");
        }

        return new ReducerTimingSeries(
            parts[0],
            ReducerTimingMeasurement.ParseLines(File.ReadLines(parts[2])),
            PositiveInteger(parts[1], $"{parts[0]} minimum samples"));
    }

    private static int PositiveInteger(string text, string name)
    {
        var value = NonNegativeInteger(text, name);
        return value > 0
            ? value
            : throw new InvalidDataException($"{name} must be greater than zero.");
    }

    private static int NonNegativeInteger(string text, string name)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
            value >= 0
            ? value
            : throw new InvalidDataException($"{name} must be a non-negative whole number.");
    }

    private static void Write(string path, ServerMeasurement server)
    {
        var absolutePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ??
            throw new InvalidDataException("Server evidence output has no directory."));
        File.WriteAllText(
            absolutePath,
            JsonSerializer.Serialize(server, SerializerOptions));
    }

    private static void WriteSummary(
        ReducerTimingMeasurement timings,
        LoadClientMeasurement load,
        ServerMeasurement server,
        PerformanceVerdict verdict)
    {
        foreach (var reducer in timings.Reducers)
        {
            Console.WriteLine(
                $"{reducer.Name}: samples={reducer.SampleCount} " +
                $"p95={reducer.P95Microseconds:0.##}us " +
                $"p99={reducer.P99Microseconds:0.##}us");
        }

        Console.WriteLine(
            $"ack p95={load.CommandAckP95Milliseconds:0.###}ms " +
            $"p99={load.CommandAckP99Milliseconds:0.###}ms; " +
            $"server CPU={server.ServerCpuPercent:0.###}%; " +
            $"memory growth={server.MemoryGrowthPercent:0.###}%");
        foreach (var failure in verdict.Checks.Where(check => !check.Passed))
        {
            Console.Error.WriteLine(
                $"{failure.Name} failed: {failure.Measured:0.###}, expected {failure.Requirement}.");
        }
    }
}
