using System.Text.Json;

namespace Sea.LoadTests;

public static class LoadEvidenceDocument
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static void Write(string path, LoadExecutionEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(evidence);
        var absolutePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ??
            throw new InvalidOperationException("Evidence path has no directory."));
        File.WriteAllText(absolutePath, JsonSerializer.Serialize(evidence, Options));
    }
}
