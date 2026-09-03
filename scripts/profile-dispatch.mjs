#!/usr/bin/env node
// Summarise per-phase timings of the simulation dispatcher from module logs.
//
// Set SimulationWorkRules.ProfileDispatchPhases to true, publish, let the world
// tick with a player connected, then pipe the module logs through this script:
//
//   ./scripts/spacetime.sh logs sea-local -n 20000 \
//     --server http://host.docker.internal:43000 | node scripts/profile-dispatch.mjs
//
// Each "PROF <phase>" line is timed against the previous one inside the same
// tick, so a phase's cost is the time between its marker and the marker before it.
import { createInterface } from "node:readline";

const marker = /(\d{4}-\d{2}-\d{2}T[\d:.]+)Z\s+INFO: .*PROF (\w+)/;
const durations = new Map();
const tickTotals = [];
let previous = null;
let tickStart = null;

function record(phase, milliseconds) {
  if (!durations.has(phase)) {
    durations.set(phase, []);
  }
  durations.get(phase).push(milliseconds);
}

function timestampMilliseconds(stamp) {
  const [date, time] = stamp.split("T");
  const [hours, minutes, seconds] = time.split(":");
  return (
    Date.parse(`${date}T00:00:00Z`) +
    (Number(hours) * 3600 + Number(minutes) * 60 + Number(seconds)) * 1000
  );
}

for await (const line of createInterface({ input: process.stdin })) {
  const match = marker.exec(line);
  if (!match) {
    continue;
  }

  const [, stamp, phase] = match;
  const at = timestampMilliseconds(stamp);
  if (phase === "start") {
    tickStart = at;
    previous = at;
    continue;
  }

  if (previous === null) {
    continue;
  }

  record(phase, at - previous);
  previous = at;
  if (phase === "movement" && tickStart !== null) {
    tickTotals.push(at - tickStart);
  }
}

function summarise(samples) {
  const sorted = [...samples].sort((left, right) => left - right);
  const total = sorted.reduce((sum, value) => sum + value, 0);
  const percentile = (fraction) =>
    sorted[Math.min(sorted.length - 1, Math.floor(fraction * sorted.length))];
  return {
    samples: sorted.length,
    avg: (total / sorted.length).toFixed(2),
    p95: percentile(0.95).toFixed(2),
    max: sorted[sorted.length - 1].toFixed(2),
  };
}

if (tickTotals.length === 0) {
  console.error("No PROF markers found; enable SimulationWorkRules.ProfileDispatchPhases.");
  process.exit(1);
}

const rows = [...durations.entries()].map(([phase, samples]) => ({ phase, ...summarise(samples) }));
rows.push({ phase: "tick total", ...summarise(tickTotals) });
console.table(rows);
