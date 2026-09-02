#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

cd "$repo_root"
mkdir -p .cache
generated=$(mktemp "$repo_root/.cache/content-catalog.XXXXXX")
trap 'rm -f -- "$generated"' EXIT HUP INT TERM

node scripts/generate-content.mjs --out "$generated" >/dev/null
if ! diff -u server/spacetimedb/spacetimedb/Generated/ContentCatalog.g.cs "$generated"; then
  echo "ContentCatalog.g.cs is stale; run 'pnpm content:generate' and commit the result." >&2
  exit 1
fi

echo "Generated content catalog matches the committed JSON."
