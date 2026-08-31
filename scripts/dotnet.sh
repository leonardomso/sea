#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
dotnet_image=${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:latest}

exec docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_ROLL_FORWARD=Major \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$dotnet_image" \
  dotnet \
  "$@"
