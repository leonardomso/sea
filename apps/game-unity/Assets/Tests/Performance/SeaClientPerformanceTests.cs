using NUnit.Framework;
using Sea.Client;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace Sea.Tests.Performance
{
    public sealed class SeaClientPerformanceTests
    {
        private const int ShipCount = 250;
        private static readonly ProfilerMarker InterpolationMarker =
            new("Sea.Tests.Interpolation250");
        private readonly GameObject[] ships = new GameObject[ShipCount];
        private readonly SeaMotionTimeline[] timelines = new SeaMotionTimeline[ShipCount];
        private NativeArray<float2> positions;
        private NativeArray<float> squaredDistances;

        [SetUp]
        public void SetUp()
        {
            for (var index = 0; index < ships.Length; index++)
            {
                ships[index] = new GameObject($"Performance Ship {index}");
                timelines[index] = new SeaMotionTimeline();
                timelines[index].Push(10, new Vector3(index, 0f, index), index % 360);
                timelines[index].Push(11, new Vector3(index + 1f, 0f, index), index % 360 + 5f);
            }

            positions = new NativeArray<float2>(ShipCount, Allocator.Persistent);
            squaredDistances = new NativeArray<float>(ShipCount, Allocator.Persistent);
            for (var index = 0; index < ShipCount; index++)
            {
                positions[index] = new float2(index, index * 0.5f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ship in ships)
            {
                Object.DestroyImmediate(ship);
            }

            positions.Dispose();
            squaredDistances.Dispose();
        }

        [Test, Performance]
        public void Interpolating_250_visible_ships_has_a_repeatable_fixture()
        {
            Measure.Method(() =>
                {
                    using (InterpolationMarker.Auto())
                    {
                        for (var index = 0; index < ships.Length; index++)
                        {
                            var sample = timelines[index].Sample(10.5d);
                            ships[index].transform.SetPositionAndRotation(
                                sample.Position,
                                Quaternion.Euler(0f, sample.HeadingDegrees, 0f));
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(30)
                .IterationsPerMeasurement(10)
                .GC()
                .Run();
        }

        [Test, Performance]
        public void Burst_visibility_for_250_ships_has_no_managed_collection_work()
        {
            Measure.Method(() =>
                {
                    var job = new SeaVisibilityDistanceJob
                    {
                        Positions = positions,
                        Origin = new float2(40f, 25f),
                        SquaredDistances = squaredDistances,
                    };
                    job.Schedule(ShipCount, 64).Complete();
                })
                .WarmupCount(10)
                .MeasurementCount(30)
                .IterationsPerMeasurement(10)
                .GC()
                .Run();
        }
    }
}
