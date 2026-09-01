using System;
using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaRuntimeValidationProbe : MonoBehaviour
    {
        private SeaConnectionController connection;
        private bool enabledForThisRun;
        private bool combatEnabledForThisRun;
        private bool tacticalEnabledForThisRun;
        private bool movementValidated;
        private bool moveRequested;
        private bool stopRequested;
        private float speedBeforeStop;
        private Vector2 start;
        private Vector2 destination;
        private bool combatApproachRequested;
        private bool combatTargetRequested;
        private bool combatFireRequested;
        private bool combatLaunchObserved;
        private bool combatHoldRequested;
        private ulong combatTargetId;
        private uint combatInitialHull;
        private uint combatInitialAmmo;
        private float nextCombatCourseTime;
        private float combatFireRequestedAt;
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

        private void Awake()
        {
            enabledForThisRun = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => argument == "-seaRuntimeMoveTest");
            combatEnabledForThisRun = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => argument == "-seaRuntimeCombatTest");
            tacticalEnabledForThisRun = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => argument == "-seaRuntimeTacticalTest");
        }

        public void ConfigureDependencies(SeaConnectionController connectionController) =>
            connection = connectionController;

        private void Update()
        {
            if ((!enabledForThisRun && !combatEnabledForThisRun && !tacticalEnabledForThisRun) ||
                connection?.Connection == null ||
                !connection.IsSubscribed)
            {
                return;
            }

            var ownership = connection.Connection.Db.PlayerOwnership.Owner.Find(connection.LocalIdentity);
            if (ownership == null)
            {
                return;
            }

            var ship = connection.Connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
            if (ship != null)
            {
                if (enabledForThisRun && !movementValidated)
                {
                    ObserveShip(ship);
                }
                else if (combatEnabledForThisRun)
                {
                    ObserveCombat(ship);
                }
                else if (tacticalEnabledForThisRun)
                {
                    ObserveTactical(ship);
                }
            }
        }

        private void ObserveShip(Ship ship)
        {
            var position = new Vector2(ship.PositionX, ship.PositionY);
            if (!moveRequested)
            {
                start = position;
                destination = new Vector2(Mathf.Min(position.x + 12f, 95f), position.y);
                if (Mathf.Approximately(destination.x, position.x))
                {
                    destination.x = Mathf.Max(position.x - 12f, -95f);
                }
                connection.Connection.Reducers.SetCourse(destination.x, destination.y);
                moveRequested = true;
                return;
            }

            var travelled = Vector2.Distance(start, position);
            var remaining = Vector2.Distance(position, destination);
            if (!stopRequested && ship.IsMoving && ship.Speed > 0.5f && travelled > 0.1f && remaining > 0.1f)
            {
                speedBeforeStop = ship.Speed;
                stopRequested = true;
                connection.Connection.Reducers.StopCourse();
                return;
            }

            if (stopRequested && ship.IsStopping && ship.Speed > 0f && ship.Speed < speedBeforeStop)
            {
                movementValidated = true;
                Debug.Log("Sea runtime observed progressive sailing.", this);
            }
        }

        private void ObserveCombat(Ship player)
        {
            var target = combatTargetId == 0
                ? connection.Connection.Db.Ship.Iter().FirstOrDefault(
                    ship => ship.Faction == "npc" && ship.IsActive && ship.IsAlive)
                : connection.Connection.Db.Ship.EntityId.Find(combatTargetId);
            if (target == null)
            {
                if (!combatApproachRequested)
                {
                    connection.Connection.Reducers.SetCourse(20f, -35f);
                    combatApproachRequested = true;
                }

                return;
            }

            combatTargetId = target.EntityId;
            var playerPosition = new Vector2(player.PositionX, player.PositionY);
            var targetPosition = new Vector2(target.PositionX, target.PositionY);
            var distance = Vector2.Distance(playerPosition, targetPosition);
            if (distance > SeaRuntimeValidationRules.CombatObservationRange)
            {
                if (Time.unscaledTime >= nextCombatCourseTime)
                {
                    var outward = (playerPosition - targetPosition).normalized;
                    if (outward.sqrMagnitude < 0.5f)
                    {
                        outward = new Vector2(-1f, -1f).normalized;
                    }

                    var approach = targetPosition + outward * 10f;
                    connection.Connection.Reducers.SetCourse(
                        Mathf.Clamp(approach.x, -95f, 95f),
                        Mathf.Clamp(approach.y, -95f, 95f));
                    nextCombatCourseTime = Time.unscaledTime + 1f;
                }

                return;
            }

            if (!combatTargetRequested)
            {
                connection.Connection.Reducers.SelectTarget(target.EntityId);
                connection.Connection.Reducers.SetAmmo("round");
                combatTargetRequested = true;
                return;
            }

            if (player.TargetEntityId != target.EntityId)
            {
                return;
            }

            var inventory = connection.Connection.Db.Inventory.ByShip
                .Filter(player.EntityId)
                .FirstOrDefault(item => item.ItemId == "round");
            if (combatFireRequested)
            {
                if (inventory != null && inventory.Quantity < combatInitialAmmo)
                {
                    combatLaunchObserved = true;
                }

                if (combatLaunchObserved && target.Hull < combatInitialHull)
                {
                    combatEnabledForThisRun = false;
                    Debug.Log("Sea runtime observed authoritative manual broadside combat.", this);
                    return;
                }

                if (!combatLaunchObserved && Time.unscaledTime - combatFireRequestedAt > 2f)
                {
                    combatFireRequested = false;
                }

                return;
            }

            if (!SeaVolleyPresentationRules.IsInsideBroadsideArc(
                    playerPosition,
                    player.HeadingDegrees,
                    targetPosition,
                    "port",
                    halfArcDegrees: 44f))
            {
                if (Time.unscaledTime >= nextCombatCourseTime)
                {
                    var bearing = Mathf.Atan2(
                        targetPosition.x - playerPosition.x,
                        targetPosition.y - playerPosition.y) * Mathf.Rad2Deg;
                    var desiredHeading = (bearing + 90f) * Mathf.Deg2Rad;
                    var turnDestination = playerPosition + new Vector2(
                        Mathf.Sin(desiredHeading),
                        Mathf.Cos(desiredHeading)) * 10f;
                    connection.Connection.Reducers.SetCourse(
                        Mathf.Clamp(turnDestination.x, -95f, 95f),
                        Mathf.Clamp(turnDestination.y, -95f, 95f));
                    nextCombatCourseTime = Time.unscaledTime + 0.5f;
                }

                return;
            }

            if (!combatHoldRequested &&
                SeaRuntimeValidationRules.ShouldHoldPositionBeforeFire(
                    distance,
                    combatTargetRequested))
            {
                combatHoldRequested = true;
                connection.Connection.Reducers.StopCourse();
                return;
            }

            if (combatHoldRequested && player.Speed > 0.25f)
            {
                return;
            }

            if (inventory == null || inventory.Quantity == 0)
            {
                return;
            }

            combatInitialAmmo = inventory.Quantity;
            combatInitialHull = target.Hull;
            combatFireRequested = true;
            combatFireRequestedAt = Time.unscaledTime;
            connection.Connection.Reducers.FireBroadside("port", "hull");
        }

        private void ObserveTactical(Ship player)
        {
            var world = connection.Connection.Db.WorldState.Id.Find(1);
            if (world == null)
            {
                return;
            }

            if (!tacticalAbilityRequested)
            {
                tacticalInitialHull = player.Hull;
                tacticalAbilityRequested = true;
                connection.Connection.Reducers.ActivateAbility("full_sail");
                return;
            }

            if (!tacticalAbilityObserved)
            {
                var status = connection.Connection.Db.ShipStatus.ByShip
                    .Filter(player.EntityId)
                    .FirstOrDefault(item => item.StatusType == "full_sail" && item.IsActive);
                var cooldown = connection.Connection.Db.Cooldown.ByShip
                    .Filter(player.EntityId)
                    .FirstOrDefault(item => item.CooldownType == "full_sail");
                if (status == null || cooldown == null || cooldown.ReadyAtTick <= world.Tick)
                {
                    return;
                }

                tacticalAbilityObserved = true;
            }

            var storm = connection.Connection.Db.WorldObject.Iter()
                .FirstOrDefault(item => item.Kind == "storm" && item.IsActive);
            if (storm == null)
            {
                return;
            }

            var playerPosition = new Vector2(player.PositionX, player.PositionY);
            var stormPosition = new Vector2(storm.PositionX, storm.PositionY);
            if (!tacticalDamageObserved)
            {
                if (player.Hull < tacticalInitialHull)
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
                    connection.Connection.Reducers.SetCourse(tacticalRetreat.x, tacticalRetreat.y);
                    tacticalRetreatRequested = true;
                    return;
                }

                if (!tacticalStormCourseRequested || Time.unscaledTime >= nextTacticalCourseTime)
                {
                    connection.Connection.Reducers.SetCourse(storm.PositionX, storm.PositionY);
                    tacticalStormCourseRequested = true;
                    nextTacticalCourseTime = Time.unscaledTime + 1f;
                }

                return;
            }

            if (!tacticalRepairRequested)
            {
                if (!tacticalRetreatRequested ||
                    Vector2.Distance(playerPosition, stormPosition) <= storm.Radius + 5f)
                {
                    if (Time.unscaledTime >= nextTacticalCourseTime)
                    {
                        connection.Connection.Reducers.SetCourse(tacticalRetreat.x, tacticalRetreat.y);
                        nextTacticalCourseTime = Time.unscaledTime + 1f;
                    }

                    return;
                }

                connection.Connection.Reducers.StopCourse();
                connection.Connection.Reducers.StartRepair();
                tacticalRepairRequested = true;
                return;
            }

            var channel = connection.Connection.Db.ShipChannel.ShipEntityId.Find(player.EntityId);
            tacticalRepairObserved |= channel != null && channel.IsActive && channel.ChannelType == "repair";
            if (tacticalRepairObserved && player.Hull > tacticalDamagedHull)
            {
                tacticalEnabledForThisRun = false;
                Debug.Log(
                    "Sea runtime observed tactical ability, storm damage, and progressive repair.",
                    this);
            }
        }
    }
}
