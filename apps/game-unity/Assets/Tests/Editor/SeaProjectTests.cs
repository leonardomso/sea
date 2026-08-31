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
        public void Apricum_ship_model_is_imported_for_runtime_use()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");

            Assert.That(shipModel, Is.Not.Null);
            Assert.That(shipModel.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }

        [Test]
        public void Apricum_visual_preserves_FBX_axis_conversion_when_yaw_is_applied()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var ship = SeaShipVisualFactory.Create(
                shipModel,
                "Axis Correct Ship",
                10f,
                modelYawOffsetDegrees: 90f);
            var visual = ship.transform.Find("Visual");
            var expected = Quaternion.Euler(0f, 90f, 0f) * shipModel.transform.localRotation;

            Assert.That(Quaternion.Angle(visual.localRotation, expected), Is.LessThan(0.1f));
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Apricum_hull_intersects_the_waterline_instead_of_hovering()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var ship = SeaShipVisualFactory.Create(
                shipModel,
                "Waterline Ship",
                10f,
                modelYawOffsetDegrees: 90f);
            ship.transform.position = Vector3.up * SeaWorldView.ShipRootHeight;
            var bounds = SeaShipVisualFactory.CalculateRendererBounds(ship);
            var submergedDepth = SeaWorldView.WaterSurfaceHeight - bounds.min.y;

            Assert.That(submergedDepth, Is.InRange(0.04f, 0.16f));
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Apricum_ship_model_has_a_game_ready_triangle_budget()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var triangleCount = shipModel.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Sum(mesh => Enumerable.Range(0, mesh.subMeshCount)
                    .Sum(subMesh => (long)mesh.GetIndexCount(subMesh) / 3));

            Assert.That(triangleCount, Is.LessThanOrEqualTo(30_000),
                "The starter ship exceeds its 30,000-triangle runtime budget.");
        }

        [Test]
        public void Apricum_model_excludes_studio_geometry()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var meshFilters = shipModel.GetComponentsInChildren<MeshFilter>(true);

            Assert.That(meshFilters, Has.Length.EqualTo(1));
            Assert.That(meshFilters[0].name, Does.Not.Contain("Cube"));
        }

        [Test]
        public void Apricum_material_uses_all_runtime_pbr_textures()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Ships/Apricum/Apricum.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Standard"));
            Assert.That(material.GetTexture("_BaseMap") ?? material.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null);
        }

        [Test]
        public void Apricum_textures_use_color_correct_import_settings()
        {
            var baseColor = (TextureImporter)AssetImporter.GetAtPath(
                "Assets/Art/Ships/Apricum/Textures/Apricum_BaseColor.png");
            var normal = (TextureImporter)AssetImporter.GetAtPath(
                "Assets/Art/Ships/Apricum/Textures/Apricum_Normal.png");
            var metallicSmoothness = (TextureImporter)AssetImporter.GetAtPath(
                "Assets/Art/Ships/Apricum/Textures/Apricum_MetallicSmoothness.png");

            Assert.That(baseColor.sRGBTexture, Is.True);
            Assert.That(normal.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(normal.flipGreenChannel, Is.False);
            Assert.That(metallicSmoothness.sRGBTexture, Is.False);
            Assert.That(metallicSmoothness.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        }

        [Test]
        public void Apricum_ship_visual_never_uses_an_all_white_fallback()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Ships/Apricum/Apricum.mat");
            var ship = SeaShipVisualFactory.Create(shipModel, "Colored Ship", 10f, material);
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
        public void Apricum_ship_visual_uses_the_imported_model_at_a_readable_scale()
        {
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");

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
                targetHeadingDegrees: 0f,
                deltaTime: 0.1f,
                movementSpeed: 5f,
                turnSpeedDegrees: 90f);

            Assert.That(ship.transform.position.z, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Quaternion.Angle(Quaternion.identity, ship.transform.rotation),
                Is.EqualTo(0f).Within(0.1f));
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Ship_motion_does_not_slide_sideways_while_turning()
        {
            var ship = new GameObject("Turning Ship");
            var initialPosition = ship.transform.position;
            const float authoritativeHeading = 5.5f;
            var headingRadians = authoritativeHeading * Mathf.Deg2Rad;
            var authoritativePosition = new Vector3(
                Mathf.Sin(headingRadians),
                0f,
                Mathf.Cos(headingRadians));

            SeaShipMotion.Step(
                ship.transform,
                authoritativePosition,
                targetHeadingDegrees: authoritativeHeading,
                deltaTime: 0.1f,
                movementSpeed: 5f,
                turnSpeedDegrees: 90f);

            var movementDirection = (ship.transform.position - initialPosition).normalized;
            var headingAlignment = Vector3.Dot(movementDirection, ship.transform.forward);

            Assert.That(headingAlignment, Is.GreaterThanOrEqualTo(0.9f),
                "The ship translated sideways instead of sailing along its heading.");
            Object.DestroyImmediate(ship);
        }

        [Test]
        public void Chart_clicks_outside_the_projected_map_are_clamped_to_valid_water()
        {
            var clamped = SeaChartCoordinates.ClampToMap(new Vector2(-180f, 245f));

            Assert.That(clamped.x, Is.EqualTo(SeaChartCoordinates.MapMinimum));
            Assert.That(clamped.y, Is.EqualTo(SeaChartCoordinates.MapMaximum));
        }

        [Test]
        public void Island_clicks_are_blocked_but_adjacent_shore_water_is_selectable()
        {
            Assert.That(SeaChartCoordinates.IsBlockedDestination(
                Vector2.zero, Vector2.zero, 10f), Is.True);
            Assert.That(SeaChartCoordinates.IsBlockedDestination(
                new Vector2(0f, 11f), Vector2.zero, 10f), Is.False);
        }

        [Test]
        public void Mini_map_positions_map_top_left_and_bottom_right_to_chart_extents()
        {
            var topLeft = SeaMiniMapRules.ToWorldPosition(new Vector2(0f, 0f));
            var bottomRight = SeaMiniMapRules.ToWorldPosition(new Vector2(1f, 1f));

            Assert.That(topLeft.x, Is.EqualTo(SeaChartCoordinates.MapMinimum));
            Assert.That(topLeft.z, Is.EqualTo(SeaChartCoordinates.MapMaximum));
            Assert.That(bottomRight.x, Is.EqualTo(SeaChartCoordinates.MapMaximum));
            Assert.That(bottomRight.z, Is.EqualTo(SeaChartCoordinates.MapMinimum));
        }

        [Test]
        public void Mini_map_screen_hit_testing_uses_the_camera_pixel_rectangle()
        {
            var pixelRect = new Rect(800f, 600f, 200f, 150f);

            Assert.That(SeaMiniMapRules.TryScreenToWorldPosition(
                new Vector2(800.1f, 749.9f), pixelRect, out var topLeft), Is.True);
            Assert.That(topLeft.x, Is.EqualTo(SeaChartCoordinates.MapMinimum).Within(0.2f));
            Assert.That(topLeft.z, Is.EqualTo(SeaChartCoordinates.MapMaximum).Within(0.2f));
            Assert.That(SeaMiniMapRules.TryScreenToWorldPosition(
                new Vector2(799f, 750f), pixelRect, out _), Is.False);
        }

        [Test]
        public void Main_scene_references_the_Apricum_ship_and_material()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var world = Object.FindFirstObjectByType<SeaWorldView>();
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Ships/Apricum/Apricum.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Ships/Apricum/Apricum.mat");
            var fogShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/SeaChartFog.shader");

            Assert.That(world, Is.Not.Null);
            Assert.That(world.ShipModel, Is.SameAs(shipModel));
            Assert.That(world.ShipMaterial, Is.SameAs(material));
            Assert.That(world.FogShader, Is.SameAs(fogShader),
                "The production build must retain the serialized fog shader.");
            Assert.That(world.ModelYawOffset, Is.EqualTo(270f),
                "Apricum's bow must face the authoritative ship-forward direction.");
            Assert.That(world.PresentationTurnSpeed, Is.EqualTo(720f),
                "Combat sailing should visually settle onto a new heading without lag.");
        }

        [Test]
        public void Main_scene_uses_a_readable_two_point_five_d_camera_angle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var worldCamera = Camera.main;

            Assert.That(worldCamera, Is.Not.Null);
            Assert.That(worldCamera.transform.eulerAngles.x, Is.EqualTo(55f).Within(0.1f));
            Assert.That(worldCamera.orthographicSize, Is.EqualTo(34f).Within(0.1f));
        }

        [Test]
        public void Main_scene_includes_a_full_map_mini_camera()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            var miniMap = GameObject.Find("Mini Map Camera")?.GetComponent<Camera>();
            var chartController = Object.FindFirstObjectByType<SeaChartCameraController>();

            Assert.That(miniMap, Is.Not.Null);
            Assert.That(miniMap.orthographic, Is.True);
            Assert.That(miniMap.orthographicSize, Is.EqualTo(108f).Within(0.1f));
            Assert.That(miniMap.rect.width, Is.EqualTo(0.17f).Within(0.001f));
            Assert.That((miniMap.cullingMask & (1 << 8)), Is.Zero,
                "Fog of war belongs to the main chart, not the strategic minimap.");
            Assert.That(chartController.MiniMapCamera, Is.SameAs(miniMap),
                "Minimap input must use the exact serialized camera viewport.");
        }

        [Test]
        public void Sea_chart_uses_an_authored_animated_water_shader()
        {
            var waterShader = Shader.Find("Sea/Chart Water");

            Assert.That(waterShader, Is.Not.Null);
            Assert.That(Shader.Find("Sea/Chart Fog"), Is.Not.Null);
            Assert.That(SeaWorldView.VisionRadius, Is.EqualTo(44f));
        }

        [Test]
        public void Procedural_islands_have_shore_rock_and_land_layers()
        {
            var sand = SeaMaterialFactory.Create(new Color(0.72f, 0.58f, 0.34f));
            var rock = SeaMaterialFactory.Create(new Color(0.20f, 0.22f, 0.20f));
            var land = SeaMaterialFactory.Create(new Color(0.18f, 0.34f, 0.22f));
            var island = SeaWorldGeometryFactory.CreateIsland(
                "Test Island",
                Vector3.zero,
                10f,
                sand,
                rock,
                land);

            Assert.That(island.transform.Find("Sand Shore"), Is.Not.Null);
            Assert.That(island.transform.Find("Rock Shelf"), Is.Not.Null);
            Assert.That(island.transform.Find("Island Crown"), Is.Not.Null);
            Assert.That(island.GetComponentsInChildren<Renderer>(), Has.Length.GreaterThanOrEqualTo(3));
            Object.DestroyImmediate(island);
            Object.DestroyImmediate(sand);
            Object.DestroyImmediate(rock);
            Object.DestroyImmediate(land);
        }

        [Test]
        public void Wake_emission_requires_meaningful_ship_speed()
        {
            Assert.That(SeaShipFeedback.ShouldEmitWake(0f, 12f), Is.False);
            Assert.That(SeaShipFeedback.ShouldEmitWake(0.2f, 12f), Is.False);
            Assert.That(SeaShipFeedback.ShouldEmitWake(3f, 12f), Is.True);
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
        public void Chart_camera_follows_until_manual_pan_and_recenter_restores_follow()
        {
            var cameraObject = new GameObject("Chart Camera");
            cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<SeaChartCameraController>();

            Assert.That(controller.IsFollowingPlayer, Is.True);
            controller.SetPanInput(Vector2.right);
            Assert.That(controller.IsFollowingPlayer, Is.False);
            controller.SetPanInput(Vector2.zero);
            Assert.That(controller.IsFollowingPlayer, Is.False);
            controller.Recenter();
            Assert.That(controller.IsFollowingPlayer, Is.True);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void Mini_map_navigation_centers_the_main_chart_and_detaches_ship_follow()
        {
            var cameraObject = new GameObject("Interactive Chart Camera");
            var chartCamera = cameraObject.AddComponent<Camera>();
            chartCamera.orthographic = true;
            chartCamera.orthographicSize = 34f;
            cameraObject.transform.position = new Vector3(0f, 70f, -50f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            var controller = cameraObject.AddComponent<SeaChartCameraController>();
            controller.Configure(chartCamera);
            var destination = new Vector3(-46f, 0f, 43f);

            controller.ShowChartPosition(destination);

            var ray = chartCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            Assert.That(plane.Raycast(ray, out var distance), Is.True);
            var center = ray.GetPoint(distance);
            Assert.That(center.x, Is.EqualTo(destination.x).Within(0.01f));
            Assert.That(center.z, Is.EqualTo(destination.z).Within(0.01f));
            Assert.That(controller.IsFollowingPlayer, Is.False);
            Object.DestroyImmediate(cameraObject);
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
