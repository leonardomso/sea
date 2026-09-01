#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_binary="$project_root/apps/game-unity/Build/Sea.app/Contents/MacOS/game-unity"
database_name="${SEA_LOCAL_CLIENT_DATABASE:-sea-local}"
runtime_directory="${SEA_LOCAL_CLIENT_STATE_DIR:-$project_root/.cache/local-clients}"
pid_file="$runtime_directory/pids"

if [ ! -x "$game_binary" ]; then
  echo "Build the macOS player before launching local clients." >&2
  exit 2
fi

mkdir -p "$runtime_directory"
managed_clients_running=false
if [ -f "$pid_file" ]; then
  while read -r pid; do
    if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null; then
      managed_clients_running=true
      break
    fi
  done <"$pid_file"
fi
if [ "$managed_clients_running" = true ]; then
  echo "A managed local client set is already running." >&2
  exit 1
fi

: >"$pid_file"
profiles=(captain-1 captain-2 captain-3 captain-4)
for index in "${!profiles[@]}"; do
  profile="${profiles[$index]}"
  arguments=(
    -seaProfile "$profile"
    -seaDatabaseName "$database_name"
    -logFile "$runtime_directory/$profile.log"
  )
  if [ "$index" -eq 0 ]; then
    arguments+=(
      -screen-fullscreen 0
      -screen-width 1280
      -screen-height 720
    )
  else
    arguments+=( -batchmode -nographics )
  fi

  nohup "$game_binary" "${arguments[@]}" >/dev/null 2>&1 &
  echo "$!" >>"$pid_file"
done

echo "Launched one visible client and three headless clients against $database_name."
