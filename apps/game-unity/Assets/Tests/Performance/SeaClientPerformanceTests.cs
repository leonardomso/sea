using NUnit.Framework;
using Sea.Client;
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

        [SetUp]
        public void SetUp()
        {
            for (var index = 0; index < ships.Length; index++)
            {
                ships[index] = new GameObject($"Performance Ship {index}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ship in ships)
            {
                Object.DestroyImmediate(ship);
            }
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
                            SeaShipMotion.Step(
                                ships[index].transform,
                                new Vector3(index, 0f, index),
                                index % 360,
                                1f / 60f,
                                8f,
                                720f);
                        }
                    }
                })
                .WarmupCount(10)
                .MeasurementCount(30)
                .IterationsPerMeasurement(10)
                .GC()
                .Run();
        }
    }
}
