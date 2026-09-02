using System.Text.Json;
using Sea.Performance;

namespace Sea.PerformanceEvidence.Cli;

public static class PerformanceCli
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static int Run(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            return arguments.Length > 0
                ? arguments[0] switch
            {
                "assemble" => Assemble(arguments),
                "scale" => ScaleEvidenceCli.Run(arguments),
                _ => Validate(arguments),
            }
                : Validate(arguments);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            Console.Error.WriteLine(error.Message);
            return 2;
        }
    }

    private static int Validate(string[] arguments)
    {
        if (arguments.Length != 3)
        {
            WriteUsage();
            return 2;
        }

        var evidence = PerformanceEvidenceDocument.Deserialize(File.ReadAllText(arguments[0]));
        return WriteVerdict(evidence, arguments[1], arguments[2]);
    }

    private static int Assemble(string[] arguments)
    {
        if (arguments.Length != 10)
        {
            WriteUsage();
            return 2;
        }

        var machine = Environment.GetEnvironmentVariable("SEA_PERFORMANCE_MACHINE");
        if (string.IsNullOrWhiteSpace(machine))
        {
            throw new InvalidDataException("SEA_PERFORMANCE_MACHINE is required.");
        }

        var evidence = PerformanceEvidenceAssembler.Assemble(
            machine,
            DateTimeOffset.UtcNow,
            Read<LoadClientMeasurement>(arguments[1]),
            Read<ServerMeasurement>(arguments[2]),
            Read<ClientEvidence>(arguments[3]),
            Read<ClientEvidence>(arguments[4]),
            Read<CorrectnessEvidence>(arguments[5]),
            Read<QualityEvidence>(arguments[6]));
        WriteFile(arguments[7], PerformanceEvidenceDocument.Serialize(evidence));
        return WriteVerdict(evidence, arguments[8], arguments[9]);
    }

    private static T Read<T>(string path) where T : class =>
        PerformanceEvidenceDocument.DeserializeFragment<T>(File.ReadAllText(path));

    private static int WriteVerdict(
        PerformanceRunEvidence evidence,
        string verdictPath,
        string summaryPath)
    {
        var verdict = PerformanceBudget.Evaluate(evidence);
        WriteFile(verdictPath, JsonSerializer.Serialize(verdict, SerializerOptions));
        WriteFile(summaryPath, PerformanceSummary.ToMarkdown(evidence, verdict));
        return verdict.Passed ? 0 : 1;
    }

    private static void WriteFile(string path, string contents)
    {
        var absolutePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ??
            throw new InvalidOperationException("Output path has no directory."));
        File.WriteAllText(absolutePath, contents);
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: performance-evidence <input.json> <verdict.json> <summary.md>\n" +
            "   or: performance-evidence assemble <load.json> <server.json> <mac.json> " +
            "<webgl.json> <correctness.json> <quality.json> <evidence.json> " +
            "<verdict.json> <summary.md>\n" +
            "   or: performance-evidence scale <load.json> <clients> <active> " +
            "<resources.txt> <processors> <server.json> <name:min:path>...");
    }
}
