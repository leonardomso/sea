#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
. "$repo_root/scripts/lib/local-ports.sh"

exec "$repo_root/scripts/spacetime.sh" publish sea-local \
  --server "$SEA_SPACETIME_DOCKER_URL" \
  --yes \
  --module-path server/spacetimedb/spacetimedb \
  "$@"
