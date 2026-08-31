# SpacetimeDB server module

This directory contains the C# SpacetimeDB module for the local validation build.

The module owns player identity, ship state, map entities, resources, events, intent reducers, and the fixed-tick simulation skeleton. `seed/world.json` documents the deterministic starter map; the compiled module seed is intentionally deterministic because SpacetimeDB reducers cannot read files at runtime.

The repository follows the current SpacetimeDB 2.x Docker image and matching 2.x C# runtime package. The module targets .NET 8 because the current C# quickstart supports it and the .NET 10 WASI workflow is not supported on macOS. The CLI can be run from the same image while the host toolchain is being finalized:

```sh
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp \
  -v "$PWD:/workspace" -w /workspace \
  clockworklabs/spacetime:latest \
  build --module-path server/spacetimedb/spacetimedb
```
