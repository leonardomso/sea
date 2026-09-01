#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
spacetime_image=${SPACETIME_IMAGE:-clockworklabs/spacetime@sha256:31f6d1d754f821362cec7e39e3680dd2efeb7a57f3ed0eb36c4cca1f12f46bb3}
wasi_sdk_version=24

case "$(uname -m)" in
  arm64 | aarch64)
    wasi_arch=arm64
    wasi_sha256=ae6c1417ea161e54bc54c0a168976af57a0c6e53078857886057a71a0d928646
    ;;
  x86_64 | amd64)
    wasi_arch=x86_64
    wasi_sha256=c6c38aab56e5de88adf6c1ebc9c3ae8da72f88ec2b656fb024eda8d4167a0bc5
    ;;
  *)
    echo "Unsupported host architecture for the WASI SDK: $(uname -m)" >&2
    exit 2
    ;;
esac

wasi_cache_relative=.cache/wasi-sdk-${wasi_sdk_version}-linux-${wasi_arch}
wasi_cache="$repo_root/$wasi_cache_relative"
if [ ! -x "$wasi_cache/bin/clang" ]; then
  mkdir -p "$wasi_cache"
  wasi_archive=$(mktemp -t sea-wasi-sdk.XXXXXX)
  curl --fail --location --retry 3 \
    --output "$wasi_archive" \
    "https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-${wasi_sdk_version}/wasi-sdk-${wasi_sdk_version}.0-${wasi_arch}-linux.tar.gz"
  printf '%s  %s\n' "$wasi_sha256" "$wasi_archive" | shasum -a 256 --check
  tar -xf "$wasi_archive" -C "$wasi_cache" --strip-components=1
  rm -f "$wasi_archive"
fi

spacetime_root=/tmp/sea-spacetime
spacetime_config=/tmp/sea-spacetime/cli.toml
if [ -n "${SPACETIME_STATE_RELATIVE:-}" ]; then
  case "$SPACETIME_STATE_RELATIVE" in
    .cache/spacetime-*) ;;
    *)
      echo "SPACETIME_STATE_RELATIVE must be a .cache/spacetime-* path." >&2
      exit 2
      ;;
  esac
  mkdir -p "$repo_root/$SPACETIME_STATE_RELATIVE"
  spacetime_root="/workspace/$SPACETIME_STATE_RELATIVE"
  spacetime_config="$spacetime_root/cli.toml"
fi

exec docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp/dotnet-cli \
  -e "WASI_SDK_PATH=/workspace/$wasi_cache_relative" \
  -v "$repo_root:/workspace" \
  -w /workspace \
  "$spacetime_image" \
  --root-dir "$spacetime_root" \
  --config-path "$spacetime_config" \
  "$@"
