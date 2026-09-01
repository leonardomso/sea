using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe
    {
        private bool tacticalAbilityRequested;
        private bool tacticalAbilityObserved;
        private bool tacticalStormCourseRequested;
        private bool tacticalDamageObserved;
        private bool tacticalRetreatRequested;
        private bool tacticalRepairRequested;
        private bool tacticalRepairObserved;
        private uint tacticalInitialHull;
        private uint tacticalDamagedHull;
        private Vector2 tacticalRetreat;
        private float nextTacticalCourseTime;
        private float tacticalAbilityRequestedAt;
        private float tacticalRepairRequestedAt;

        private void ObserveTactical(Ship player)
        {
            var world = connection.Connection.Db.WorldState.Id.Find(1);
            if (world == null)
            {
                return;
            }

            if (!ObserveTacticalAbility(player, world))
            {
                return;
            }

            var storm = connection.Connection.Db.WorldObject.Iter()
                .FirstOrDefault(item => item.Kind == "storm" && item.IsActive);
            if (storm == null)
            {
                SailToPredictedStorm(world.Tick);
                return;
            }

            ObserveStormAndRepair(player, storm);
        }

        private bool ObserveTacticalAbility(Ship player, WorldState world)
        {
            if (!tacticalAbilityRequested)
            {
                if (!CanIssueTacticalCommand(player))
                {
                    return false;
                }

                tacticalInitialHull = player.Hull;
                tacticalAbilityRequested = true;
                tacticalAbilityRequestedAt = Time.unscaledTime;
                Issue(
                    new ShipCommand.ActivateAbility(new ActivateAbilityCommand("full_sail")),
                    "runtime activate full sail");
                return false;
            }

            if (tacticalAbilityObserved)
            {
                return true;
            }

            var status = connection.Connection.Db.ShipStatus.ByShip
                .Filter(player.EntityId)
                .FirstOrDefault(item => item.StatusType == "full_sail" && item.IsActive);
            var cooldown = connection.Connection.Db.Cooldown.ByShip
                .Filter(player.EntityId)
                .FirstOrDefault(item => item.CooldownType == "full_sail");
            tacticalAbilityObserved = status != null && cooldown != null &&
                cooldown.ReadyAtTick > world.Tick;
            if (!tacticalAbilityObserved &&
                SeaRuntimeValidationRules.ShouldRetryTacticalCommand(
                    observed: false,
                    tacticalAbilityRequestedAt,
                    Time.unscaledTime))
            {
                tacticalAbilityRequested = false;
            }

            return tacticalAbilityObserved;
        }

        private void SailToPredictedStorm(ulong worldTick)
        {
            if (tacticalStormCourseRequested && Time.unscaledTime < nextTacticalCourseTime)
            {
                return;
            }

            var searchPosition = SeaRuntimeValidationRules.SeededStormPosition(worldTick);
            SetCourse(searchPosition.x, searchPosition.y);
            tacticalStormCourseRequested = true;
            nextTacticalCourseTime = Time.unscaledTime + 1f;
        }

        private void ObserveStormAndRepair(Ship player, WorldObject storm)
        {
            var playerPosition = new Vector2(player.PositionX, player.PositionY);
            var stormPosition = new Vector2(storm.PositionX, storm.PositionY);
            if (!tacticalDamageObserved)
            {
                ObserveStormDamage(player, playerPosition, storm, stormPosition);
                return;
            }

            if (!tacticalRepairRequested)
            {
                RequestRepairOutsideStorm(player, playerPosition, storm, stormPosition);
                return;
            }

            ObserveRepair(player);
        }

        private void ObserveStormDamage(
            Ship player,
            Vector2 playerPosition,
            WorldObject storm,
            Vector2 stormPosition)
        {
            if (player.Hull < tacticalInitialHull &&
                SeaRuntimeValidationRules.HasStormExposure(player.EnvironmentExposureCode))
            {
                tacticalDamageObserved = true;
                tacticalDamagedHull = player.Hull;
                var outward = (playerPosition - stormPosition).normalized;
                if (outward.sqrMagnitude < 0.5f)
                {
                    outward = Vector2.right;
                }

                tacticalRetreat = SeaChartCoordinates.ClampToMap(
                    playerPosition + outward * (storm.Radius + 18f));
                SetCourse(tacticalRetreat.x, tacticalRetreat.y);
                tacticalRetreatRequested = true;
                return;
            }

            SailToStorm(storm);
        }

        private void RequestRepairOutsideStorm(
            Ship player,
            Vector2 playerPosition,
            WorldObject storm,
            Vector2 stormPosition)
        {
            if (!tacticalRetreatRequested ||
                Vector2.Distance(playerPosition, stormPosition) <= storm.Radius + 5f)
            {
                SailToRetreat();
                return;
            }

            if (!CanIssueTacticalCommand(player))
            {
                return;
            }

            StopCourse();
            Issue(
                new ShipCommand.StartRepair(new StartRepairCommand()),
                "runtime start repair");
            tacticalRepairRequested = true;
            tacticalRepairRequestedAt = Time.unscaledTime;
        }

        private void ObserveRepair(Ship player)
        {
            var channel = connection.Connection.Db.ShipChannel.ShipEntityId.Find(player.EntityId);
            tacticalRepairObserved |= channel != null && channel.IsActive &&
                channel.ChannelType == "repair";
            if (tacticalRepairObserved && player.Hull > tacticalDamagedHull)
            {
                tacticalEnabledForThisRun = false;
                Debug.Log(
                    "Sea runtime observed tactical ability, storm damage, and progressive repair.",
                    this);
                return;
            }

            if (channel == null &&
                SeaRuntimeValidationRules.ShouldRetryTacticalCommand(
                    observed: false,
                    tacticalRepairRequestedAt,
                    Time.unscaledTime))
            {
                tacticalRepairRequested = false;
                tacticalRepairObserved = false;
            }
        }

        private void SailToStorm(WorldObject storm)
        {
            if (!tacticalStormCourseRequested || Time.unscaledTime >= nextTacticalCourseTime)
            {
                SetCourse(storm.PositionX, storm.PositionY);
                tacticalStormCourseRequested = true;
                nextTacticalCourseTime = Time.unscaledTime + 1f;
            }
        }

        private void SailToRetreat()
        {
            if (Time.unscaledTime >= nextTacticalCourseTime)
            {
                SetCourse(tacticalRetreat.x, tacticalRetreat.y);
                nextTacticalCourseTime = Time.unscaledTime + 1f;
            }
        }

        private static bool CanIssueTacticalCommand(Ship player) =>
            SeaRuntimeValidationRules.CanIssueTacticalCommand(
                player.IsActive,
                player.IsAlive,
                player.ModeCode);
    }
}
