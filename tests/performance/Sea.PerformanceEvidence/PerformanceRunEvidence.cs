namespace Sea.Performance;

public sealed record PerformanceRunEvidence(
    int SchemaVersion,
    string Machine,
    DateTimeOffset RecordedAtUtc,
    LoadEvidence Load,
    ClientEvidence MacOS,
    ClientEvidence WebGL,
    CorrectnessEvidence Correctness,
    QualityEvidence Quality);
