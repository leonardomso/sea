#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
maximum_lines=500
violations_file=$(mktemp)
trap 'rm -f "$violations_file"' EXIT HUP INT TERM

is_phase_nine_baseline() {
  case "$1" in
    server/spacetimedb/spacetimedb/Lib.cs | \
    server/spacetimedb/spacetimedb/SailingRules.cs | \
    apps/game-unity/Assets/Scripts/SeaHudController.cs | \
    apps/game-unity/Assets/Tests/Editor/SeaProjectTests.cs)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

find "$repo_root/server" "$repo_root/apps/game-unity/Assets" "$repo_root/tests" \
  -type f -name '*.cs' \
  ! -path '*/Generated/*' \
  ! -path '*/Packages/*' \
  ! -path '*/Library/*' \
  -print | while IFS= read -r absolute_path; do
    relative_path=${absolute_path#"$repo_root/"}
    line_count=$(wc -l < "$absolute_path" | tr -d ' ')
    if [ "$line_count" -le "$maximum_lines" ] || is_phase_nine_baseline "$relative_path"; then
      continue
    fi

    echo "$relative_path has $line_count lines. The limit is $maximum_lines." >> "$violations_file"
  done

if [ -s "$violations_file" ]; then
  cat "$violations_file" >&2
  exit 1
fi

echo "C# file-size check passed. Existing Phase 9 split targets are temporarily allowlisted."
