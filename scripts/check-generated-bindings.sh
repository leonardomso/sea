#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

cd "$repo_root"
mkdir -p .cache
generated_root=$(mktemp -d "$repo_root/.cache/binding-drift.XXXXXX")
generated_relative=${generated_root#"$repo_root/"}

cleanup() {
  case "$generated_root" in
    "$repo_root"/.cache/binding-drift.*)
      rm -rf -- "$generated_root"
      ;;
    *)
      echo "Refusing to remove an unexpected binding-drift path: $generated_root" >&2
      ;;
  esac
}
trap cleanup EXIT HUP INT TERM

if ! "$script_dir/spacetime.sh" generate --yes --lang csharp \
  --module-path server/spacetimedb/spacetimedb \
  --out-dir "$generated_relative/csharp" >"$generated_root/csharp.log" 2>&1; then
  cat "$generated_root/csharp.log" >&2
  exit 1
fi
if ! "$script_dir/spacetime.sh" generate --yes --lang typescript \
  --module-path server/spacetimedb/spacetimedb \
  --out-dir "$generated_relative/typescript" >"$generated_root/typescript.log" 2>&1; then
  cat "$generated_root/typescript.log" >&2
  exit 1
fi

diff -ru --exclude='*.meta' \
  apps/game-unity/Assets/Generated/SpacetimeDB \
  "$generated_root/csharp"
diff -ruB packages/contracts/src/generated "$generated_root/typescript"

echo "Generated SpacetimeDB bindings match the committed clients."
