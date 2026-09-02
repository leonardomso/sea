#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sea.Client;
using SpacetimeDB.Types;

namespace Sea.Tests
{
    public sealed class SeaSubscriptionTests
    {
        [Test]
        public void Initial_subscription_plan_is_owner_scoped_and_never_unrestricted()
        {
            var queries = SeaSubscriptionPlan.Initial("0xabc123");

            Assert.That(queries, Does.Contain("SELECT * FROM player_ownership WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM player_command_state WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM command_result_event WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM encounter_reward_event WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM encounter_reward WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM player_clock WHERE owner = 0xabc123"));
            Assert.That(queries, Does.Contain("SELECT * FROM world_state"));
            Assert.That(queries, Does.Not.Contain("SELECT * FROM world_object"));
            Assert.That(queries.Any(query => query == "SELECT * FROM ship"), Is.False);
        }

        [Test]
        public void Owner_clock_estimates_ticks_without_global_row_updates()
        {
            Assert.That(SeaWorldClock.Estimate(100, 10, 50d, 51.25d), Is.EqualTo(112));
            Assert.That(SeaWorldClock.Estimate(100, 10, 50d, 49d), Is.EqualTo(100));
            Assert.That(SeaWorldClock.Estimate(100, 0, 50d, 52d), Is.EqualTo(102));
        }

        [Test]
        public void Player_subscription_includes_authoritative_tactical_channels()
        {
            var queries = SeaSubscriptionPlan.Player(42);

            Assert.That(queries, Does.Contain(
                "SELECT * FROM ship_channel WHERE ship_entity_id = 42"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM ship_movement WHERE entity_id = 42"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM combat_event WHERE owner_entity_id = 42"));
            Assert.That(queries, Has.Some.EqualTo(
                "SELECT * FROM volley WHERE is_active = true AND " +
                "(source_entity_id = 42 OR target_entity_id = 42)"));
        }

        [Test]
        public void Focus_subscription_keeps_selected_target_and_volley_endpoints()
        {
            var queries = SeaSubscriptionPlan.Focus(localShipEntityId: 7, targetEntityId: 42);

            Assert.That(queries, Does.Contain("SELECT * FROM ship WHERE entity_id = 42"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM ship_movement WHERE entity_id = 42"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM ship_status WHERE ship_entity_id = 42"));
            Assert.That(queries, Does.Contain(
                "SELECT * FROM volley WHERE is_active = true AND " +
                "(source_entity_id = 7 OR target_entity_id = 7 OR " +
                "source_entity_id = 42 OR target_entity_id = 42)"));
            Assert.That(queries, Does.Not.Contain("SELECT * FROM ship"));
        }

        [Test]
        public void Spatial_subscription_plan_is_bounded_to_nearby_chunks_and_active_rows()
        {
            var queries = SeaSubscriptionPlan.Spatial(chunkX: 4, chunkY: 2, radius: 1);

            Assert.That(queries, Has.Some.Contains("chunk_x >= 3"));
            Assert.That(queries, Has.Some.Contains("chunk_x <= 5"));
            Assert.That(queries, Has.Some.Contains("chunk_y >= 1"));
            Assert.That(queries, Has.Some.Contains("chunk_y <= 3"));
            Assert.That(queries.All(query => query.Contains("is_active = true")), Is.True);
            Assert.That(queries, Has.Some.StartsWith("SELECT * FROM world_object"));
            Assert.That(queries, Has.Some.StartsWith("SELECT * FROM ship_movement"));
        }

        [Test]
        public void Spatial_subscription_radius_covers_the_default_chart_view()
        {
            const float widescreenAspect = 16f / 9f;
            var viewHalfWidth = SeaChartCameraRules.DefaultZoom * widescreenAspect;
            var guaranteedHalfWidth =
                SeaSubscriptionPlan.SpatialRadius * SeaSubscriptionPlan.ChunkSize;

            Assert.That(guaranteedHalfWidth, Is.GreaterThanOrEqualTo(viewHalfWidth));
        }

        [Test]
        public void Only_the_latest_subscription_generation_can_apply()
        {
            var generations = new SeaSubscriptionGeneration();
            var first = generations.Begin();
            var second = generations.Begin();

            Assert.That(generations.IsCurrent(first), Is.False);
            Assert.That(generations.IsCurrent(second), Is.True);
        }

        [Test]
        public void Reconnect_reset_never_reuses_a_subscription_generation()
        {
            var generations = new SeaSubscriptionGeneration();
            var beforeDisconnect = generations.Begin();

            generations.Reset();
            var afterReconnect = generations.Begin();

            Assert.That(generations.IsCurrent(beforeDisconnect), Is.False);
            Assert.That(generations.IsCurrent(afterReconnect), Is.True);
            Assert.That(afterReconnect, Is.GreaterThan(beforeDisconnect));
        }

        [Test]
        public void Spatial_interest_ignores_chunk_boundary_jitter()
        {
            var interest = new SeaSpatialInterest();
            interest.Observe(chunkX: 4, chunkY: 2, nowSeconds: 0d);
            Assert.That(interest.TryTakeDue(0d, out var initial), Is.True);
            Assert.That(initial, Is.EqualTo(new SeaChunk(4, 2)));

            interest.Observe(chunkX: 5, chunkY: 2, nowSeconds: 0.01d);
            interest.Observe(chunkX: 4, chunkY: 2, nowSeconds: 0.05d);

            Assert.That(interest.TryTakeDue(1d, out _), Is.False);
        }

        [Test]
        public void Failed_spatial_interest_retries_after_the_debounce()
        {
            var interest = new SeaSpatialInterest();
            interest.Observe(chunkX: 4, chunkY: 2, nowSeconds: 0d);
            Assert.That(interest.TryTakeDue(0d, out var requested), Is.True);

            interest.Failed(requested, nowSeconds: 0d);

            Assert.That(interest.TryTakeDue(0.14d, out _), Is.False);
            Assert.That(interest.TryTakeDue(0.15d, out var retried), Is.True);
            Assert.That(retried, Is.EqualTo(requested));
        }

        // The generator is the only source of table names, so the plan is checked against
        // the bindings rather than against a hand-maintained list of query strings.
        [Test]
        public void Every_subscribed_table_exists_in_the_generated_bindings()
        {
            var generated = GeneratedTableNames();
            Assert.That(generated, Does.Contain("world_state"));

            var subscribed = SubscribedTableNames();
            Assert.That(subscribed, Is.Not.Empty);
            Assert.That(subscribed, Does.Contain("command_result_event"));
            Assert.That(subscribed, Does.Contain("world_object"));

            var missing = subscribed.Where(name => !generated.Contains(name)).ToArray();
            Assert.That(
                missing,
                Is.Empty,
                "Subscription plan queries tables the generated bindings do not define: "
                    + string.Join(", ", missing));
        }

        private static SortedSet<string> SubscribedTableNames()
        {
            var queries = new List<string>();
            queries.AddRange(SeaSubscriptionPlan.Initial("0xabc123"));
            queries.AddRange(SeaSubscriptionPlan.Player(42));
            queries.AddRange(SeaSubscriptionPlan.Focus(localShipEntityId: 7, targetEntityId: 42));
            queries.AddRange(SeaSubscriptionPlan.Spatial(chunkX: 4, chunkY: 2, radius: 1));

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(string.Join("\n", queries), @"FROM\s+([a-z_][a-z0-9_]*)"))
            {
                names.Add(match.Groups[1].Value);
            }

            return names;
        }

        private static SortedSet<string> GeneratedTableNames()
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var field in typeof(RemoteTables).GetFields(
                BindingFlags.Public | BindingFlags.Instance))
            {
                var typeName = field.FieldType.Name;
                if (typeName.EndsWith("Handle", StringComparison.Ordinal))
                {
                    names.Add(SnakeCase(typeName.Substring(0, typeName.Length - "Handle".Length)));
                }
            }

            return names;
        }

        private static string SnakeCase(string pascalCase)
        {
            var builder = new StringBuilder(pascalCase.Length + 4);
            for (var index = 0; index < pascalCase.Length; index++)
            {
                var character = pascalCase[index];
                if (char.IsUpper(character) && index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        [Test]
        public void Local_client_profile_selects_an_isolated_identity_token()
        {
            var profile = SeaClientOptions.Profile(
                new[] { "game-unity", "-seaProfile", "captain-3" },
                "captain-1");

            Assert.That(profile, Is.EqualTo("captain-3"));
            Assert.That(
                SeaClientOptions.IdentityTokenKey(profile),
                Is.EqualTo("spacetimedb.identity_token.captain-3"));
        }
    }
}
#endif
