using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaChartCameraRulesTests
    {
        [Test]
        public void Zoom_is_clamped_between_the_minimum_and_the_maximum()
        {
            Assert.That(SeaChartCameraRules.ClampZoom(5f), Is.EqualTo(SeaChartCameraRules.MinimumZoom));
            Assert.That(SeaChartCameraRules.ClampZoom(20f), Is.EqualTo(20f));
            Assert.That(SeaChartCameraRules.ClampZoom(500f), Is.EqualTo(SeaChartCameraRules.MaximumZoom));
        }

        [Test]
        public void The_default_zoom_frames_the_ship_rather_than_the_whole_chart()
        {
            var extents = SeaChartCameraRules.ViewHalfExtents(SeaChartCameraRules.DefaultZoom, 16f / 9f);
            var mapHalfSize = (SeaChartCoordinates.MapMaximum - SeaChartCoordinates.MapMinimum) / 2f;

            Assert.That(SeaChartCameraRules.DefaultZoom, Is.LessThan(SeaChartCameraRules.MaximumZoom));
            Assert.That(SeaChartCameraRules.DefaultZoom, Is.GreaterThan(SeaChartCameraRules.MinimumZoom));
            Assert.That(
                extents.x,
                Is.LessThan(mapHalfSize / 2f),
                "The default view shows a quarter of the map or less, so sailing reads as motion.");
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
        public void Clamp_center_keeps_the_footprint_inside_the_drawn_water()
        {
            // The chart runs 0..400 with its middle at 200, so a reach is read out from 200
            // rather than from zero. Half-extents of 60 by 80 leave a reach of 180 on x and
            // 160 on z: the view may carry one margin past the map edge, but never past the
            // water the world draws.
            var extents = new Vector2(60f, 80f);

            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(-150f, 0f, 550f), extents),
                Is.EqualTo(new Vector3(20f, 0f, 360f)));
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(210f, 3f, 180f), extents),
                Is.EqualTo(new Vector3(210f, 3f, 180f)),
                "A center well inside the water is untouched.");
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(380f, 0f, 40f), extents),
                Is.EqualTo(new Vector3(380f, 0f, 40f)),
                "A footprint that exactly touches the drawn edge is allowed.");
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(400f, 0f, 0f), new Vector2(35f, 25f)),
                Is.EqualTo(new Vector3(400f, 0f, 0f)),
                "At the default zoom the camera reaches the map corner itself.");
        }

        [Test]
        public void Mini_map_positions_map_panel_corners_to_the_map_corners()
        {
            // This rule answers in WORLD space, and the minimap camera looks straight down,
            // so the top of the panel is the maximum world z. North is drawn there because
            // SeaChartCoordinates.ToWorld puts north there.
            Assert.That(
                SeaMiniMapRules.ToWorldPosition(Vector2.zero),
                Is.EqualTo(new Vector3(0f, 0f, 400f)),
                "The top-left of the panel is the north-west corner of the chart.");
            Assert.That(
                SeaMiniMapRules.ToWorldPosition(Vector2.one),
                Is.EqualTo(new Vector3(400f, 0f, 0f)),
                "The bottom-right of the panel is the south-east corner.");
            Assert.That(
                SeaMiniMapRules.ToWorldPosition(new Vector2(0.5f, 0.5f)),
                Is.EqualTo(new Vector3(200f, 0f, 200f)),
                "The middle of the panel is the middle of the chart, which is no longer zero.");
            Assert.That(
                SeaMiniMapRules.ToWorldPosition(new Vector2(2f, -1f)),
                Is.EqualTo(new Vector3(400f, 0f, 400f)),
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
            Assert.That(center.x, Is.EqualTo(200f).Within(0.001f));
            Assert.That(center.z, Is.EqualTo(200f).Within(0.001f));
            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(new Vector2(1400f, 496f), panel, out var bottomLeft),
                Is.True);
            // Screen pixels count up from the bottom, so the panel's own origin is its
            // south-west corner: minimum x, and south is the minimum world z.
            Assert.That(bottomLeft, Is.EqualTo(new Vector3(0f, 0f, 0f)));
            Assert.That(
                SeaMiniMapRules.TryScreenToWorldPosition(Vector2.zero, new Rect(0f, 0f, 0f, 0f), out _),
                Is.False,
                "An empty panel rect never claims a click.");
        }
    }
}
