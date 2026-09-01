#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
unity_assets="$repo_root/apps/game-unity/Assets"

for assembly in \
  Domain/Sea.Domain.asmdef \
  Networking/Sea.Networking.asmdef \
  Input/Sea.Input.asmdef \
  Presentation/Sea.Presentation.asmdef \
  UI/Sea.UI.asmdef \
  Editor/Sea.Editor.asmdef \
  Tests/EditMode/Sea.Tests.EditMode.asmdef \
  Tests/PlayMode/Sea.Tests.PlayMode.asmdef \
  Tests/Performance/Sea.Tests.Performance.asmdef; do
  if [ ! -f "$unity_assets/$assembly" ]; then
    echo "Missing Unity assembly definition: Assets/$assembly" >&2
    exit 1
  fi
done

if rg -n \
  'Find(First|Any)?ObjectByType|FindObjectOfType|GameObject\.Find|Resources\.FindObjectsOfTypeAll' \
  "$unity_assets/Domain" \
  "$unity_assets/Networking" \
  "$unity_assets/Input" \
  "$unity_assets/Presentation" \
  "$unity_assets/UI" \
  --glob '*.cs'; then
  echo "Runtime scene searches are forbidden. Use injection or serialized scene adapters." >&2
  exit 1
fi

echo "Unity assembly boundaries and runtime composition checks passed."
