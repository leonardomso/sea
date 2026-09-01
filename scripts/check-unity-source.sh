#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_root="$project_root/apps/game-unity"
project_version="$unity_root/ProjectSettings/ProjectVersion.txt"
manifest="$unity_root/Packages/manifest.json"

test -f "$project_version"
test -f "$manifest"
test -f "$unity_root/Assets/Networking/SeaConnectionController.cs"
test -f "$unity_root/Assets/Domain/SeaConnectionRecoveryPolicy.cs"
test -f "$unity_root/Assets/Domain/SeaCommandResultText.cs"
test -f "$unity_root/Assets/Networking/SeaAuthTokenStore.cs"
test -f "$unity_root/Assets/Presentation/SeaGameController.cs"
test -f "$unity_root/Assets/Domain/SeaShipMotion.cs"
test -f "$unity_root/Assets/Presentation/SeaShipVisualFactory.cs"
test -f "$unity_root/Assets/Presentation/SeaShipFeedback.cs"
test -f "$unity_root/Assets/Presentation/SeaWorldGeometryFactory.cs"
test -f "$unity_root/Assets/Domain/SeaSubscriptionPlan.cs"
test -f "$unity_root/Assets/Domain/SeaChartCoordinates.cs"
test -f "$unity_root/Assets/Presentation/SeaChartCameraController.cs"
test -f "$unity_root/Assets/Input/SeaInputController.cs"
test -f "$unity_root/Assets/UI/SeaHudController.cs"
test -f "$unity_root/Assets/Domain/SeaHudViewModel.cs"
test -f "$unity_root/Assets/Domain/SeaTacticalPresentationRules.cs"
test -f "$unity_root/Assets/Domain/SeaRuntimeValidationRules.cs"
test -f "$unity_root/Assets/Presentation/SeaWorldView.cs"
test -f "$unity_root/Assets/Input/SeaControls.inputactions"
test -f "$unity_root/Assets/UI/SeaHud.uxml"
test -f "$unity_root/Assets/UI/SeaHud.uss"
test -f "$unity_root/Assets/Art/Ships/Apricum/Apricum.fbx"
test -f "$unity_root/Assets/Art/Ships/Apricum/Textures/Apricum_BaseColor.png"
test -f "$unity_root/Assets/Art/Ships/Apricum/Textures/Apricum_Normal.png"
test -f "$unity_root/Assets/Art/Ships/Apricum/Textures/Apricum_MetallicSmoothness.png"
test -f "$unity_root/Assets/Shaders/SeaChartWater.shader"
test -f "$project_root/packages/spacetimedb-unity/src/UnityTcpWebSocket.cs"
test -f "$unity_root/Assets/Generated/SpacetimeDB/SpacetimeDBClient.g.cs"

grep -q '^m_EditorVersion: 6000\.3\.23f1$' "$project_version"
grep -q 'com.clockworklabs.spacetimedbsdk.*file:../../../packages/spacetimedb-unity' "$manifest"
grep -q 'com.unity.inputsystem.*1.15.0' "$manifest"
grep -q '^  activeInputHandler: 1$' "$unity_root/ProjectSettings/ProjectSettings.asset"
grep -q '^using System.Collections;$' "$project_root/packages/spacetimedb-unity/src/SpacetimeDBClient.cs"
grep -q 'DbConnection\.Builder' "$unity_root/Assets/Networking/SeaConnectionController.cs"
grep -q 'Reducers\.IssueShipCommand' "$unity_root/Assets/Networking/SeaConnectionController.cs"
grep -q 'new ShipCommand\.SetCourse' "$unity_root/Assets/Presentation/SeaGameController.cs"
grep -q 'new ShipCommand\.FireBroadside' "$unity_root/Assets/Presentation/SeaGameController.cs"
grep -q 'new ShipCommand\.ActivateAbility' "$unity_root/Assets/Presentation/SeaGameController.cs"
grep -q 'new ShipCommand\.StartRepair' "$unity_root/Assets/Presentation/SeaGameController.cs"
grep -q 'new ShipCommand\.StartBoarding' "$unity_root/Assets/Presentation/SeaGameController.cs"
if grep -R -q --include='*.cs' -E 'Reducers\.(SetCourse|StopCourse|SelectTarget|ClearTarget|SetAmmo|FireBroadside|ActivateAbility|StartRepair|StartBoarding|CancelRepair|CancelBoarding|MoveTo)' \
  "$unity_root/Assets/Networking" "$unity_root/Assets/Presentation"; then
  echo "Runtime Unity code must use IssueShipCommand for gameplay." >&2
  exit 1
fi
grep -q 'FindActionMap("Gameplay"' "$unity_root/Assets/Input/SeaInputController.cs"
grep -q 'name="port-broadside"' "$unity_root/Assets/UI/SeaHud.uxml"

if grep -R -q --include='*.cs' 'SubscribeToAllTables' \
  "$unity_root/Assets/Networking" "$unity_root/Assets/Presentation"; then
  echo "Runtime Unity code must use scoped subscriptions." >&2
  exit 1
fi

if grep -R -q --include='*.cs' -E 'void OnGUI\(|Input\.Get' \
  "$unity_root/Assets/Domain" "$unity_root/Assets/Networking" \
  "$unity_root/Assets/Input" "$unity_root/Assets/Presentation" "$unity_root/Assets/UI"; then
  echo "Runtime Unity code must use UI Toolkit and the Input System exclusively." >&2
  exit 1
fi

echo "Unity source checks passed for $(grep '^m_EditorVersion:' "$project_version")"
