using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaChartCameraRulesTests
    {
        [Test]
        public void Zoom_is_clamped_between_the_minimum_and_the_map_bound_maximum()
        {
            Assert.That(SeaChartCameraRules.ClampZoom(5f, 16f / 9f), Is.EqualTo(SeaChartCameraRules.MinimumZoom));
            Assert.That(SeaChartCameraRules.ClampZoom(45f, 16f / 9f), Is.EqualTo(45f));
            Assert.That(
                SeaChartCameraRules.ClampZoom(500f, 16f / 9f),
                Is.EqualTo(SeaChartCameraRules.MaximumZoomFor(16f / 9f)));
        }

        [Test]
        public void Wider_screens_get_a_lower_zoom_cap_so_the_map_edge_stays_hidden()
        {
            var ultraWide = SeaChartCameraRules.MaximumZoomFor(21f / 9f);
            var wide = SeaChartCameraRules.MaximumZoomFor(16f / 9f);
            var square = SeaChartCameraRules.MaximumZoomFor(1f);

            Assert.That(ultraWide, Is.LessThan(wide));
            Assert.That(wide, Is.LessThan(square));
            Assert.That(
                SeaChartCameraRules.ViewHalfExtents(ultraWide, 21f / 9f).x,
                Is.LessThanOrEqualTo(100.001f),
                "At the cap the footprint never reaches past the map edge.");
        }

        [Test]
        public void Pan_delta_scales_with_speed_and_frame_time_on_the_ground_plane()
        {
            var delta = SeaChartCameraRules.PanDelta(1f, -0.5f, 45f, 0.1f);

            Assert.That(delta.x, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(delta.y, Is.EqualTo(0f), "Panning never lifts the camera.");
            Assert.That(delta.z, Is.EqualTo(-2.25f).Within(0.001f));
            Assert.That(SeaChartCameraRules.PanDelta(0f, 0f, 45f, 0.1f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Drag_delta_scales_with_zoom_so_a_screen_fraction_always_moves_the_same_view_fraction()
        {
            var near = SeaChartCameraRules.DragDelta(new Vector2(90f, 0f), 20f, 900f);
            var far = SeaChartCameraRules.DragDelta(new Vector2(90f, 0f), 80f, 900f);

            Assert.That(far.x, Is.EqualTo(near.x * 4f).Within(0.001f));
            Assert.That(
                SeaChartCameraRules.DragDelta(new Vector2(90f, 0f), 45f, 0f).x,
                Is.EqualTo(SeaChartCameraRules.DragDelta(new Vector2(90f, 0f), 45f, 1f).x),
                "A zero-height viewport is treated as one pixel tall instead of dividing by zero.");
        }

        [Test]
        public void Clamp_center_keeps_the_footprint_inside_every_map_edge()
        {
            var extents = new Vector2(30f, 40f);

            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(-150f, 0f, 150f), extents),
                Is.EqualTo(new Vector3(-70f, 0f, 60f)));
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(10f, 3f, -20f), extents),
                Is.EqualTo(new Vector3(10f, 3f, -20f)),
                "A center whose footprint is already inside the map is untouched.");
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(70f, 0f, -60f), extents),
                Is.EqualTo(new Vector3(70f, 0f, -60f)),
                "A footprint that exactly touches the map edge is allowed.");
        }

        [Test]
        public void Mini_map_positions_map_panel_corners_to_the_map_corners()
        {
            Assert.That(SeaMiniMapRules.ToWorldPosition(Vector2.zero), Is.EqualTo(new Vector3(-100f, 0f, 100f)));
            Assert.That(SeaMiniMapRules.ToWorldPosition(Vector2.one), Is.EqualTo(new Vector3(100f, 0f, -100f)));
            Assert.That(SeaMiniMapRules.ToWorldPosition(new Vector2(0.5f, 0.5f)), Is.EqualTo(Vector3.zero));
            Assert.That(
                SeaMiniMapRules.ToWorldPosition(new Vector2(2f, -1f)),
                Is.EqualTo(new Vector3(100f, 0f, 100f)),
                "Positions past the panel are clamped to the map.");
        }

        [Test]
        public void Mini_map_screen_clicks_only_count_inside_the_panel()
        {
            var panel = new Rect(1400f, 496f, 432f, 432f);

            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(new Vector2(1000f, 600f), panel, out _),
                Is.False);
            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(new Vector2(1616f, 712f), panel, out var center),
                Is.True);
            Assert.That(center.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(center.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(new Vector2(1400f, 496f), panel, out var bottomLeft),
                Is.True);
            Assert.That(bottomLeft, Is.EqualTo(new Vector3(-100f, 0f, -100f)));
            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(Vector2.zero, new Rect(0f, 0f, 0f, 0f), out _),
                Is.False,
                "An empty panel rect never claims a click.");
        }
    }
}
