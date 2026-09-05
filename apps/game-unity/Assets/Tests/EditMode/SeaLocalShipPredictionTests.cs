using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    /// <summary>
    /// Dead reckoning for the ship the player steers (SEA_5 12.2 and 12.3). She is drawn where
    /// the server will agree she is, not a render delay behind it, so a click turns the hull on
    /// the frame it is made.
    /// </summary>
    public sealed class SeaLocalShipPredictionTests
    {
        private static Vector2[] Leg(Vector2 from, Vector2 to) => new[] { from, to };

        [Test]
        public void PredictionUsesTheSpeedTheServerSentNotTheHullsRatedSpeed()
        {
            // The server says she is doing 4.25 in a storm; her rated speed is 5.0.
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(
                position: new Vector2(50f, 50f),
                headingDegrees: 90f,
                effectiveSpeed: 4.25f,
                route: Leg(new Vector2(50f, 50f), new Vector2(250f, 50f)),
                routeVersion: 1);

            prediction.Advance(1.0f);

            Assert.AreEqual(54.25f, prediction.Position.x, 0.01f);
        }

        [Test]
        public void ANewRouteVersionReplacesTheOldOneWithoutASnap()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
                Leg(new Vector2(50f, 50f), new Vector2(250f, 50f)), 1);
            prediction.Advance(1f);

            prediction.OnServerUpdate(new Vector2(55f, 50f), 90f, 5f,
                Leg(new Vector2(55f, 50f), new Vector2(55f, 250f)), 2);

            Assert.AreEqual(55f, prediction.Position.x, 0.01f);
            Assert.AreEqual(50f, prediction.Position.y, 0.01f);
        }

        [Test]
        public void HeadingCatchesUpOverFourHundredMillisecondsRatherThanSnapping()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(0f, 0f), 0f, 5f,
                Leg(new Vector2(0f, 0f), new Vector2(0f, 100f)), 1);

            prediction.OnServerUpdate(new Vector2(0f, 0f), 180f, 5f,
                Leg(new Vector2(0f, 0f), new Vector2(0f, -100f)), 2);
            prediction.Advance(0.2f);

            Assert.AreEqual(90f, prediction.DrawnHeadingDegrees, 5f);
        }

        [Test]
        public void TheFirstBearingTheServerGivesIsDrawnStraightAway()
        {
            // There is nothing to catch up from on the first update, and easing out of a
            // heading of zero would swing every ship in the fleet round from north on spawn.
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(0f, 0f), 270f, 5f,
                Leg(new Vector2(0f, 0f), new Vector2(-100f, 0f)), 1);

            Assert.AreEqual(270f, prediction.DrawnHeadingDegrees, 0.01f);
        }

        [Test]
        public void SmallDisagreementsAreEasedAwayRatherThanSnapped()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
                Leg(new Vector2(50f, 50f), new Vector2(250f, 50f)), 1);
            prediction.Advance(1f);

            // The server says 54.4 where we drew 55.0: six tenths of a square, which is inside
            // the tolerance, so the drawn hull stays put and closes the gap over the next few
            // frames.
            prediction.OnServerUpdate(new Vector2(54.4f, 50f), 90f, 5f,
                Leg(new Vector2(54.4f, 50f), new Vector2(250f, 50f)), 1);

            Assert.AreEqual(55f, prediction.Position.x, 0.01f);
        }

        [Test]
        public void AnEasedDisagreementIsGoneInsideTheEaseWindow()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
                Leg(new Vector2(50f, 50f), new Vector2(250f, 50f)), 1);
            prediction.Advance(1f);
            prediction.OnServerUpdate(new Vector2(54.4f, 50f), 90f, 5f,
                Leg(new Vector2(54.4f, 50f), new Vector2(250f, 50f)), 1);

            // Six tenths of a square behind, then a further second of sailing: she is where
            // the server would have her, not six tenths ahead of it.
            prediction.Advance(1f);

            Assert.AreEqual(59.4f, prediction.Position.x, 0.01f);
        }

        [Test]
        public void AnErrorOverOneSquareSnapsToTheServer()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f,
                Leg(new Vector2(50f, 50f), new Vector2(250f, 50f)), 1);
            prediction.Advance(1f);

            // Two squares out. Easing that away would leave the hull wrong for most of a
            // second, so she is put where the server says she is (SEA_5 12.3).
            prediction.OnServerUpdate(new Vector2(53f, 50f), 90f, 5f,
                Leg(new Vector2(53f, 50f), new Vector2(250f, 50f)), 1);

            Assert.AreEqual(53f, prediction.Position.x, 0.01f);
        }

        [Test]
        public void AShipWithNoRouteIsWhereTheServerLeftHer()
        {
            var prediction = new SeaLocalShipPrediction();
            prediction.OnServerUpdate(new Vector2(50f, 50f), 90f, 5f, null, 0);

            prediction.Advance(1f);

            Assert.AreEqual(50f, prediction.Position.x, 0.01f);
            Assert.AreEqual(50f, prediction.Position.y, 0.01f);
        }
    }
}
