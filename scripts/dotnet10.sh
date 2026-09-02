#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
dotnet_image=${DOTNET10_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a}
network_option=
if [ -n "${SEA_DOCKER_NETWORK:-}" ]; then
  network_option="--network=$SEA_DOCKER_NETWORK"
fi

# SEA_DOCKER_NETWORK is an internally generated Docker network name.
# shellcheck disable=SC2086
exec docker run --rm \
  $network_option \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -e SEA_LOAD_CLIENTS \
  -e SEA_LOAD_ACTIVE_CLIENTS \
  -e SEA_LOAD_DATABASE \
  -e SEA_LOAD_EVIDENCE \
  -e SEA_LOAD_RAMP_SECONDS \
  -e SEA_LOAD_REPORT_DIRECTORY \
  -e SEA_LOAD_SECONDS \
  -e SEA_LOAD_SETUP_SECONDS \
  -e SEA_LOAD_SERVER \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$dotnet_image" \
  dotnet \
  "$@"
