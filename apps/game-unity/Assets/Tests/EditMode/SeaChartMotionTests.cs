using System;
using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    public sealed class SeaChartMotionTests
    {
        [Test]
        public void Snapshot_clock_starts_on_the_first_observed_tick_and_advances_with_local_time()
        {
            var clock = new SeaSnapshotClock(10);
            Assert.That(clock.IsRunning, Is.False);
            Assert.That(clock.ServerTick(0d), Is.EqualTo(0d));

            clock.Observe(100, now: 0d);

            Assert.That(clock.IsRunning, Is.True);
            Assert.That(clock.ServerTick(0d), Is.EqualTo(100d).Within(0.001d));
            Assert.That(clock.RenderTick(0d), Is.EqualTo(98d).Within(0.001d));
            Assert.That(clock.ServerTick(0.5d), Is.EqualTo(105d).Within(0.001d));
        }

        [Test]
        public void Snapshot_clock_snaps_forward_on_early_samples_and_ignores_late_ones()
        {
            var clock = new SeaSnapshotClock(10);
            clock.Observe(100, now: 0d);
            clock.ServerTick(0d);

            clock.Observe(110, now: 0.5d);
            Assert.That(clock.ServerTick(0.5d), Is.EqualTo(110d).Within(0.001d));

            clock.Observe(100, now: 1d);
            Assert.That(clock.ServerTick(1d), Is.EqualTo(114.99d).Within(0.001d),
                "A late snapshot must not pull the render clock backwards.");
        }

        [Test]
        public void Snapshot_clock_slews_small_leads_instead_of_jumping()
        {
            var clock = new SeaSnapshotClock(10);
            clock.Observe(100, now: 0d);
            clock.ServerTick(0d);

            clock.Observe(106, now: 0.55d);

            Assert.That(clock.ServerTick(0.55d), Is.EqualTo(105.775d).Within(0.001d));
            Assert.That(clock.ServerTick(1.55d), Is.EqualTo(116d).Within(0.001d));
        }

        [Test]
        public void Motion_timeline_extrapolates_at_most_one_tick_past_the_latest_sample()
        {
            var timeline = new SeaMotionTimeline();
            timeline.Push(10, new Vector3(0f, 0f, 0f), 30f);
            timeline.Push(11, new Vector3(10f, 0f, 0f), 30f);

            Assert.That(timeline.Sample(11.5d).Position.x, Is.EqualTo(15f).Within(0.001f));
            Assert.That(timeline.Sample(13d).Position.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(timeline.Sample(13d).HeadingDegrees, Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void Motion_timeline_ignores_stale_ticks_and_replaces_duplicates()
        {
            var timeline = new SeaMotionTimeline();
            Assert.That(timeline.HasSamples, Is.False);
            Assert.Throws<InvalidOperationException>(() => timeline.Sample(0d));

            timeline.Push(11, new Vector3(10f, 0f, 0f), 0f);
            timeline.Push(10, new Vector3(99f, 0f, 0f), 0f);
            Assert.That(timeline.LatestTick, Is.EqualTo(11UL));
            Assert.That(timeline.Sample(10d).Position.x, Is.EqualTo(10f));

            timeline.Push(11, new Vector3(12f, 0f, 0f), 0f);
            Assert.That(timeline.Sample(11d).Position.x, Is.EqualTo(12f));
        }

        [Test]
        public void Motion_timeline_keeps_only_the_newest_samples()
        {
            var timeline = new SeaMotionTimeline();
            for (ulong tick = 0; tick < 20; tick++)
            {
                timeline.Push(tick, new Vector3(tick, 0f, 0f), 0f);
            }

            Assert.That(timeline.Sample(0d).Position.x, Is.EqualTo(20 - SeaMotionTimeline.Capacity));
            Assert.That(timeline.Sample(18.5d).Position.x, Is.EqualTo(18.5f).Within(0.001f));
        }

        [Test]
        public void Chart_follow_stays_detached_until_the_player_asks_for_the_ship()
        {
            var follow = new SeaChartFollowState();
            Assert.That(follow.IsFollowing, Is.True);

            follow.Interrupt();
            Assert.That(follow.IsFollowing, Is.False);
            follow.Interrupt();
            Assert.That(follow.IsFollowing, Is.False, "Repeated input never re-attaches on its own.");

            follow.Resume();
            Assert.That(follow.IsFollowing, Is.True);
            follow.Resume();
            Assert.That(follow.IsFollowing, Is.True);
        }

        [Test]
        public void Chart_camera_footprint_and_zoom_cap_keep_the_view_inside_the_map()
        {
            var extents = SeaChartCameraRules.ViewHalfExtents(45f, 16f / 9f);
            Assert.That(extents.x, Is.EqualTo(80f).Within(0.01f));
            Assert.That(extents.y, Is.EqualTo(54.936f).Within(0.01f));

            Assert.That(SeaChartCameraRules.MaximumZoomFor(16f / 9f), Is.EqualTo(56.25f).Within(0.001f));
            Assert.That(SeaChartCameraRules.MaximumZoomFor(1f), Is.EqualTo(80f));

            var clamped = SeaChartCameraRules.ClampCenter(new Vector3(90f, 5f, -90f), new Vector2(30f, 40f));
            Assert.That(clamped, Is.EqualTo(new Vector3(70f, 5f, -60f)));
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(50f, 0f, 50f), new Vector2(120f, 120f)),
                Is.EqualTo(Vector3.zero),
                "A view wider than the map centers on it.");
        }

        [Test]
        public void Mini_map_viewport_corners_trace_the_chart_footprint()
        {
            var corners = new Vector3[4];

            SeaMiniMapRules.ViewportCorners(new Vector3(10f, 14f, -20f), new Vector2(30f, 40f), corners);

            Assert.That(corners, Is.EquivalentTo(new[]
            {
                new Vector3(-20f, 14f, -60f), new Vector3(40f, 14f, -60f),
                new Vector3(40f, 14f, 20f), new Vector3(-20f, 14f, 20f),
            }));
        }

        [Test]
        public void Mini_map_camera_pixel_rect_maps_panel_bounds_to_screen_pixels()
        {
            var rect = SeaMiniMapRules.ScreenPixelRect(
                new Rect(700f, 76f, 216f, 216f),
                panelHeight: 540f,
                screenHeight: 1080f);

            Assert.That(rect, Is.EqualTo(new Rect(1400f, 496f, 432f, 432f)));
        }

        [Test]
        public void Chart_drag_moves_the_view_opposite_to_the_pointer_by_screen_fraction()
        {
            var horizontal = SeaChartCameraRules.DragDelta(new Vector2(100f, 0f), 45f, 900f);
            Assert.That(horizontal.x, Is.EqualTo(-10f).Within(0.001f));
            Assert.That(horizontal.z, Is.EqualTo(0f));

            var vertical = SeaChartCameraRules.DragDelta(new Vector2(0f, 90f), 45f, 900f);
            Assert.That(vertical.z, Is.EqualTo(-10.987f).Within(0.01f));
        }
    }
}
