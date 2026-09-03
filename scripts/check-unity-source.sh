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
test -f "$unity_root/Assets/Domain/SeaSnapshotClock.cs"
test -f "$unity_root/Assets/Domain/SeaMotionTimeline.cs"
test -f "$unity_root/Assets/Presentation/SeaShipVisualFactory.cs"
test -f "$unity_root/Assets/Presentation/SeaShipFeedback.cs"
test -f "$unity_root/Assets/Presentation/SeaWorldGeometryFactory.cs"
test -f "$unity_root/Assets/Domain/SeaSubscriptionPlan.cs"
test -f "$unity_root/Assets/Domain/SeaSpatialInterest.cs"
test -f "$unity_root/Assets/Domain/SeaSubscriptionGeneration.cs"
test -f "$unity_root/Assets/Networking/SeaConnectionSubscriptions.cs"
test -f "$unity_root/Assets/Networking/SeaConnectionClientState.cs"
test -f "$unity_root/Assets/Presentation/SeaVisibilityDistanceJob.cs"
test -f "$unity_root/Assets/Presentation/SeaShipPresentation.cs"
test -f "$unity_root/Assets/Presentation/SeaOwnedAssetCatalog.cs"
test -f "$unity_root/Assets/Presentation/SeaOwnedAssetLease.cs"
test -f "$unity_root/Assets/Presentation/SeaWorldView.Assets.cs"
test -f "$unity_root/Assets/Domain/SeaOwnedAssetPolicy.cs"
test -f "$unity_root/Assets/Domain/SeaKeyedBoundedPool.cs"
test -f "$unity_root/Assets/Editor/SeaOwnedAssetEditorLifecycle.cs"
test -f "$unity_root/Assets/Editor/SeaOwnedAssetValidator.cs"
test -f "$unity_root/Assets/Domain/SeaChartCoordinates.cs"
test -f "$unity_root/Assets/Presentation/SeaChartCameraController.cs"
test -f "$unity_root/Assets/Input/SeaInputController.cs"
test -f "$unity_root/Assets/UI/SeaHudController.cs"
test -f "$unity_root/Assets/UI/SeaHudSnapshotReader.cs"
test -f "$unity_root/Assets/Domain/SeaHudViewModel.cs"
test -f "$unity_root/Assets/Domain/SeaTacticalPresentationRules.cs"
test -f "$unity_root/Assets/Domain/SeaRuntimeValidationRules.cs"
test -f "$unity_root/Assets/Presentation/SeaWorldView.cs"
test -f "$unity_root/Assets/Input/SeaControls.inputactions"
test -f "$unity_root/Assets/UI/SeaHud.uxml"
test -f "$unity_root/Assets/UI/SeaHud.uss"
test -f "$unity_root/Assets/Art/SeaOwnedAssets.asset"
test -f "$unity_root/Assets/AddressableAssetsData/AddressableAssetSettings.asset"
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
# Every ShipCommand variant the module publishes must be issued by runtime client code. The
# variant list comes from the generated bindings, so a new or renamed command fails here.
ship_commands="$(sed -n 's/^ *[A-Za-z]*Command \([A-Za-z]*\),\{0,1\}$/\1/p' "$unity_root/Assets/Generated/SpacetimeDB/Types/ShipCommand.g.cs")"
if [ -z "$ship_commands" ]; then
  echo "No ShipCommand variants found in the generated bindings." >&2
  exit 1
fi
for command in $ship_commands; do
  if ! grep -R -q --include='*.cs' --exclude-dir=Generated --exclude-dir=Tests "new ShipCommand\.$command" "$unity_root/Assets"; then
    echo "Runtime Unity code never issues ShipCommand.$command." >&2
    exit 1
  fi
done
if grep -R -q --include='*.cs' -E 'Reducers\.(SetCourse|StopCourse|SelectTarget|ClearTarget|SetAmmo|FireBroadside|ActivateAbility|StartRepair|StartBoarding|CancelRepair|CancelBoarding|MoveTo)' \
  "$unity_root/Assets/Networking" "$unity_root/Assets/Presentation"; then
  echo "Runtime Unity code must use IssueShipCommand for gameplay." >&2
  exit 1
fi
grep -q 'FindActionMap("Gameplay"' "$unity_root/Assets/Input/SeaInputController.cs"
grep -q 'name="fire-control"' "$unity_root/Assets/UI/SeaHud.uxml"

if grep -R -q --include='*.cs' 'SubscribeToAllTables' \
  "$unity_root/Assets/Networking" "$unity_root/Assets/Presentation"; then
  echo "Runtime Unity code must use scoped subscriptions." >&2
  exit 1
fi
if grep -q '\.Iter()' \
  "$unity_root"/Assets/Presentation/SeaWorldView*.cs \
  "$unity_root/Assets/UI/SeaHudController.cs" \
  "$unity_root/Assets/UI/SeaHudSnapshotReader.cs"; then
  echo "World presentation and HUD updates must use row callbacks, not table iteration." >&2
  exit 1
fi

grep -q 'ProfilerMarker' "$unity_root/Assets/Networking/SeaConnectionController.cs"
grep -q 'ProfilerMarker' "$unity_root/Assets/Presentation/SeaWorldView.cs"
grep -q 'ProfilerMarker' "$unity_root/Assets/UI/SeaHudEvents.cs"
grep -q 'SeaVisibilityDistanceJob' "$unity_root/Assets/Presentation/SeaWorldView.Rows.cs"
grep -q 'MaterialPropertyBlock' "$unity_root/Assets/Presentation/SeaShipPresentation.cs"
grep -q 'SeaKeyedBoundedPool<SeaOwnedShipRole, GameObject>' \
  "$unity_root/Assets/Presentation/SeaWorldView.Assets.cs"
grep -q 'Addressables.LoadAssetAsync' \
  "$unity_root/Assets/Presentation/SeaOwnedAssetLease.cs"
grep -q 'Addressables.Release' \
  "$unity_root/Assets/Presentation/SeaOwnedAssetLease.cs"
grep -q 'SeaDirtyState' "$unity_root/Assets/UI/SeaHudEvents.cs"
grep -q -- '-seaPresentationPerformanceTest' \
  "$unity_root"/Assets/Presentation/SeaRuntimeValidationProbe*.cs
grep -q 'SeedSyntheticPerformanceFleet(requiredShipCount)' \
  "$unity_root"/Assets/Presentation/SeaRuntimeValidationProbe*.cs
grep -q 'ShouldRestoreSyntheticFleet' \
  "$unity_root"/Assets/Presentation/SeaRuntimeValidationProbe*.cs
grep -q 'test-unity-presentation-performance.sh' "$project_root/scripts/verify-unity.sh"

if grep -R -q --include='*.cs' -E 'void OnGUI\(|Input\.Get' \
  "$unity_root/Assets/Domain" "$unity_root/Assets/Networking" \
  "$unity_root/Assets/Input" "$unity_root/Assets/Presentation" "$unity_root/Assets/UI"; then
  echo "Runtime Unity code must use UI Toolkit and the Input System exclusively." >&2
  exit 1
fi

echo "Unity source checks passed for $(grep '^m_EditorVersion:' "$project_version")"
