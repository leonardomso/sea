using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    /// <summary>
    /// The line a captain sees drawn over the water is the route the server is sailing her
    /// along (SEA_5 §4.3), so these pin the two things that can make it a lie: the shape of
    /// the line, and the ground it is drawn on.
    /// </summary>
    public sealed class SeaRouteViewTests
    {
        [Test]
        public void ARouteIsDrawnAsOneLineThroughEveryWaypoint()
        {
            var points = SeaRouteView.BuildLine(
                new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f) },
                0f);

            Assert.AreEqual(3, points.Length);
        }

        [Test]
        public void NoRouteDrawsNothing()
        {
            Assert.AreEqual(0, SeaRouteView.BuildLine(System.Array.Empty<Vector2>(), 0f).Length);
        }

        [Test]
        public void AShipWithNoCourseDrawsNothing()
        {
            Assert.AreEqual(0, SeaRouteView.BuildLine(null, 0f).Length);
        }

        [Test]
        public void TheLineIsDrawnInTheWorldTheShipIsDrawnIn()
        {
            // Chart y grows south and Unity z grows north, so a waypoint is reflected about the
            // middle of the map exactly as SeaChartCoordinates.ToWorld reflects the hull that
            // follows it. Reading the chart y straight into z would draw every course mirrored.
            var points = SeaRouteView.BuildLine(new[] { new Vector2(120f, 30f) }, -0.4f);

            Assert.AreEqual(SeaChartCoordinates.ToWorld(120f, 30f, -0.4f), points[0]);
            Assert.AreEqual(370f, points[0].z, 0.0001f);
        }

        [Test]
        public void TheLineIsLaidAtTheHeightItIsAskedFor()
        {
            var points = SeaRouteView.BuildLine(new[] { new Vector2(1f, 2f) }, 0.25f);

            Assert.AreEqual(0.25f, points[0].y, 0.0001f);
        }
    }
}
