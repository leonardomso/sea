using NBomber;
using NBomber.CSharp;
using NBomber.Contracts;
using Sea.LoadTests;
using System.Diagnostics;

var options = LoadRunOptions.FromEnvironment();
var tracker = new LoadRunTracker();
var activePool = new ClientPool<LoadClientSession>();
LoadClientPopulation? population = null;
LoadMeasurementWindow? measurementWindow = null;
var process = Process.GetCurrentProcess();
var cpuStart = process.TotalProcessorTime;
var wallStart = Stopwatch.StartNew();

var scenario = Scenario.Create("spacetimedb_real_client", RunActiveIterationAsync)
    .WithInit(async _ =>
    {
        population = await LoadClientPopulation.ConnectAsync(options, tracker)
            .ConfigureAwait(false);
        measurementWindow = new LoadMeasurementWindow(
            DateTimeOffset.UtcNow + options.SetupDuration);
        foreach (var session in population.Sessions.Take(options.ActiveClients))
        {
            activePool.AddClient(session);
        }
    })
    .WithClean(async _ =>
    {
        if (population is null)
        {
            return;
        }

        population.RecordRetention(tracker);
        await population.DisposeAsync().ConfigureAwait(false);
    })
    .WithWarmUpDuration(options.SetupDuration)
    .WithLoadSimulations(
        Simulation.KeepConstant(
            copies: options.ActiveClients,
            during: options.MeasureDuration));

try
{
    NBomberRunner.RegisterScenarios(scenario)
        .WithReportFolder(options.ReportDirectory)
        .Run();
}
catch (Exception error)
{
    tracker.RecordFailure(error);
}

var processorCount = Math.Max(1, Environment.ProcessorCount);
var cpuPercent = wallStart.Elapsed <= TimeSpan.Zero
    ? 0
    : (process.TotalProcessorTime - cpuStart).TotalMilliseconds /
        wallStart.Elapsed.TotalMilliseconds / processorCount * 100;
var evidence = tracker.Snapshot(
    options.ActiveClients,
    options.TotalClients - options.ActiveClients,
    cpuPercent);
LoadEvidenceDocument.Write(options.EvidencePath, evidence);
Console.WriteLine(
    $"SEA_LOAD_EVIDENCE={Path.GetFullPath(options.EvidencePath)}; " +
    $"cpu={cpuPercent:0.###}%; failures={evidence.FailedClients}");

return evidence.FailedClients == 0 ? 0 : 1;

async Task<IResponse> RunActiveIterationAsync(IScenarioContext context)
{
    try
    {
        var session = activePool.GetClient(context.ScenarioInfo.InstanceNumber);
        await Task.Delay(
                session.TakeCourseDelay(),
                context.ScenarioCancellationToken)
            .ConfigureAwait(false);
        var acknowledgement = await session.Client.TryStartSailingAsync(session.Plan)
            .ConfigureAwait(false);
        if (acknowledgement is TimeSpan latency &&
            measurementWindow?.Contains(DateTimeOffset.UtcNow) == true)
        {
            tracker.RecordAcknowledgement(latency);
        }
        return Response.Ok();
    }
    catch (OperationCanceledException) when (context.ScenarioCancellationToken.IsCancellationRequested)
    {
        return Response.Ok();
    }
    catch (Exception error)
    {
        tracker.RecordFailure(error);
        return Response.Fail();
    }
}
