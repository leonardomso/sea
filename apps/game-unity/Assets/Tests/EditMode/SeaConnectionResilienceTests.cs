#if UNITY_EDITOR
using NUnit.Framework;
using Sea.Client;
using SpacetimeDB;
using UnityEngine;

namespace Sea.Tests
{
    /// <summary>
    /// How the client survives a bad connection: a stale identity, an unauthorized upgrade, a
    /// transient outage, and the command line that points a smoke run at its own database.
    /// </summary>
    public sealed partial class SeaProjectTests
    {
        [Test]
        public void Auth_token_store_can_clear_a_stale_local_identity()
        {
            const string testKey = "sea.tests.identity-token";
            var tokens = new SeaAuthTokenStore(testKey);
            tokens.Save("stale-token");

            tokens.Clear();

            Assert.That(tokens.Token, Is.Empty);
        }

        [Test]
        public void Unauthorized_cached_identity_is_cleared_and_retried_anonymously()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(401, "Unauthorized"),
                attemptedWithToken: true,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.ClearIdentityAndRetry));
            Assert.That(decision.DelaySeconds, Is.Zero);
        }

        [Test]
        public void Unauthorized_anonymous_connection_stops_retrying()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(401, "Unauthorized"),
                attemptedWithToken: false,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.Stop));
        }

        [Test]
        public void Transient_connection_failures_use_bounded_backoff()
        {
            var first = SeaConnectionRecoveryPolicy.Decide(
                new System.TimeoutException("offline"),
                attemptedWithToken: false,
                transientFailureCount: 0);
            var repeated = SeaConnectionRecoveryPolicy.Decide(
                new System.TimeoutException("offline"),
                attemptedWithToken: false,
                transientFailureCount: 20);

            Assert.That(first.Action, Is.EqualTo(SeaConnectionRecoveryAction.RetryAfterDelay));
            Assert.That(first.DelaySeconds, Is.EqualTo(2f));
            Assert.That(repeated.DelaySeconds, Is.EqualTo(30f));
        }

        [Test]
        public void Missing_database_is_a_permanent_connection_failure()
        {
            var decision = SeaConnectionRecoveryPolicy.Decide(
                new WebSocketUpgradeException(404, "Not Found"),
                attemptedWithToken: false,
                transientFailureCount: 0);

            Assert.That(decision.Action, Is.EqualTo(SeaConnectionRecoveryAction.Stop));
        }

        [Test]
        public void World_material_factory_always_returns_a_runtime_material()
        {
            var material = SeaMaterialFactory.Create(Color.white);

            Assert.That(material, Is.Not.Null);
            Object.DestroyImmediate(material);
        }

        [TestCase(new[] { "game", "-seaDatabaseName", "sea-smoke" }, "sea-smoke")]
        [TestCase(new[] { "game", "-seaDatabaseName=sea-smoke" }, "sea-smoke")]
        [TestCase(new[] { "game" }, "sea-local")]
        public void Runtime_database_can_be_isolated_by_command_line(
            string[] arguments,
            string expected)
        {
            Assert.That(SeaClientOptions.DatabaseName(arguments, "sea-local"), Is.EqualTo(expected));
        }
    }
}
#endif
