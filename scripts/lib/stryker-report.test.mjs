import assert from "node:assert/strict";
import test from "node:test";
import { mutationGateFailures, summarizeMutationReport } from "./stryker-report.mjs";

const mutant = (status, line, extra = {}) => ({
  status,
  location: { start: { line, column: 1 } },
  mutatorName: "Arithmetic operator",
  replacement: "a  -  b",
  ...extra,
});

test("summarizes counts, score and survivors from a Stryker report", () => {
  const summary = summarizeMutationReport({
    files: {
      "Domain/ShipStatRules.cs": {
        mutants: [mutant("Killed", 1), mutant("Killed", 2), mutant("Timeout", 3), mutant("Survived", 4), mutant("NoCoverage", 5), mutant("Ignored", 6)],
      },
    },
  });
  assert.equal(summary.measured, 5);
  assert.equal(summary.counts.other, 1);
  assert.equal(summary.scorePercent, 60);
  assert.deepEqual(summary.survivors.map((survivor) => [survivor.line, survivor.status]), [[4, "Survived"], [5, "NoCoverage"]]);
  assert.equal(summary.survivors[0].replacement, "a - b");
});

test("survivor paths are reported relative to the repository", () => {
  const summary = summarizeMutationReport({
    files: { "/workspace/server/spacetimedb/domain/../spacetimedb/Domain/Rules.cs": { mutants: [mutant("Survived", 3)] } },
  });
  assert.equal(summary.survivors[0].file, "server/spacetimedb/spacetimedb/Domain/Rules.cs");
});

test("a file with no mutants passes with a perfect score", () => {
  const summary = summarizeMutationReport({ files: { "Domain/Empty.cs": { mutants: [] } } });
  assert.equal(summary.measured, 0);
  assert.equal(summary.scorePercent, 100);
  assert.deepEqual(mutationGateFailures(summary, 90), []);
});

test("the gate names the score and every survivor", () => {
  const summary = summarizeMutationReport({
    files: { "Domain/Rules.cs": { mutants: [mutant("Killed", 1), mutant("Survived", 9)] } },
  });
  const failures = mutationGateFailures(summary, 90);
  assert.equal(failures.length, 2);
  assert.match(failures[0], /50\.00% is below 90%/);
  assert.match(failures[1], /Survived Domain\/Rules.cs:9 Arithmetic operator: a - b/);
});

test("timeouts count as killed and killed-only reports pass", () => {
  const summary = summarizeMutationReport({
    files: { "Domain/Rules.cs": { mutants: [mutant("Killed", 1), mutant("Timeout", 2)] } },
  });
  assert.equal(summary.scorePercent, 100);
  assert.deepEqual(mutationGateFailures(summary, 90), []);
});
