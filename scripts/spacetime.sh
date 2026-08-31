#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
spacetime_image=${SPACETIME_IMAGE:-clockworklabs/spacetime:latest}

exec docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$spacetime_image" \
  --root-dir /tmp/sea-spacetime \
  --config-path /tmp/sea-spacetime/cli.toml \
  "$@"
