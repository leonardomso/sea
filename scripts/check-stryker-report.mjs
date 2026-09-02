import fs from "node:fs";
import path from "node:path";

const [reportPath, coveragePath, qualityPath] = process.argv.slice(2);
if (!reportPath || !coveragePath || !qualityPath) {
  throw new Error("Usage: check-stryker-report.mjs <report.json> <coverage.txt> <quality.json>");
}

const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
const statuses = new Map();
for (const file of Object.values(report.files ?? {})) {
  for (const mutant of file.mutants ?? []) {
    statuses.set(mutant.status, (statuses.get(mutant.status) ?? 0) + 1);
  }
}

const killed = statuses.get("Killed") ?? 0;
const survived = statuses.get("Survived") ?? 0;
const noCoverage = statuses.get("NoCoverage") ?? 0;
const timeout = statuses.get("Timeout") ?? 0;
const measured = killed + survived + noCoverage + timeout;
const mutationScore = measured === 0 ? 0 : killed * 100 / measured;
const coverage = fs.readFileSync(coveragePath, "utf8");
const lineCoverage = percentage(coverage, /^  Line coverage: ([0-9.]+)%/m);
const branchCoverage = percentage(coverage, /^  Branch coverage: ([0-9.]+)%/m);
const criticalSurvivors = survived + noCoverage + timeout;
const quality = {
  lineCoveragePercent: lineCoverage,
  branchCoveragePercent: branchCoverage,
  mutationScorePercent: mutationScore,
  criticalSurvivingMutations: criticalSurvivors,
};
fs.mkdirSync(path.dirname(qualityPath), { recursive: true });
fs.writeFileSync(qualityPath, `${JSON.stringify(quality, null, 2)}\n`);

if (mutationScore < 90 || criticalSurvivors !== 0) {
  console.error(JSON.stringify({ ...quality, statuses: Object.fromEntries(statuses) }, null, 2));
  process.exit(1);
}

console.log(
  `Command policy mutation score ${mutationScore.toFixed(2)}%; ` +
  `${killed} mutants killed and no critical mutant survived.`
);

function percentage(text, pattern) {
  const match = text.match(pattern);
  if (!match) throw new Error(`Missing coverage value for ${pattern}.`);
  return Number(match[1]);
}
