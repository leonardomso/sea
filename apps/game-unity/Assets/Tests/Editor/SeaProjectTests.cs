#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sea.Client;
using SpacetimeDB;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaProjectTests
    {
        [Test]
        public void MainScene_is_enabled_in_build_settings()
        {
            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            Assert.That(scenes, Has.Length.EqualTo(1));
            Assert.That(scenes[0], Is.EqualTo("Assets/Scenes/Main.unity"));
        }

        [Test]
        public void Standalone_player_keeps_network_recovery_running_when_unfocused()
        {
            Assert.That(PlayerSettings.runInBackground, Is.True);
        }

        [Test]
        public void Standalone_player_uses_a_safe_resizable_window()
        {
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(PlayerSettings.resizableWindow, Is.True);
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1280));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(720));
        }

        [Test]
        public void Runtime_frame_policy_caps_foreground_and_background_work()
        {
            Assert.That(SeaFrameRatePolicy.TargetForFocus(true), Is.EqualTo(60));
            Assert.That(SeaFrameRatePolicy.TargetForFocus(false), Is.EqualTo(15));
        }

        [Test]
        public void Generated_spacetime_bindings_are_present()
        {
            Assert.That(File.Exists("Assets/Generated/SpacetimeDB/SpacetimeDBClient.g.cs"), Is.True);
        }

        [Test]
        public void Starter_ship_model_is_imported_for_runtime_use()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/StarterShip/StarterShip.fbx");

            Assert.That(shipModel, Is.Not.Null);
            Assert.That(shipModel.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }

        [Test]
        public void Starter_ship_model_has_a_game_ready_triangle_budget()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/StarterShip/StarterShip.fbx");
            var triangleCount = shipModel.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Sum(mesh => Enumerable.Range(0, mesh.subMeshCount)
                    .Sum(subMesh => (long)mesh.GetIndexCount(subMesh) / 3));

            Assert.That(triangleCount, Is.LessThanOrEqualTo(30_000),
                "The starter ship exceeds its 30,000-triangle runtime budget.");
        }

        [Test]
        public void Starter_ship_visual_never_uses_an_all_white_fallback()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/StarterShip/StarterShip.fbx");
            var ship = SeaShipVisualFactory.Create(shipModel, "Colored Ship", 10f);
            var materials = ship.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToArray();
            var hasTextureOrColor = materials.Any(material =>
            {
                var color = material.color;
                var channelRange = Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b);
                return material.mainTexture != null || channelRange > 0.05f;
            });

            Assert.That(hasTextureOrColor, Is.True,
                "The runtime ship visual only contains white fallback materials.");
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Starter_ship_visual_uses_the_imported_model_at_a_readable_scale()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/StarterShip/StarterShip.fbx");

            var ship = SeaShipVisualFactory.Create(shipModel, "Test Ship", 10f);
            var bounds = SeaShipVisualFactory.CalculateRendererBounds(ship);
            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);

            Assert.That(ship.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(footprint, Is.EqualTo(10f).Within(0.05f));
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Ship_motion_advances_and_turns_over_multiple_frames()
        {
            var ship = new GameObject("Moving Ship");

            SeaShipMotion.Step(
                ship.transform,
                new Vector3(0f, 0f, 10f),
                deltaTime: 0.1f,
                movementSpeed: 5f,
                turnSpeedDegrees: 90f,
                modelYawOffset: -90f);

            Assert.That(ship.transform.position.z, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, ship.transform.rotation),
                Is.EqualTo(9f).Within(0.1f));
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Main_scene_references_the_starter_ship_model()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var world = Object.FindFirstObjectByType<SeaWorldView>();
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/StarterShip/StarterShip.fbx");

            Assert.That(world, Is.Not.Null);
            Assert.That(world.ShipModel, Is.SameAs(shipModel));
        }

        [Test]
        public void Main_scene_uses_a_readable_two_point_five_d_camera_angle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var worldCamera = Camera.main;

            Assert.That(worldCamera, Is.Not.Null);
            Assert.That(worldCamera.transform.eulerAngles.x, Is.EqualTo(55f).Within(0.1f));
            Assert.That(worldCamera.orthographicSize, Is.EqualTo(45f).Within(0.1f));
        }

        [Test]
        public void Main_scene_applies_the_frame_rate_policy()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            Assert.That(Object.FindFirstObjectByType<SeaFrameRateController>(), Is.Not.Null);
        }

        [Test]
        public void Auth_token_store_can_clear_a_stale_local_identity()
        {
            const string testKey = "sea.tests.identity-token";
            var tokens = new SeaAuthTokenStore(testKey);
            tokens.Save("stale-token");

            tokens.Clear();

            Assert.That(tokens.Token, Is.Empty);
        }

        [Test]
        public void Unauthorized_cached_identity_is_cleared_and_retried_anonymously()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(401, "Unauthorized"),
                attemptedWithToken: true,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.ClearIdentityAndRetry));
            Assert.That(decision.DelaySeconds, Is.Zero);
        }

        [Test]
        public void Unauthorized_anonymous_connection_stops_retrying()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(401, "Unauthorized"),
                attemptedWithToken: false,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.Stop));
        }

        [Test]
        public void Transient_connection_failures_use_bounded_backoff()
        {
            var first = SeaConnectionRecoveryPolicy.Decide(
                new System.TimeoutException("offline"),
                attemptedWithToken: false,
                transientFailureCount: 0);
            var repeated = SeaConnectionRecoveryPolicy.Decide(
                new System.TimeoutException("offline"),
                attemptedWithToken: false,
                transientFailureCount: 20);

            Assert.That(first.Action, Is.EqualTo(SeaConnectionRecoveryAction.RetryAfterDelay));
            Assert.That(first.DelaySeconds, Is.EqualTo(2f));
            Assert.That(repeated.DelaySeconds, Is.EqualTo(30f));
        }

        [Test]
        public void Missing_database_is_a_permanent_connection_failure()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(404, "Not Found"),
                attemptedWithToken: false,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.Stop));
        }

        [Test]
        public void World_material_factory_always_returns_a_runtime_material()
        {
            var material = SeaMaterialFactory.Create(Color.white);

            Assert.That(material, Is.Not.Null);
            Object.DestroyImmediate(material);
        }

    }
}
#endif
