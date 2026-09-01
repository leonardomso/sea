#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
dotnet_image=${DOTNET10_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a}

exec docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$dotnet_image" \
  dotnet \
  "$@"
