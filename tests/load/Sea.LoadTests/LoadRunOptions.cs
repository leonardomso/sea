using System.Globalization;

namespace Sea.LoadTests;

public sealed record LoadRunOptions(
    string Server,
    string Database,
    int TotalClients,
    int ActiveClients,
    TimeSpan RampDuration,
    TimeSpan SetupDuration,
    TimeSpan MeasureDuration,
    string EvidencePath,
    string ReportDirectory)
{
    public static LoadRunOptions FromEnvironment()
    {
        var database = Required("SEA_LOAD_DATABASE");
        var clients = Integer("SEA_LOAD_CLIENTS", 5_000);
        var active = Integer("SEA_LOAD_ACTIVE_CLIENTS", 1_000);
        var rampSeconds = Integer("SEA_LOAD_RAMP_SECONDS", 60);
        var setupSeconds = Integer("SEA_LOAD_SETUP_SECONDS", 120);
        var measureSeconds = Integer("SEA_LOAD_SECONDS", 900);
        Validate(clients, active, rampSeconds, setupSeconds, measureSeconds);
        return new LoadRunOptions(
            Environment.GetEnvironmentVariable("SEA_LOAD_SERVER") ??
                "http://host.docker.internal:3000",
            database,
            clients,
            active,
            TimeSpan.FromSeconds(rampSeconds),
            TimeSpan.FromSeconds(setupSeconds),
            TimeSpan.FromSeconds(measureSeconds),
            Environment.GetEnvironmentVariable("SEA_LOAD_EVIDENCE") ??
                "Build/performance/load-client.json",
            Environment.GetEnvironmentVariable("SEA_LOAD_REPORT_DIRECTORY") ??
                "Build/performance/nbomber");
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Set {name} to run the load workload.")
            : value;
    }

    private static int Integer(string name, int defaultValue)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return defaultValue;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"{name} must be a whole number.");
    }

    private static void Validate(
        int clients,
        int active,
        int rampSeconds,
        int setupSeconds,
        int measureSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clients);
        ArgumentOutOfRangeException.ThrowIfNegative(active);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(active, clients);
        ArgumentOutOfRangeException.ThrowIfNegative(rampSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(setupSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(measureSeconds);
    }
}
