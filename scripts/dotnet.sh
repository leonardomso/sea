#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
dotnet_image=${DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:8.0.424@sha256:bb32ba3ba3ea36e38572d9d8db76fa15f7cbf722f3f886e06bca6d528bd4fba8}

exec docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -e SEA_TEST_DATABASE \
  -e SEA_TEST_SERVER \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$dotnet_image" \
  dotnet \
  "$@"
