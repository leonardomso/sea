#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sea.Client;
using SpacetimeDB;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
        public void Main_scene_keeps_chart_camera_controls_separate_from_ship_controls()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            Assert.That(Object.FindFirstObjectByType<SeaChartCameraController>(), Is.Not.Null);
        }

        [Test]
        public void Initial_subscription_plan_is_owner_scoped_and_never_unrestricted()
        {
            var queries = SeaSubscriptionPlan.Initial("0xabc123");

            Assert.That(queries, Does.Contain("SELECT * FROM player_ownership WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain("SELECT * FROM world_state"));
            Assert.That(queries.Any(query => query == "SELECT * FROM ship"), Is.False);
        }

        [Test]
        public void Spatial_subscription_plan_is_bounded_to_nearby_chunks_and_active_rows()
        {
            var queries = SeaSubscriptionPlan.Spatial(chunkX: 4, chunkY: 2, radius: 1);

            Assert.That(queries, Has.Some.Contains("chunk_x >= 3"));
            Assert.That(queries, Has.Some.Contains("chunk_x <= 5"));
            Assert.That(queries, Has.Some.Contains("chunk_y >= 1"));
            Assert.That(queries, Has.Some.Contains("chunk_y <= 3"));
            Assert.That(queries.All(query => query.Contains("is_active = true")), Is.True);
        }

        [Test]
        public void Client_chart_coordinates_match_the_server_contract()
        {
            Assert.That(SeaChartCoordinates.TryCellCenter("AX 59", out var center), Is.True);
            Assert.That(center.Column, Is.EqualTo(49));
            Assert.That(center.Row, Is.EqualTo(59));
            Assert.That(SeaChartCoordinates.LabelAt(center.X, center.Y), Is.EqualTo("AX 59"));
        }

        [Test]
        public void Chart_camera_rules_clamp_zoom_and_do_not_issue_ship_commands()
        {
            Assert.That(SeaChartCameraRules.ClampZoom(5f), Is.EqualTo(20f));
            Assert.That(SeaChartCameraRules.ClampZoom(100f), Is.EqualTo(80f));
            Assert.That(SeaChartCameraRules.PanDelta(1f, -1f, 20f, 0.5f),
                Is.EqualTo(new Vector3(10f, 0f, -10f)));
        }

        [Test]
        public void Gameplay_input_map_exposes_every_locked_navigation_and_combat_command()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions");

            Assert.That(controls, Is.Not.Null);
            var gameplay = controls.FindActionMap("Gameplay", throwIfNotFound: true);
            var requiredActions = new[]
            {
                "Point", "SetCourse", "StopCourse", "PanChart", "ZoomChart", "RecenterChart",
                "OpenNavigator", "CycleTargetNext", "CycleTargetPrevious", "ClearTarget", "Pause",
                "FirePort", "FireStarboard", "AimHull", "AimSails", "AimCannons", "AmmoRound",
                "AmmoChain", "AmmoGrapeshot", "AmmoIncendiary", "FullSail", "Brace",
                "EmergencyPump", "SmokeScreen", "Repair", "Board",
            };

            Assert.That(gameplay.actions.Select(action => action.name), Is.EquivalentTo(requiredActions));
            Assert.That(controls.FindActionMap("Menu", throwIfNotFound: true), Is.Not.Null);
        }

        [Test]
        public void Opening_the_chart_menu_blocks_gameplay_without_pausing_the_world()
        {
            var controls = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions"));
            var host = new GameObject("Input mode test");
            var input = host.AddComponent<SeaInputController>();
            var originalTimeScale = Time.timeScale;

            input.Configure(controls);
            input.SetMenuOpen(true);

            Assert.That(input.IsMenuOpen, Is.True);
            Assert.That(controls.FindActionMap("Gameplay").enabled, Is.False);
            Assert.That(controls.FindActionMap("Menu").enabled, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(controls);
        }

        [Test]
        public void Every_player_command_exposes_a_rebindable_keyboard_or_mouse_binding()
        {
            var controls = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Input/SeaControls.inputactions"));
            var host = new GameObject("Rebind test");
            var input = host.AddComponent<SeaInputController>();

            input.Configure(controls);
            var bindings = input.GetRebindableBindings();

            var required = controls.FindActionMap("Gameplay").actions
                .Where(action => action.name != "Point")
                .Select(action => action.name)
                .ToArray();
            Assert.That(bindings.Select(binding => binding.ActionName).Distinct(), Is.SupersetOf(required));
            Assert.That(bindings.All(binding => !string.IsNullOrWhiteSpace(binding.DisplayPath)), Is.True);
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(controls);
        }

        [Test]
        public void Combat_hud_view_model_formats_player_target_and_reload_state()
        {
            var model = SeaHudViewModel.From(new SeaHudSnapshot
            {
                IsReady = true,
                Coordinate = "AX 59",
                HeadingDegrees = 275f,
                Speed = 12.5f,
                Hull = 750,
                MaxHull = 1000,
                Experience = 1250,
                CurrentLevelExperience = 1000,
                NextLevelExperience = 2000,
                TargetName = "RAIDER 7",
                TargetHull = 300,
                TargetMaxHull = 600,
                TargetSails = 100,
                TargetMaxSails = 400,
                TargetCannons = 50,
                TargetMaxCannons = 200,
                PortReloadRemainingSeconds = 2f,
                ReloadDurationSeconds = 4f,
                StarboardReloadRemainingSeconds = 0f,
            });

            Assert.That(model.HullProgress, Is.EqualTo(0.75f));
            Assert.That(model.ExperienceProgress, Is.EqualTo(0.25f));
            Assert.That(model.HullText, Is.EqualTo("750 / 1,000"));
            Assert.That(model.NavigationText, Is.EqualTo("AX 59  •  275°  •  12.5 KN"));
            Assert.That(model.HasTarget, Is.True);
            Assert.That(model.TargetHullProgress, Is.EqualTo(0.5f));
            Assert.That(model.TargetSailsProgress, Is.EqualTo(0.25f));
            Assert.That(model.TargetCannonsProgress, Is.EqualTo(0.25f));
            Assert.That(model.PortReloadProgress, Is.EqualTo(0.5f));
            Assert.That(model.StarboardReady, Is.True);
        }

        [Test]
        public void Runtime_hud_contains_the_locked_chart_combat_instruments()
        {
            var document = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SeaHud.uxml");
            Assert.That(document, Is.Not.Null);
            var root = document.CloneTree();

            var requiredElements = new[]
            {
                "connection-status", "navigation-readout", "player-hull", "player-experience",
                "target-frame", "target-hull", "target-sails", "target-cannons",
                "port-broadside", "starboard-broadside", "weak-point-rail", "ammo-rail",
                "ability-rail", "status-strip", "channel-progress", "coordinate-navigator",
                "chart-menu", "rebind-list",
            };

            Assert.That(requiredElements.All(name => root.Q(name) != null), Is.True);
        }

        [Test]
        public void Main_scene_hosts_the_input_system_and_runtime_hud_document()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);

            var input = Object.FindFirstObjectByType<SeaInputController>();
            var hud = Object.FindFirstObjectByType<SeaHudController>();
            var document = Object.FindFirstObjectByType<UIDocument>();

            Assert.That(input, Is.Not.Null);
            Assert.That(input.Actions, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(document, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.SameAs(
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SeaHud.uxml")));
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(document.panelSettings.referenceResolution,
                Is.EqualTo(new Vector2Int(1280, 720)));
        }

        [Test]
        public void Runtime_client_has_no_legacy_input_or_immediate_mode_hud_path()
        {
            var runtimeSources = Directory.GetFiles("Assets/Scripts", "*.cs")
                .Select(File.ReadAllText)
                .ToArray();

            Assert.That(runtimeSources.Any(source => source.Contains("void OnGUI(")), Is.False);
            Assert.That(runtimeSources.Any(source => source.Contains("Input.Get")), Is.False);
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
