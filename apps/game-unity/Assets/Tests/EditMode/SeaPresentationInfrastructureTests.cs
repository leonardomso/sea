#if UNITY_EDITOR
using NUnit.Framework;
using Sea.Client;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Sea.Tests
{
    public sealed class SeaPresentationInfrastructureTests
    {
        [Test]
        public void Bounded_pool_reuses_reset_items_and_refuses_unbounded_growth()
        {
            var nextId = 0;
            var resetCount = 0;
            var pool = new SeaBoundedPool<PooledValue>(
                () => new PooledValue(++nextId),
                _ => resetCount++,
                initialCapacity: 1,
                maximumCapacity: 2);

            Assert.That(pool.TryAcquire(out var first), Is.True);
            Assert.That(pool.TryAcquire(out var second), Is.True);
            Assert.That(pool.TryAcquire(out _), Is.False);
            Assert.That(pool.CreatedCount, Is.EqualTo(2));

            pool.Release(first);
            Assert.That(resetCount, Is.EqualTo(2));
            Assert.That(pool.TryAcquire(out var reused), Is.True);
            Assert.That(reused.Id, Is.EqualTo(first.Id));
            Assert.That(second.Id, Is.Not.EqualTo(reused.Id));
        }

        [Test]
        public void Keyed_pool_reuses_the_matching_role_and_enforces_one_global_limit()
        {
            var nextId = 0;
            var pool = new SeaKeyedBoundedPool<string, PooledValue>(
                _ => new PooledValue(++nextId),
                _ => { },
                maximumCapacity: 2);

            Assert.That(pool.TryAcquire("player", out var player), Is.True);
            Assert.That(pool.TryAcquire("raider", out var raider), Is.True);
            Assert.That(pool.TryAcquire("gunship", out _), Is.False);

            pool.Release(player);
            Assert.That(pool.TryAcquire("player", out var reused), Is.True);
            Assert.That(reused, Is.SameAs(player));
            Assert.That(raider, Is.Not.SameAs(reused));
            Assert.That(pool.CreatedCount, Is.EqualTo(2));
        }

        [Test]
        public void Keyed_pool_rejects_foreign_and_duplicate_releases()
        {
            var pool = new SeaKeyedBoundedPool<string, PooledValue>(
                _ => new PooledValue(1),
                _ => { },
                maximumCapacity: 1);
            Assert.That(pool.TryAcquire("player", out var player), Is.True);

            Assert.Throws<System.InvalidOperationException>(() =>
                pool.Release(new PooledValue(2)));
            pool.Release(player);
            Assert.Throws<System.InvalidOperationException>(() => pool.Release(player));
        }

        [Test]
        public void Dirty_state_coalesces_changes_until_the_consumer_applies_them()
        {
            var dirty = new SeaDirtyState(initiallyDirty: false);

            Assert.That(dirty.TryConsume(), Is.False);
            dirty.Mark();
            dirty.Mark();

            Assert.That(dirty.TryConsume(), Is.True);
            Assert.That(dirty.TryConsume(), Is.False);
        }

        [Test]
        public void Row_registry_handles_insert_update_delete_and_resubscribe_without_duplicates()
        {
            var registry = new SeaRowRegistry<ulong, string>();

            registry.Upsert(7, "inserted");
            registry.Upsert(7, "updated");
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetValue(7, out var updated), Is.True);
            Assert.That(updated, Is.EqualTo("updated"));

            Assert.That(registry.Remove(7), Is.True);
            registry.Clear();
            registry.Upsert(7, "resubscribed");

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGetValue(7, out var resubscribed), Is.True);
            Assert.That(resubscribed, Is.EqualTo("resubscribed"));
        }

        [TestCase(SeaPresentationPlatform.MacOS, 250)]
        [TestCase(SeaPresentationPlatform.WebGL, 100)]
        public void Platform_ship_limits_match_the_rendering_budget(
            SeaPresentationPlatform platform,
            int expected)
        {
            Assert.That(SeaPresentationRules.VisibleShipLimit(platform), Is.EqualTo(expected));
        }

        [Test]
        public void Targeted_and_volley_endpoint_ships_remain_visible_at_distance()
        {
            Assert.That(
                SeaPresentationRules.LevelFor(distance: 160f, isRelevantEndpoint: true),
                Is.EqualTo(SeaPresentationLevel.Distant));
            Assert.That(
                SeaPresentationRules.LevelFor(distance: 160f, isRelevantEndpoint: false),
                Is.EqualTo(SeaPresentationLevel.Hidden));
        }

        [Test]
        public void Interpolation_buffer_samples_uneven_ten_hertz_updates_smoothly()
        {
            var buffer = new SeaInterpolationBuffer();
            buffer.Push(new Vector3(0f, 0f, 0f), headingDegrees: 350f, receivedAt: 0d);
            buffer.Push(new Vector3(10f, 0f, 0f), headingDegrees: 10f, receivedAt: 0.1d);

            var first = buffer.Sample(renderedAt: 0.15d, interpolationDelay: 0.1d);
            Assert.That(first.Position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(0f, first.HeadingDegrees), Is.EqualTo(0f).Within(0.001f));

            buffer.Push(new Vector3(25f, 0f, 0f), headingDegrees: 40f, receivedAt: 0.25d);
            var uneven = buffer.Sample(renderedAt: 0.30d, interpolationDelay: 0.1d);
            Assert.That(uneven.Position.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(uneven.HeadingDegrees, Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void Burst_visibility_job_computes_plain_data_distances()
        {
            var positions = new NativeArray<float2>(2, Allocator.TempJob);
            var distances = new NativeArray<float>(2, Allocator.TempJob);
            try
            {
                positions[0] = new float2(3f, 4f);
                positions[1] = new float2(-6f, 8f);

                var job = new SeaVisibilityDistanceJob
                {
                    Positions = positions,
                    Origin = float2.zero,
                    SquaredDistances = distances,
                };
                job.Run(positions.Length);

                Assert.That(distances[0], Is.EqualTo(25f).Within(0.001f));
                Assert.That(distances[1], Is.EqualTo(100f).Within(0.001f));
            }
            finally
            {
                positions.Dispose();
                distances.Dispose();
            }
        }

        private sealed class PooledValue
        {
            public PooledValue(int id) => Id = id;

            public int Id { get; }
        }
    }
}
#endif
