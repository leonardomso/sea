using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    /// <summary>
    /// The client half of server/spacetimedb/tests/ShipStopsAtTheMarkTests.cs. A ship the
    /// player is drawing has to come to rest wherever the server would rest her, or the
    /// prediction draws a circle the authority is not sailing.
    /// </summary>
    public sealed class SeaShipStopsAtTheMarkTests
    {
        // The sloop as the seed content rates her, sailed by the shared handling figures, in
        // squares per second. These read 24, 10 and 30 while a square was ten world units; the
        // server's half of this file was re-derived to 2.4, 1 and 3 and this half was not, so
        // the two suites stopped sailing the same ship.
        private static readonly SeaSailingParameters Sloop = new(2.4f, 1f, 3f, 150f);

        private const float Tick = 0.1f;

        private static (float Seconds, float MissedBy) SailTo(
            float distance,
            float bearingDegrees,
            float startingSpeed,
            int tickLimit = 3000)
        {
            // World space, where the client's own rule puts heading 0 on +z. The chart's flip
            // between -y north and +z north lives in SeaChartCoordinates and stops there.
            var radians = bearingDegrees * Mathf.Deg2Rad;
            var destination = new Vector3(
                distance * Mathf.Sin(radians), 0f, distance * Mathf.Cos(radians));
            var state = new SeaSailingState(Vector3.zero, 0f, startingSpeed);
            for (var tick = 1; tick <= tickLimit; tick++)
            {
                var step = SeaSailingRules.Step(state, destination, false, Sloop, Tick);
                state = new SeaSailingState(step.Position, step.HeadingDegrees, step.Speed);
                if (step.Arrived)
                {
                    return (tick * Tick, Vector3.Distance(step.Position, destination));
                }
            }

            return (float.PositiveInfinity, float.PositiveInfinity);
        }

        [TestCase(0f)]
        [TestCase(12f)]
        [TestCase(24f)]
        public void A_ship_comes_to_rest_at_the_mark_from_every_bearing(float startingSpeed)
        {
            for (var distance = 1f; distance <= 140f; distance += 5f)
            {
                for (var bearing = 0f; bearing < 360f; bearing += 15f)
                {
                    var (seconds, missedBy) = SailTo(distance, bearing, startingSpeed);

                    Assert.That(
                        seconds,
                        Is.Not.EqualTo(float.PositiveInfinity),
                        $"She never stopped: {distance} units off, bearing {bearing}.");
                    Assert.That(
                        missedBy,
                        Is.LessThanOrEqualTo(SeaSailingRules.ArrivalRadius + 0.001f),
                        $"She rested {missedBy} units off the mark at bearing {bearing}.");
                }
            }
        }

        [TestCase(0.3f, 90f)]
        [TestCase(0.2f, 120f)]
        [TestCase(0.1f, 180f)]
        [TestCase(0.5f, 60f)]
        public void A_short_click_off_the_bow_no_longer_circles(float distance, float bearing)
        {
            var (seconds, _) = SailTo(distance, bearing, 0f);

            Assert.That(seconds, Is.LessThanOrEqualTo(4f));
        }

        /// <summary>
        /// The number the server publishes and the number the client predicts with are the
        /// same number, and nothing but a matching edit may change either.
        /// </summary>
        [Test]
        public void The_arrival_radius_matches_the_server()
        {
            Assert.That(SeaSailingRules.ArrivalRadius, Is.EqualTo(0.15f));
        }
    }
}
