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
    public sealed partial class SeaProjectTests
    {
        [Test]
        public void Initial_subscription_plan_is_owner_scoped_and_never_unrestricted()
        {
            var queries = SeaSubscriptionPlan.Initial("0xabc123");

            Assert.That(queries, Does.Contain("SELECT * FROM player_ownership WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain("SELECT * FROM world_state"));
            Assert.That(queries.Any(query => query == "SELECT * FROM ship"), Is.False);
        }

        [Test]
        public void Player_subscription_includes_authoritative_tactical_channels()
        {
            var queries = SeaSubscriptionPlan.Player(42);

            Assert.That(queries, Does.Contain(
                "SELECT * FROM ship_channel WHERE ship_entity_id = 42"));
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
            Assert.That(center.Column, Is.EqualTo(23));
            Assert.That(center.Row, Is.EqualTo(59));
            Assert.That(SeaChartCoordinates.LabelAt(center.X, center.Y), Is.EqualTo("AX 59"));
            Assert.That(SeaChartCoordinates.LabelAt(-99.9f, 99.9f), Is.EqualTo("AA 0"));
            Assert.That(SeaChartCoordinates.LabelAt(99.9f, -99.9f), Is.EqualTo("CZ 60"));
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
        public void Manual_combat_bindings_replace_the_prototype_engage_reducer()
        {
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/FireBroadside.g.cs"), Is.True);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/SetAmmo.g.cs"), Is.True);
            Assert.That(File.Exists(
                "Assets/Generated/SpacetimeDB/Reducers/Engage.g.cs"), Is.False);
        }

        [Test]
        public void Tactical_reducer_bindings_are_generated_for_every_hotbar_command()
        {
            var reducers = new[]
            {
                "ActivateAbility.g.cs", "StartRepair.g.cs", "CancelRepair.g.cs",
                "StartBoarding.g.cs", "CancelBoarding.g.cs",
            };

            Assert.That(reducers.All(file => File.Exists(
                $"Assets/Generated/SpacetimeDB/Reducers/{file}")), Is.True);
        }

        [Test]
        public void Shoals_and_storms_have_distinct_chart_geometry()
        {
            var shallows = SeaMaterialFactory.CreateTransparent(new Color(0.2f, 0.8f, 0.7f, 0.35f));
            var storm = SeaMaterialFactory.CreateTransparent(new Color(0.12f, 0.16f, 0.2f, 0.7f));
            var shoal = SeaWorldGeometryFactory.CreateShoal(
                "Test Shoal", Vector3.zero, 10f, shallows);
            var cloud = SeaWorldGeometryFactory.CreateStorm(
                "Test Storm", Vector3.zero, 10f, storm);

            Assert.That(shoal.transform.Find("Shoal Water"), Is.Not.Null);
            Assert.That(cloud.GetComponentsInChildren<Renderer>(), Has.Length.GreaterThanOrEqualTo(5));
            Assert.That(shoal.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(cloud.GetComponentsInChildren<Collider>(), Is.Empty);
            Object.DestroyImmediate(shoal);
            Object.DestroyImmediate(cloud);
            Object.DestroyImmediate(shallows);
            Object.DestroyImmediate(storm);
        }

        [Theory]
        [TestCase(10ul, 60ul, 10ul, 0f)]
        [TestCase(10ul, 60ul, 35ul, 0.5f)]
        [TestCase(10ul, 60ul, 60ul, 1f)]
        public void Tactical_channel_progress_uses_authoritative_ticks(
            ulong startedAtTick,
            ulong completesAtTick,
            ulong currentTick,
            float expected)
        {
            Assert.That(SeaTacticalPresentationRules.ChannelProgress(
                startedAtTick,
                completesAtTick,
                currentTick), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Runtime_combat_observation_stays_inside_one_spatial_chunk_and_holds_position()
        {
            Assert.That(SeaRuntimeValidationRules.CombatObservationRange,
                Is.LessThanOrEqualTo(25f));
            Assert.That(SeaRuntimeValidationRules.ShouldHoldPositionBeforeFire(
                distance: 12f,
                targetSelected: true), Is.True);
            Assert.That(SeaRuntimeValidationRules.ShouldHoldPositionBeforeFire(
                distance: 25f,
                targetSelected: true), Is.False);
        }

        [Theory]
        [TestCase(5ul, 10ul, 5ul, 0f)]
        [TestCase(5ul, 10ul, 7ul, 0.4f)]
        [TestCase(5ul, 10ul, 10ul, 1f)]
        [TestCase(5ul, 10ul, 20ul, 1f)]
        public void Volley_presentation_uses_authoritative_launch_and_impact_ticks(
            ulong firedAtTick,
            ulong impactAtTick,
            ulong currentTick,
            float expected)
        {
            Assert.That(SeaVolleyPresentationRules.Progress(
                firedAtTick,
                impactAtTick,
                currentTick), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Broadside_effects_spawn_on_the_ordered_side()
        {
            Assert.That(SeaVolleyPresentationRules.LocalSideOffset("port", 3f),
                Is.EqualTo(new Vector3(-3f, 0f, 0f)));
            Assert.That(SeaVolleyPresentationRules.LocalSideOffset("starboard", 3f),
                Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(SeaVolleyPresentationRules.IsInsideBroadsideArc(
                Vector2.zero, 0f, Vector2.left * 10f, "port"), Is.True);
            Assert.That(SeaVolleyPresentationRules.IsInsideBroadsideArc(
                Vector2.zero, 0f, Vector2.right * 10f, "port"), Is.False);
        }

        [Test]
        public void Combat_visual_pool_reuses_released_instances()
        {
            var pool = new SeaCombatVisualPool(() => new GameObject("Pooled combat visual"));
            var first = pool.Acquire();

            pool.Release(first);
            var second = pool.Acquire();

            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Object.DestroyImmediate(second);
        }

        [Test]
        public void Aggregated_volley_visual_is_lightweight_and_trail_enabled()
        {
            var material = SeaMaterialFactory.Create(Color.black);
            var volley = SeaCombatVisualFactory.CreateVolley(material);

            Assert.That(volley.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(5));
            Assert.That(volley.GetComponentsInChildren<TrailRenderer>(true), Has.Length.EqualTo(5));
            Assert.That(volley.GetComponentsInChildren<Collider>(true), Is.Empty);
            Object.DestroyImmediate(volley);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Combat_effect_visual_supports_particles_and_spatial_audio()
        {
            var material = SeaMaterialFactory.Create(Color.white);
            var effect = SeaCombatVisualFactory.CreateEffect("Impact", material);
            var audio = effect.GetComponent<AudioSource>();

            Assert.That(effect.GetComponent<ParticleSystem>(), Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.spatialBlend, Is.GreaterThan(0f));
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(material);
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
                "connection-status", "navigation-readout", "gold-label", "diamond-label",
                "top-coordinate-ruler", "left-coordinate-ruler",
                "player-hull", "player-experience",
                "mini-map-frame",
                "target-frame", "target-hull", "target-sails", "target-cannons",
                "port-broadside", "starboard-broadside", "weak-point-rail", "ammo-rail",
                "ability-rail", "status-strip", "channel-progress", "coordinate-navigator",
                "chart-menu", "rebind-list",
            };

            Assert.That(requiredElements.All(name => root.Q(name) != null), Is.True);
            Assert.That(root.Q<Button>("aim-hull").text, Is.EqualTo("1"));
            Assert.That(root.Q<Button>("ability-full-sail").text, Is.EqualTo("Z"));
            Assert.That(root.Q<Button>("port-broadside"), Is.Not.Null);
            Assert.That(root.Q<Button>("starboard-broadside"), Is.Not.Null);
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

        [TestCase(new[] { "game", "-seaDatabaseName", "sea-smoke" }, "sea-smoke")]
        [TestCase(new[] { "game", "-seaDatabaseName=sea-smoke" }, "sea-smoke")]
        [TestCase(new[] { "game" }, "sea-local")]
        public void Runtime_database_can_be_isolated_by_command_line(
            string[] arguments,
            string expected)
        {
            Assert.That(SeaClientOptions.DatabaseName(arguments, "sea-local"), Is.EqualTo(expected));
        }

    }
}
#endif
