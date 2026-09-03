#if UNITY_EDITOR
using System.IO;
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
            Assert.That(miniMap.orthographicSize, Is.EqualTo(100f).Within(0.1f));
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
            Assert.That(SeaPresentationRules.VisionRadius, Is.EqualTo(44f));
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
            var chartCamera = cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<SeaChartCameraController>();
            controller.Configure(chartCamera);

            Assert.That(controller.IsFollowingPlayer, Is.True);
            controller.SetPanInput(Vector2.right);
            Assert.That(controller.IsFollowingPlayer, Is.False);
            controller.SetPanInput(Vector2.zero);
            Assert.That(controller.IsFollowingPlayer, Is.False, "Releasing WASD leaves the camera where it was pushed.");
            controller.Recenter();
            Assert.That(controller.IsFollowingPlayer, Is.True);

            controller.BeginDrag(Vector2.zero);
            controller.EndDrag();
            Assert.That(controller.IsFollowingPlayer, Is.False, "A middle-mouse drag detaches the camera too.");
            controller.Recenter();
            Assert.That(controller.IsFollowingPlayer, Is.True);

            controller.ShowChartPosition(new Vector3(20f, 0f, 20f));
            Assert.That(controller.IsFollowingPlayer, Is.False, "Jumping the chart somewhere detaches it as well.");
            controller.Recenter();
            Assert.That(controller.IsFollowingPlayer, Is.True);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void Recentering_the_chart_ends_the_pan_glide_it_was_coasting_on()
        {
            var cameraObject = new GameObject("Chart Camera");
            var chartCamera = cameraObject.AddComponent<Camera>();
            var controller = cameraObject.AddComponent<SeaChartCameraController>();
            controller.Configure(chartCamera);

            Assert.That(controller.IsGliding, Is.False);
            controller.SetPanInput(Vector2.right);
            controller.Pan(1f / 60f);
            Assert.That(controller.IsGliding, Is.True, "Holding WASD builds a glide.");

            controller.SetPanInput(Vector2.zero);
            controller.Recenter();

            Assert.That(controller.IsGliding, Is.False,
                "Leftover glide would push the chart away from the ship the follow pulls it to.");
            Assert.That(controller.IsFollowingPlayer, Is.True);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void Chart_zoom_and_drag_keep_the_camera_attached_or_detached_as_the_player_left_it()
        {
            var cameraObject = new GameObject("Zooming Chart Camera");
            var chartCamera = cameraObject.AddComponent<Camera>();
            chartCamera.orthographic = true;
            chartCamera.orthographicSize = 45f;
            cameraObject.transform.position = new Vector3(0f, 70f, -50f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            var controller = cameraObject.AddComponent<SeaChartCameraController>();
            controller.Configure(chartCamera);

            controller.Zoom(1f);
            Assert.That(chartCamera.orthographicSize, Is.LessThan(45f), "Scrolling forward zooms in.");
            Assert.That(controller.IsFollowingPlayer, Is.True, "Zooming never detaches the camera from the ship.");
            controller.Zoom(-100f);
            Assert.That(
                chartCamera.orthographicSize,
                Is.EqualTo(SeaChartCameraRules.MaximumZoomFor(chartCamera.aspect)).Within(0.001f),
                "Zooming out stops where the map edge would show.");

            // Zoomed all the way out the footprint spans the map, so zoom back in to leave room to drag.
            chartCamera.orthographicSize = SeaChartCameraRules.MinimumZoom;
            controller.BeginDrag(new Vector2(100f, 100f));
            var before = cameraObject.transform.position.x;
            controller.DragTo(new Vector2(150f, 100f));
            Assert.That(cameraObject.transform.position.x, Is.LessThan(before), "Dragging right slides the chart left.");
            controller.EndDrag();
            var released = cameraObject.transform.position.x;
            controller.DragTo(new Vector2(300f, 100f));
            Assert.That(
                cameraObject.transform.position.x,
                Is.EqualTo(released),
                "Pointer motion after the drag ended does not move the chart.");
            Assert.That(controller.IsFollowingPlayer, Is.False);
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
            var destination = new Vector3(-30f, 0f, 40f);

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
    }
}
#endif
