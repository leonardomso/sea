using NUnit.Framework;
using Sea.Client;
using UnityEngine;

namespace Sea.Tests.EditMode
{
    /// <summary>
    /// The client half of server/spacetimedb/tests/RouteRulesTests.cs. A ship the player is
    /// drawing has to walk her route exactly where the server walks it, or the hull is drawn
    /// somewhere the authority will not agree she is and every leg ends in a correction.
    /// </summary>
    public sealed class SeaRouteRulesTests
    {
        [Test]
        public void SheWalksHerRouteAtConstantSpeed()
        {
            var route = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) };

            var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

            Assert.AreEqual(5f, step.Position.x, 0.0001f);
            Assert.AreEqual(0f, step.Position.y, 0.0001f);
            Assert.AreEqual(90f, step.HeadingDegrees, 0.0001f);
        }

        [Test]
        public void ACornerIsTurnedInsideOneStepWithNoDistanceLost()
        {
            var route = new[]
            {
                new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 4f),
            };

            var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

            Assert.AreEqual(3f, step.Position.x, 0.0001f);
            Assert.AreEqual(2f, step.Position.y, 0.0001f);
            Assert.AreEqual(1, step.WaypointIndex);
        }

        [Test]
        public void SheStopsOnTheLastWaypointAndNotPastIt()
        {
            var route = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f) };

            var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 5f);

            Assert.AreEqual(2f, step.Position.x, 0.0001f);
            Assert.IsTrue(step.Arrived);
        }

        [Test]
        public void AShipWithNowhereLeftToGoKeepsHerPlaceAndHerBearing()
        {
            var route = new[] { new Vector2(7f, 7f) };

            var step = SeaRouteRules.Advance(route, 0, new Vector2(7f, 7f), 41f, 5f);

            Assert.AreEqual(7f, step.Position.x, 0.0001f);
            Assert.AreEqual(41f, step.HeadingDegrees, 0.0001f);
            Assert.IsTrue(step.Arrived);
        }

        [Test]
        public void ATickWithNoWayOnHerIsNotAnArrival()
        {
            var route = new[] { new Vector2(0f, 0f), new Vector2(100f, 0f) };

            var step = SeaRouteRules.Advance(route, 0, new Vector2(0f, 0f), 90f, 0f);

            Assert.AreEqual(0f, step.Position.x, 0.0001f);
            Assert.IsFalse(step.Arrived);
        }

        [Test]
        public void TheClientAndTheServerAgreeOnTheSameRoute()
        {
            // SEA_5 section 13 test 1, the same assertion the server makes in RouteRulesTests.
            // If these two ever disagree the local ship is drawn where the server will not
            // agree she is.
            var route = new[] { new Vector2(50f, 50f), new Vector2(250f, 50f) };
            var position = new Vector2(50f, 50f);
            var index = 0;

            for (var tick = 0; tick < 100; tick++)
            {
                var step = SeaRouteRules.Advance(route, index, position, 90f, 5.0f * 0.1f);
                position = step.Position;
                index = step.WaypointIndex;
            }

            Assert.AreEqual(100f, position.x, 0.01f);
        }

        [Test]
        public void ZeroIsNorthAndNinetyIsEast()
        {
            Assert.AreEqual(
                0f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(0f, -1f), 0f), 0.001f);
            Assert.AreEqual(
                90f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(1f, 0f), 0f), 0.001f);
            Assert.AreEqual(
                180f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(0f, 1f), 0f), 0.001f);
            Assert.AreEqual(
                270f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(-1f, 0f), 0f), 0.001f);
            Assert.AreEqual(
                41f, SeaGeometry.HeadingTo(Vector2.one, Vector2.one, 41f), 0.001f);
        }

        [Test]
        public void ABearingIsAlwaysReadAsZeroToThreeSixty()
        {
            Assert.AreEqual(350f, SeaGeometry.NormalizeAngle(-10f), 0.001f);
            Assert.AreEqual(10f, SeaGeometry.NormalizeAngle(370f), 0.001f);
            Assert.AreEqual(0f, SeaGeometry.NormalizeAngle(360f), 0.001f);
            Assert.AreEqual(
                315f, SeaGeometry.HeadingTo(Vector2.zero, new Vector2(-1f, -1f), 0f), 0.001f);
        }
    }
}
