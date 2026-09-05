#!/usr/bin/env sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
# The whole schema directory, not one file: the Ship table lives in ShipTable.cs so that
# neither half runs past the 500-line limit, and the contract is the same either way.
schema="$repo_root/server/spacetimedb/spacetimedb/Schema"
simulation="$repo_root/server/spacetimedb/spacetimedb/Simulation"
navigation="$repo_root/server/spacetimedb/spacetimedb/Navigation/NavigationState.cs"

for required in \
  'Accessor = "ByEffectDue"' \
  'Accessor = "ByVolleyExpiry"' \
  'Accessor = "ByReloading"' \
  'Accessor = "ByChannelDue"' \
  'Accessor = "ByLootExpiryDue"' \
  'Accessor = "ByRespawnDue"' \
  'Accessor = "ByEnvironmentExposure"' \
  'Accessor = "ByActiveFaction"' \
  'Accessor = "ByShipCooldown"'; do
  if ! grep -R -q --include='*.cs' "$required" "$schema"; then
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

if grep -n 'ActiveShipsIn' "$simulation/LootSystem.cs"; then
  echo "Loot claims must rank rivals by published kinematics, not stale Ship rows." >&2
  exit 1
fi

if grep -R -n --include='*.cs' 'ExpireTransientRows' "$simulation"; then
  echo "Persisted transient event cleanup must be removed." >&2
  exit 1
fi

echo "Server simulation uses indexed due and spatial work."
