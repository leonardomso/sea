#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtime_directory="${SEA_LOCAL_CLIENT_STATE_DIR:-$project_root/.cache/local-clients}"
pid_file="$runtime_directory/pids"

if [ ! -f "$pid_file" ]; then
  echo "No managed local clients are running."
  exit 0
fi

while read -r pid; do
  if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null; then
    kill -TERM "$pid"
  fi
done <"$pid_file"

rm -f "$pid_file"
echo "Stopped the managed local clients."
