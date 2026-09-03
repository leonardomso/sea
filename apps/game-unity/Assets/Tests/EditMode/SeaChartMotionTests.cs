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
            Assert.That(clock.RenderTick(0d), Is.EqualTo(99d).Within(0.001d));
            Assert.That(
                SeaSnapshotClock.RenderTickFrom(100d),
                Is.EqualTo(99d).Within(0.001d),
                "Converting a tick already read must not advance the estimate a second time.");
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
        public void Chart_camera_footprint_follows_the_ship_to_the_map_edge()
        {
            var extents = SeaChartCameraRules.ViewHalfExtents(SeaChartCameraRules.DefaultZoom, 16f / 9f);
            Assert.That(extents.x, Is.EqualTo(35.556f).Within(0.01f));
            Assert.That(extents.y, Is.EqualTo(24.416f).Within(0.01f));

            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(100f, 5f, -100f), extents),
                Is.EqualTo(new Vector3(100f, 5f, -100f)),
                "A ship in the map corner still gets the camera centred on it.");
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(90f, 5f, -90f), new Vector2(60f, 80f)),
                Is.EqualTo(new Vector3(80f, 5f, -60f)),
                "A zoomed-out view stops where the drawn water would run out.");
            Assert.That(
                SeaChartCameraRules.ClampCenter(new Vector3(50f, 0f, 50f), new Vector2(200f, 200f)),
                Is.EqualTo(Vector3.zero),
                "A view wider than the water centers on the map.");
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

        [Test]
        public void Pan_momentum_ramps_toward_the_held_direction_without_overshooting_it()
        {
            var momentum = new SeaChartPanMomentum();
            Assert.That(momentum.IsGliding, Is.False);

            var first = momentum.Advance(Vector2.right, 45f, 10f, 1f / 60f);
            Assert.That(first.x, Is.GreaterThan(0f));
            Assert.That(first.x, Is.LessThan(45f), "One frame must not jump to full pan speed.");
            Assert.That(momentum.IsGliding, Is.True);

            var velocity = first;
            for (var frame = 0; frame < 300; frame++)
            {
                velocity = momentum.Advance(Vector2.right, 45f, 10f, 1f / 60f);
            }

            Assert.That(velocity.x, Is.EqualTo(45f).Within(0.01f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Pan_momentum_coasts_to_a_full_stop_after_the_key_is_released()
        {
            var momentum = new SeaChartPanMomentum();
            for (var frame = 0; frame < 60; frame++)
            {
                momentum.Advance(Vector2.up, 45f, 10f, 1f / 60f);
            }

            var released = momentum.Advance(Vector2.zero, 45f, 10f, 1f / 60f);
            Assert.That(released.y, Is.GreaterThan(0f), "Releasing WASD glides rather than snapping.");

            for (var frame = 0; frame < 600; frame++)
            {
                momentum.Advance(Vector2.zero, 45f, 10f, 1f / 60f);
            }

            Assert.That(momentum.Velocity, Is.EqualTo(Vector2.zero),
                "A coast that never reaches zero would fight the follow forever.");
            Assert.That(momentum.IsGliding, Is.False);
        }

        [Test]
        public void Stopping_the_glide_drops_the_velocity_immediately()
        {
            var momentum = new SeaChartPanMomentum();
            momentum.Advance(new Vector2(1f, 1f), 45f, 10f, 1f / 60f);
            Assert.That(momentum.IsGliding, Is.True);

            momentum.Stop();

            Assert.That(momentum.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(momentum.IsGliding, Is.False);
        }

        [Test]
        public void Pan_momentum_scales_with_the_speed_it_is_given()
        {
            var slow = new SeaChartPanMomentum();
            var fast = new SeaChartPanMomentum();
            for (var frame = 0; frame < 300; frame++)
            {
                slow.Advance(Vector2.right, 20f, 10f, 1f / 60f);
                fast.Advance(Vector2.right, 80f, 10f, 1f / 60f);
            }

            Assert.That(slow.Velocity.x, Is.EqualTo(20f).Within(0.01f));
            Assert.That(fast.Velocity.x, Is.EqualTo(80f).Within(0.01f));
        }
    }
}
