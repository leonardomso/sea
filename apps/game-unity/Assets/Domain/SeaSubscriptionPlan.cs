using System;
using System.Collections.Generic;
using System.Linq;

namespace Sea.Client
{
    public static class SeaSubscriptionPlan
    {
        public static IReadOnlyList<string> Initial(string ownerSqlLiteral)
        {
            if (string.IsNullOrWhiteSpace(ownerSqlLiteral))
            {
                throw new ArgumentException("Owner SQL literal is required.", nameof(ownerSqlLiteral));
            }

            return new[]
            {
                "SELECT * FROM world_state",
                "SELECT * FROM environment_state",
                "SELECT * FROM ammo_definition",
                "SELECT * FROM ability_definition",
                "SELECT * FROM npc_definition",
                "SELECT * FROM level_definition",
                $"SELECT * FROM command_result_event WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM encounter_reward_event WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM encounter_reward WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_ownership WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_progression WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_command_state WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_clock WHERE owner = {ownerSqlLiteral}",
            };
        }

        public static IReadOnlyList<string> Player(ulong shipEntityId)
        {
            return new[]
            {
                $"SELECT * FROM ship WHERE entity_id = {shipEntityId}",
                $"SELECT * FROM ship_movement WHERE entity_id = {shipEntityId}",
                $"SELECT * FROM inventory WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM ship_status WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM cooldown WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM ship_channel WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM combat_event WHERE owner_entity_id = {shipEntityId}",
                $"SELECT * FROM volley WHERE is_active = true AND " +
                $"(source_entity_id = {shipEntityId} OR target_entity_id = {shipEntityId})",
            };
        }

        public static IReadOnlyList<string> Focus(
            ulong localShipEntityId,
            ulong targetEntityId) => Focus(localShipEntityId, new[] { targetEntityId });

        public static IReadOnlyList<string> Focus(
            ulong localShipEntityId,
            IEnumerable<ulong> targetEntityIds)
        {
            if (localShipEntityId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localShipEntityId));
            }

            if (targetEntityIds == null)
            {
                throw new ArgumentNullException(nameof(targetEntityIds));
            }

            var targets = targetEntityIds
                .Where(entityId => entityId != 0 && entityId != localShipEntityId)
                .Distinct()
                .OrderBy(entityId => entityId)
                .ToArray();
            if (targets.Length == 0)
            {
                return Array.Empty<string>();
            }

            var targetPredicate = string.Join(
                " OR ",
                targets.Select(entityId => $"entity_id = {entityId}"));
            var statusPredicate = string.Join(
                " OR ",
                targets.Select(entityId => $"ship_entity_id = {entityId}"));
            var volleyPredicate = string.Join(
                " OR ",
                new[] { localShipEntityId }
                    .Concat(targets)
                    .SelectMany(entityId => new[]
                    {
                        $"source_entity_id = {entityId}",
                        $"target_entity_id = {entityId}",
                    }));
            return new[]
            {
                $"SELECT * FROM ship WHERE {targetPredicate}",
                $"SELECT * FROM ship_movement WHERE {targetPredicate}",
                $"SELECT * FROM ship_status WHERE {statusPredicate}",
                $"SELECT * FROM cooldown WHERE {statusPredicate}",
                $"SELECT * FROM ship_channel WHERE {statusPredicate}",
                $"SELECT * FROM volley WHERE is_active = true AND ({volleyPredicate})",
            };
        }

        public static IReadOnlyList<string> Spatial(int chunkX, int chunkY, int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            var minimumX = chunkX - radius;
            var maximumX = chunkX + radius;
            var minimumY = chunkY - radius;
            var maximumY = chunkY + radius;
            var bounds =
                $"chunk_x >= {minimumX} AND chunk_x <= {maximumX} " +
                $"AND chunk_y >= {minimumY} AND chunk_y <= {maximumY}";

            return new[]
            {
                $"SELECT * FROM ship WHERE is_active = true AND {bounds}",
                $"SELECT * FROM ship_movement WHERE is_active = true AND {bounds}",
                $"SELECT * FROM volley WHERE is_active = true AND {bounds}",
                $"SELECT * FROM loot WHERE is_active = true AND {bounds}",
                $"SELECT * FROM world_object WHERE is_active = true AND {bounds}",
                $"SELECT * FROM current_zone WHERE is_active = true AND {bounds}",
            };
        }
    }
}
