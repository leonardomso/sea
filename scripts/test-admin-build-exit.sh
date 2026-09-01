#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
timeout_seconds=${ADMIN_BUILD_TIMEOUT_SECONDS:-120}
poll_seconds=1
log_file=$(mktemp -t sea-admin-build.XXXXXX)

cleanup() {
  rm -f "$log_file"
}
trap cleanup EXIT INT TERM

(
  cd "$repo_root/apps/admin"
  "$repo_root/apps/admin/node_modules/.bin/vite" build >"$log_file" 2>&1
) &
build_pid=$!
elapsed=0

while kill -0 "$build_pid" 2>/dev/null; do
  if [ "$elapsed" -ge "$timeout_seconds" ]; then
    kill "$build_pid" 2>/dev/null || true
    wait "$build_pid" 2>/dev/null || true
    cat "$log_file"
    echo "Admin production build did not exit within ${timeout_seconds}s." >&2
    exit 1
  fi

  sleep "$poll_seconds"
  elapsed=$((elapsed + poll_seconds))
done

if ! wait "$build_pid"; then
  cat "$log_file"
  exit 1
fi

cat "$log_file"
test -f "$repo_root/apps/admin/.output/server/index.mjs"
