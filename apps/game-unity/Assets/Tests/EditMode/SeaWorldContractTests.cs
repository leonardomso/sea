#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using Sea.Client;

namespace Sea.Tests
{
    public sealed class SeaWorldContractTests
    {
        [Test]
        public void Contract_reads_public_tables_and_sql_columns_from_the_generated_bindings()
        {
            Assert.That(SeaWorldContract.Tables.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(SeaWorldContract.Tables["ship"], Does.Contain("entity_id").And.Contain("chunk_x"));
            Assert.That(SeaWorldContract.Tables["world_state"], Does.Contain("tick_rate_hz"));
            Assert.That(SeaWorldContract.Tables.ContainsKey("player_ship"), Is.False);
        }

        [Test]
        public void Every_client_subscription_query_satisfies_the_contract()
        {
            var queries = SeaSubscriptionPlan.Initial("0x0123")
                .Concat(SeaSubscriptionPlan.Player(7))
                .Concat(SeaSubscriptionPlan.Focus(7, new ulong[] { 8, 9 }))
                .Concat(SeaSubscriptionPlan.Spatial(-3, 2, SeaSubscriptionPlan.SpatialRadius))
                .Append(SeaRuntimeValidationRules.RuntimeNpcSubscriptionQuery)
                .Append(SeaRuntimeValidationRules.RuntimeMovementSubscriptionQuery)
                .ToList();

            Assert.That(queries, Has.Count.GreaterThan(30));
            Assert.DoesNotThrow(() => SeaWorldContract.Require(queries));
        }

        [Test]
        public void Violations_name_the_unknown_table_or_column()
        {
            Assert.That(SeaWorldContract.Violations("SELECT * FROM ship WHERE entity_id = 1 AND chunk_x >= -2"), Is.Empty);
            Assert.That(SeaWorldContract.Violations("SELECT * FROM combat_event WHERE owner_entity_id = 7"), Is.Empty, "event tables are part of the contract");

            var unknownTable = SeaWorldContract.Violations("SELECT * FROM player_ship");
            Assert.That(unknownTable.Count, Is.EqualTo(1));
            Assert.That(unknownTable[0], Does.Contain("player_ship"));

            var unknownColumn = SeaWorldContract.Violations("SELECT * FROM ship WHERE level = 1 OR level = 2");
            Assert.That(unknownColumn.Count, Is.EqualTo(1));
            Assert.That(unknownColumn[0], Does.Contain("ship.level"));

            Assert.That(SeaWorldContract.Violations("ship").Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => SeaWorldContract.Require(new[] { "SELECT * FROM player_ship" }));
        }
    }
}
#endif
