import { mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { buildCatalog, ContentError } from "./lib/content-catalog.mjs";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dataDir = path.join(repoRoot, "server/spacetimedb/spacetimedb/Content/Data");
const defaultOutput = path.join(repoRoot, "server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs");

const args = process.argv.slice(2);
if (args.length !== 0 && (args.length !== 2 || args[0] !== "--out")) {
  console.error("usage: generate-content.mjs [--out <path>]");
  process.exit(2);
}
const outputPath = args.length === 0 ? defaultOutput : path.resolve(args[1]);

let source;
try {
  source = buildCatalog(dataDir);
} catch (error) {
  if (error instanceof ContentError) {
    console.error(error.message);
    process.exit(1);
  }
  if (error instanceof AggregateError) {
    for (const inner of error.errors) {
      console.error(inner.message);
    }
    process.exit(1);
  }
  throw error;
}

mkdirSync(path.dirname(outputPath), { recursive: true });
writeFileSync(outputPath, source);
console.log(`Wrote ${path.relative(repoRoot, outputPath)}`);
