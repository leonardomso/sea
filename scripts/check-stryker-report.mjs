#!/usr/bin/env node
// Gates one per-file Stryker run: the file must reach the minimum mutation score and leave no
// surviving or uncovered mutant. Survivors are printed with their location and replacement so
// the next test to write is obvious.
import fs from "node:fs";
import path from "node:path";
import { mutationGateFailures, summarizeMutationReport } from "./lib/stryker-report.mjs";

const [reportPath, label, summaryPath] = process.argv.slice(2);
if (!reportPath || !label || !summaryPath) {
  console.error("usage: check-stryker-report.mjs <mutation-report.json> <label> <summary.json>");
  process.exit(2);
}

const minimumScorePercent = 90;
const summary = summarizeMutationReport(JSON.parse(fs.readFileSync(reportPath, "utf8")));
fs.mkdirSync(path.dirname(summaryPath), { recursive: true });
fs.writeFileSync(
  summaryPath,
  `${JSON.stringify({ label, minimumScorePercent, scorePercent: summary.scorePercent, counts: summary.counts }, null, 2)}\n`,
);

const failures = mutationGateFailures(summary, minimumScorePercent);
if (failures.length > 0) {
  console.error(`${label}: mutation gate failed.\n${failures.map((failure) => `  ${failure}`).join("\n")}`);
  process.exit(1);
}

console.log(
  `${label}: mutation score ${summary.scorePercent.toFixed(2)}% (${summary.counts.Killed} killed, ${summary.counts.Timeout} timed out, ${summary.measured} measured).`,
);
