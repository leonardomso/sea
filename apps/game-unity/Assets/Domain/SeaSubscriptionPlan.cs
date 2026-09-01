using System;
using System.Collections.Generic;

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
                "SELECT * FROM world_object",
                "SELECT * FROM command_result_event",
                $"SELECT * FROM player_ownership WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_progression WHERE owner = {ownerSqlLiteral}",
                $"SELECT * FROM player_command_state WHERE owner = {ownerSqlLiteral}",
            };
        }

        public static IReadOnlyList<string> Player(ulong shipEntityId)
        {
            return new[]
            {
                $"SELECT * FROM ship WHERE entity_id = {shipEntityId}",
                $"SELECT * FROM inventory WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM ship_status WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM cooldown WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM ship_channel WHERE ship_entity_id = {shipEntityId}",
                $"SELECT * FROM combat_event WHERE owner_entity_id = {shipEntityId}",
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
                $"SELECT * FROM volley WHERE is_active = true AND {bounds}",
                $"SELECT * FROM loot WHERE is_active = true AND {bounds}",
                $"SELECT * FROM current_zone WHERE is_active = true AND {bounds}",
            };
        }
    }
}
