using System.Globalization;
using System.Text;

namespace Sea.Performance;

public static class PerformanceSummary
{
    public static string ToMarkdown(
        PerformanceRunEvidence evidence,
        PerformanceVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(verdict);
        var output = new StringBuilder();
        output.AppendLine("# Sea performance verdict");
        output.AppendLine();
        output.AppendLine(
            CultureInfo.InvariantCulture,
            $"- Result: **{(verdict.Passed ? "PASS" : "FAIL")}**");
        output.AppendLine(CultureInfo.InvariantCulture, $"- Machine: {evidence.Machine}");
        output.AppendLine(
            CultureInfo.InvariantCulture,
            $"- Recorded: {evidence.RecordedAtUtc:O}");
        output.AppendLine();
        output.AppendLine("| Check | Result | Measured | Requirement |");
        output.AppendLine("|---|---:|---:|---:|");
        foreach (var check in verdict.Checks)
        {
            output.Append("| ").Append(check.Name)
                .Append(" | ").Append(check.Passed ? "PASS" : "FAIL")
                .Append(" | ")
                .Append(check.Measured.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" | ").Append(check.Requirement).AppendLine(" |");
        }

        return output.ToString();
    }
}
