#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_root="$project_root/apps/game-unity"
project_version="$unity_root/ProjectSettings/ProjectVersion.txt"
manifest="$unity_root/Packages/manifest.json"

test -f "$project_version"
test -f "$manifest"
test -f "$unity_root/Assets/Scripts/SeaConnectionController.cs"
test -f "$unity_root/Assets/Scripts/SeaConnectionRecoveryPolicy.cs"
test -f "$unity_root/Assets/Scripts/SeaAuthTokenStore.cs"
test -f "$unity_root/Assets/Scripts/SeaGameController.cs"
test -f "$unity_root/Assets/Scripts/SeaShipMotion.cs"
test -f "$unity_root/Assets/Scripts/SeaShipVisualFactory.cs"
test -f "$unity_root/Assets/Scripts/SeaSubscriptionPlan.cs"
test -f "$unity_root/Assets/Scripts/SeaChartCoordinates.cs"
test -f "$unity_root/Assets/Scripts/SeaChartCameraController.cs"
test -f "$unity_root/Assets/Scripts/SeaWorldView.cs"
test -f "$unity_root/Assets/Art/Ships/StarterShip/StarterShip.fbx"
test -f "$project_root/packages/spacetimedb-unity/src/UnityTcpWebSocket.cs"
test -f "$unity_root/Assets/Generated/SpacetimeDB/SpacetimeDBClient.g.cs"

grep -q '^m_EditorVersion: 6000\.3\.23f1$' "$project_version"
grep -q 'com.clockworklabs.spacetimedbsdk.*file:../../../packages/spacetimedb-unity' "$manifest"
grep -q '^using System.Collections;$' "$project_root/packages/spacetimedb-unity/src/SpacetimeDBClient.cs"
grep -q 'DbConnection\.Builder' "$unity_root/Assets/Scripts/SeaConnectionController.cs"
grep -q 'Reducers\.SetCourse' "$unity_root/Assets/Scripts/SeaGameController.cs"
grep -q 'Reducers\.StopCourse' "$unity_root/Assets/Scripts/SeaGameController.cs"
grep -q 'Reducers\.Engage' "$unity_root/Assets/Scripts/SeaGameController.cs"

if grep -R -q --include='*.cs' 'SubscribeToAllTables' "$unity_root/Assets/Scripts"; then
  echo "Runtime Unity code must use scoped subscriptions." >&2
  exit 1
fi

echo "Unity source checks passed for $(grep '^m_EditorVersion:' "$project_version")"
