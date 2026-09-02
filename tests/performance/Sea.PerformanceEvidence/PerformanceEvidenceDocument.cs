using System.Text.Json;

namespace Sea.Performance;

public static class PerformanceEvidenceDocument
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize(PerformanceRunEvidence evidence)
    {
        Validate(evidence);
        return JsonSerializer.Serialize(evidence, Options);
    }

    public static PerformanceRunEvidence Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Performance evidence JSON is empty.");
        }

        try
        {
            var evidence = JsonSerializer.Deserialize<PerformanceRunEvidence>(json, Options);
            Validate(evidence);
            return evidence!;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Performance evidence JSON is invalid.", error);
        }
    }

    public static T DeserializeFragment<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Performance fragment JSON is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ??
                throw new InvalidDataException("Performance fragment JSON is incomplete.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Performance fragment JSON is invalid.", error);
        }
    }

    private static void Validate(PerformanceRunEvidence? evidence)
    {
        if (evidence is null || string.IsNullOrWhiteSpace(evidence.Machine) ||
            evidence.Load is null || evidence.MacOS is null || evidence.WebGL is null ||
            evidence.Correctness is null || evidence.Quality is null)
        {
            throw new InvalidDataException("Performance evidence is incomplete.");
        }
    }
}
