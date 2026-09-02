#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
schema="$repo_root/server/spacetimedb/spacetimedb/Schema/Tables.cs"
simulation="$repo_root/server/spacetimedb/spacetimedb/Simulation"
navigation="$repo_root/server/spacetimedb/spacetimedb/Navigation/NavigationState.cs"

for required in \
  'Accessor = "ByStatusDue"' \
  'Accessor = "ByImpactDue"' \
  'Accessor = "ByChannelDue"' \
  'Accessor = "ByLootExpiryDue"' \
  'Accessor = "ByRespawnDue"' \
  'Accessor = "ByEnvironmentExposure"' \
  'Accessor = "ByActiveChunkShard"' \
  'Accessor = "ByEnvironmentExposureHazardShard"' \
  'Accessor = "ByMovingShard"' \
  'Accessor = "ByShipCooldown"'; do
  if ! grep -q "$required" "$schema"; then
    echo "Missing indexed simulation contract: $required" >&2
    exit 1
  fi
done

if grep -R -n --include='*.cs' 'WorldObject.Iter()\|CurrentZone.Iter()' "$simulation"; then
  echo "Simulation hot paths must query world state by spatial index." >&2
  exit 1
fi

if grep -n 'IsNavigablePosition' "$simulation/SailingSystem.cs" | grep -q 'moved\|nextX\|nextY'; then
  echo "Movement ticks must use course-time navigation blockers." >&2
  exit 1
fi

if grep -R -n --include='*.cs' 'Ship.ByActive.Filter(true)' "$simulation"; then
  echo "Simulation hot paths must not scan every active ship." >&2
  exit 1
fi

if grep -n 'Ship.ByActive.Filter' "$navigation"; then
  echo "Player load and respawn must not scan or block on other ships." >&2
  exit 1
fi

if grep -R -n --include='*.cs' 'Loot.ByActive.Filter(true)' "$simulation"; then
  echo "Simulation hot paths must not scan every active loot row." >&2
  exit 1
fi

if grep -R -n --include='*.cs' 'ExpireTransientRows' "$simulation"; then
  echo "Persisted transient event cleanup must be removed." >&2
  exit 1
fi

echo "Server simulation uses indexed due and spatial work."
