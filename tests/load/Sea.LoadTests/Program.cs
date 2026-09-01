using NBomber.CSharp;
using Sea.LoadTests;
using System.Globalization;

var database = Environment.GetEnvironmentVariable("SEA_LOAD_DATABASE");
if (string.IsNullOrWhiteSpace(database))
{
    Console.WriteLine("Set SEA_LOAD_DATABASE to run the real SpacetimeDB load smoke.");
    return;
}

var server = Environment.GetEnvironmentVariable("SEA_LOAD_SERVER")
    ?? "http://host.docker.internal:3000";
var clients = int.TryParse(
    Environment.GetEnvironmentVariable("SEA_LOAD_CLIENTS"),
    NumberStyles.Integer,
    CultureInfo.InvariantCulture,
    out var count)
    ? count
    : 10;
var duration = int.TryParse(
    Environment.GetEnvironmentVariable("SEA_LOAD_SECONDS"),
    NumberStyles.Integer,
    CultureInfo.InvariantCulture,
    out var seconds)
    ? seconds
    : 10;

var scenario = Scenario.Create("spacetimedb_real_client", async _ =>
    {
        var client = await SpacetimeLoadClient.ConnectAsync(server, database)
            .ConfigureAwait(false);
        try
        {
            await client.LoadPlayerAsync().ConfigureAwait(false);
            return Response.Ok();
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    })
    .WithoutWarmUp()
    .WithLoadSimulations(Simulation.KeepConstant(
        copies: clients,
        during: TimeSpan.FromSeconds(duration)));

NBomberRunner.RegisterScenarios(scenario).Run();
