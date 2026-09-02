using Sea.Performance;
using Xunit;

namespace Sea.PerformanceEvidence.Tests;

public sealed class PerformanceBudgetTests
{
    [Fact]
    public void ExactFinalBudgetBoundariesPass()
    {
        var evidence = EvidenceFactory.Passing();

        var verdict = PerformanceBudget.Evaluate(evidence);

        Assert.True(verdict.Passed);
        Assert.All(verdict.Checks, check => Assert.True(check.Passed, check.Name));
        Assert.Equal(
            verdict.Checks.Count,
            verdict.Checks.Select(check => check.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("connections-retained")]
    [InlineData("server-tick-p99")]
    [InlineData("command-ack-p95")]
    [InlineData("server-cpu")]
    [InlineData("load-runner-cpu")]
    [InlineData("memory-growth")]
    [InlineData("failed-load-clients")]
    [InlineData("macos-frame-p95")]
    [InlineData("webgl-frame-p95")]
    [InlineData("client-frame-p99")]
    [InlineData("idle-allocations")]
    [InlineData("runtime-errors")]
    [InlineData("line-coverage")]
    [InlineData("branch-coverage")]
    [InlineData("mutation-score")]
    public void OneFailedMeasurementFailsOnlyItsNamedBudget(string failedCheck)
    {
        var evidence = EvidenceFactory.WithFailure(failedCheck);

        var verdict = PerformanceBudget.Evaluate(evidence);

        Assert.False(verdict.Passed);
        Assert.Contains(
            verdict.Checks,
            check => string.Equals(check.Name, failedCheck, StringComparison.Ordinal) &&
                !check.Passed);
    }

    [Fact]
    public void RetentionIsCalculatedFromAttemptedClientsWithoutRoundingUp()
    {
        var evidence = EvidenceFactory.Passing() with
        {
            Load = EvidenceFactory.Passing().Load with { RetainedClients = 4_994 },
        };

        var check = PerformanceBudget.Evaluate(evidence).Checks
            .Single(value => string.Equals(
                value.Name,
                "connections-retained",
                StringComparison.Ordinal));

        Assert.False(check.Passed);
        Assert.Equal(99.88, check.Measured, precision: 2);
    }

    [Fact]
    public void MarkdownSummaryContainsEachFailedCheckAndMachineIdentity()
    {
        var evidence = EvidenceFactory.WithFailure("server-tick-p99");
        var verdict = PerformanceBudget.Evaluate(evidence);

        var markdown = PerformanceSummary.ToMarkdown(evidence, verdict);

        Assert.Contains("M1 Pro local", markdown, StringComparison.Ordinal);
        Assert.Contains("server-tick-p99", markdown, StringComparison.Ordinal);
        Assert.Contains("FAIL", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceJsonRoundTripsWithStableCamelCaseNames()
    {
        var expected = EvidenceFactory.Passing();

        var json = PerformanceEvidenceDocument.Serialize(expected);
        var actual = PerformanceEvidenceDocument.Deserialize(json);

        Assert.Equal(expected, actual);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"tickP95Milliseconds\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"machine\":\"\"}")]
    public void IncompleteEvidenceJsonIsRejected(string json)
    {
        Assert.Throws<InvalidDataException>(() =>
            PerformanceEvidenceDocument.Deserialize(json));
    }

    [Fact]
    public void AssemblerMapsEachAdapterMeasurementIntoOneCanonicalRun()
    {
        var expected = EvidenceFactory.Passing();
        var load = new LoadClientMeasurement(
            1, 5_000, 5_000, 4_995, 1_000, 4_000, 150, 250, 85, 0);
        var server = new ServerMeasurement(1, 1_000, 4_000, 10, 20, 85, 5);

        var actual = PerformanceEvidenceAssembler.Assemble(
            expected.Machine,
            expected.RecordedAtUtc,
            load,
            server,
            expected.MacOS,
            expected.WebGL,
            expected.Correctness,
            expected.Quality);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void AssemblerRejectsUnsupportedFragmentSchemas(int loadSchema, int serverSchema)
    {
        var expected = EvidenceFactory.Passing();
        var load = new LoadClientMeasurement(
            loadSchema, 5_000, 5_000, 5_000, 1_000, 4_000, 150, 250, 85, 0);
        var server = new ServerMeasurement(serverSchema, 1_000, 4_000, 10, 20, 85, 5);

        Assert.Throws<InvalidDataException>(() => PerformanceEvidenceAssembler.Assemble(
            expected.Machine,
            expected.RecordedAtUtc,
            load,
            server,
            expected.MacOS,
            expected.WebGL,
            expected.Correctness,
            expected.Quality));
    }
}
