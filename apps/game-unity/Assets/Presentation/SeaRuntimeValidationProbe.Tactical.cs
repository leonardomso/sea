using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe
    {
        private const byte NotAvailableRejectionCode = 21;

        private static readonly ShipCommand[] RetiredCommands =
        {
            new ShipCommand.ActivateAbility(new ActivateAbilityCommand("full_sail")),
            new ShipCommand.StartBoarding(new StartBoardingCommand()),
        };

        private int retiredCommandIndex;
        private bool retiredCommandPending;
        private ulong retiredCommandId;
        private bool tacticalHullSampled;
        private bool tacticalStormCourseRequested;
        private bool tacticalDamageObserved;
        private bool tacticalRetreatRequested;
        private bool tacticalRepairRequested;
        private bool tacticalRepairObserved;
        private uint tacticalInitialHull;
        private uint tacticalDamagedHull;
        private Vector2 tacticalRetreat;
        private float nextTacticalCourseTime;
        private float tacticalRepairRequestedAt;

        private void ObserveTactical(Ship player)
        {
            var world = connection.Connection.Db.WorldState.Id.Find(1);
            if (world == null)
            {
                return;
            }

            var worldTick = connection.CurrentWorldTick;
            if (!ObserveRetiredCommands(player))
            {
                return;
            }

            if (!SampleTacticalHull(player))
            {
                return;
            }

            var storm = connection.Connection.Db.WorldObject.Iter()
                .FirstOrDefault(item => item.Kind == "storm" && item.IsActive);
            if (storm == null)
            {
                SailToPredictedStorm(worldTick);
                return;
            }

            ObserveStormAndRepair(player, storm);
        }

        /// <summary>
        /// Abilities and boarding left the game with 1b, but a stale client can still put those
        /// variants on the wire, so the module keeps them and answers <c>NotAvailable</c>. This
        /// walks that path in the built player: every retired command must come back rejected
        /// with the stable code and must never move the ship.
        /// </summary>
        private bool ObserveRetiredCommands(Ship player)
        {
            if (retiredCommandIndex >= RetiredCommands.Length)
            {
                return true;
            }

            if (!retiredCommandPending)
            {
                if (!CanIssueTacticalCommand(player))
                {
                    return false;
                }

                retiredCommandId = connection.IssueCommand(
                    RetiredCommands[retiredCommandIndex],
                    "runtime retired command");
                retiredCommandPending = retiredCommandId != 0;
                return false;
            }

            if (connection.AnsweredCommandId < retiredCommandId)
            {
                return false;
            }

            if (connection.AnsweredRejectionCode != NotAvailableRejectionCode)
            {
                Debug.LogError(
                    "Sea runtime saw a retired command answered with code " +
                    $"{connection.AnsweredRejectionCode} instead of {NotAvailableRejectionCode}.",
                    this);
            }

            retiredCommandPending = false;
            retiredCommandIndex++;
            return retiredCommandIndex >= RetiredCommands.Length;
        }

        /// <summary>
        /// The storm leg measures damage against the hull the probe started with, so it needs one
        /// clean reading before the weather is allowed to bite.
        /// </summary>
        private bool SampleTacticalHull(Ship player)
        {
            if (tacticalHullSampled)
            {
                return true;
            }

            if (!CanIssueTacticalCommand(player))
            {
                return false;
            }

            tacticalInitialHull = player.Hull;
            tacticalHullSampled = true;
            return true;
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
            var playerPosition = LivePosition(player);
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
                MarkRuntimeMilestone(SeaRuntimeMilestone.Tactical);
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
