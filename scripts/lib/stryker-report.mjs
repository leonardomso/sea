import path from "node:path";

/** Stryker runs inside the pinned SDK container, where the repository is mounted at /workspace. */
const repositoryPath = (file) => path.posix.normalize(file).replace(/^\/workspace\//, "");

/** Counts and score for one Stryker JSON report (mutation-report.json, schema 1.x). */
export function summarizeMutationReport(report) {
  const counts = { Killed: 0, Survived: 0, NoCoverage: 0, Timeout: 0, other: 0 };
  const survivors = [];
  for (const [file, { mutants = [] }] of Object.entries(report.files ?? {})) {
    for (const mutant of mutants) {
      if (mutant.status in counts) {
        counts[mutant.status] += 1;
      } else {
        counts.other += 1;
      }
      if (mutant.status === "Survived" || mutant.status === "NoCoverage") {
        survivors.push({
          file: repositoryPath(file),
          line: mutant.location?.start?.line ?? 0,
          status: mutant.status,
          mutator: mutant.mutatorName ?? "unknown",
          replacement: (mutant.replacement ?? "").replace(/\s+/g, " ").slice(0, 120),
        });
      }
    }
  }
  const measured = counts.Killed + counts.Survived + counts.NoCoverage + counts.Timeout;
  const scorePercent = measured === 0 ? 100 : ((counts.Killed + counts.Timeout) * 100) / measured;
  return { counts, measured, scorePercent, survivors };
}

/** The gate: every measured file must reach the score, and no mutant may survive or go uncovered. */
export function mutationGateFailures(summary, minimumScorePercent) {
  const failures = [];
  if (summary.scorePercent < minimumScorePercent) {
    failures.push(`mutation score ${summary.scorePercent.toFixed(2)}% is below ${minimumScorePercent}%`);
  }
  for (const survivor of summary.survivors) {
    failures.push(`${survivor.status} ${survivor.file}:${survivor.line} ${survivor.mutator}: ${survivor.replacement}`);
  }
  return failures;
}
