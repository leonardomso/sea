using System;
using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe : MonoBehaviour
    {
        private SeaConnectionController connection;
        private SeaWorldView worldView;
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
        private SubscriptionHandle runtimeCombatTargetsSubscription;
        private ulong combatTargetId;
        private uint combatInitialHull;
        private uint combatInitialAmmo;
        private float nextCombatCourseTime;
        private float combatFireRequestedAt;

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
            progressionEnabledForThisRun = Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => argument == "-seaRuntimeProgressionTest");
            ConfigureValidationEvidence();
        }

        public void ConfigureDependencies(
            SeaConnectionController connectionController,
            SeaWorldView configuredWorldView)
        {
            connection = connectionController;
            worldView = configuredWorldView;
        }

        private void Update()
        {
            if (presentationPerformanceEnabledForThisRun)
            {
                ObservePresentationPerformance();
                return;
            }

            if ((!enabledForThisRun && !combatEnabledForThisRun &&
                    !tacticalEnabledForThisRun && !progressionEnabledForThisRun) ||
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
                    var movement = connection.Connection.Db.ShipMovement.EntityId.Find(
                        ownership.ShipEntityId);
                    if (movement != null)
                    {
                        ObserveShip(ship, movement);
                    }
                }
                else if (combatEnabledForThisRun || progressionEnabledForThisRun)
                {
                    ObserveCombat(ship);
                }
                else if (tacticalEnabledForThisRun)
                {
                    ObserveTactical(ship);
                }
            }
        }

        private void ObserveShip(Ship ship, ShipMovement movement)
        {
            var position = new Vector2(movement.PositionX, movement.PositionY);
            if (!moveRequested)
            {
                start = position;
                destination = new Vector2(Mathf.Min(position.x + 12f, 95f), position.y);
                if (Mathf.Approximately(destination.x, position.x))
                {
                    destination.x = Mathf.Max(position.x - 12f, -95f);
                }
                SetCourse(destination.x, destination.y);
                moveRequested = true;
                return;
            }

            var travelled = Vector2.Distance(start, position);
            var remaining = Vector2.Distance(position, destination);
            if (!stopRequested && movement.IsMoving && movement.Speed > 0.5f &&
                travelled > 0.1f && remaining > 0.1f)
            {
                speedBeforeStop = movement.Speed;
                stopRequested = true;
                StopCourse();
                return;
            }

            if (SeaRuntimeValidationRules.HasObservedStop(
                    stopRequested,
                    travelled,
                    speedBeforeStop,
                    movement.Speed,
                    movement.IsMoving,
                    ship.IsStopping))
            {
                movementValidated = true;
                MarkRuntimeMilestone(SeaRuntimeMilestone.Movement);
                Debug.Log("Sea runtime observed progressive sailing.", this);
            }
        }

        private void ObserveCombat(Ship player)
        {
            runtimeCombatTargetsSubscription ??= connection.Connection.SubscriptionBuilder()
                .Subscribe(new[]
                {
                    SeaRuntimeValidationRules.RuntimeNpcSubscriptionQuery,
                    SeaRuntimeValidationRules.RuntimeMovementSubscriptionQuery,
                });
            var target = combatTargetId == 0
                ? connection.Connection.Db.Ship.Iter()
                    .Where(ship =>
                        ship.FactionCode == 2 &&
                        ship.ArchetypeCode == 1 &&
                        ship.IsActive &&
                        ship.IsAlive)
                    .OrderBy(ship => Vector2.SqrMagnitude(
                        LivePosition(ship) - LivePosition(player)))
                    .FirstOrDefault()
                : connection.Connection.Db.Ship.EntityId.Find(combatTargetId);
            if (target == null)
            {
                if (!combatApproachRequested)
                {
                    SetCombatCourse(20f, -35f);
                    combatApproachRequested = true;
                }

                return;
            }

            combatTargetId = target.EntityId;
            if (ObserveProgressionTarget(player, target))
            {
                return;
            }

            var playerPosition = LivePosition(player);
            var targetPosition = LivePosition(target);
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

                    var approach = targetPosition +
                        outward * SeaRuntimeValidationRules.CombatApproachRange;
                    SetCombatCourse(
                        Mathf.Clamp(approach.x, -95f, 95f),
                        Mathf.Clamp(approach.y, -95f, 95f));
                    nextCombatCourseTime = Time.unscaledTime + 1f;
                }

                return;
            }

            if (!combatTargetRequested)
            {
                Issue(
                    new ShipCommand.SelectTarget(new SelectTargetCommand(target.EntityId)),
                    "runtime select target");
                Issue(
                    new ShipCommand.SetAmmo(new SetAmmoCommand("round")),
                    "runtime select ammunition");
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
                    if (!combatValidated)
                    {
                        combatValidated = true;
                        MarkRuntimeMilestone(SeaRuntimeMilestone.Combat);
                        Debug.Log("Sea runtime observed authoritative manual broadside combat.", this);
                    }

                    if (progressionEnabledForThisRun)
                    {
                        combatFireRequested = false;
                        combatLaunchObserved = false;
                    }
                    else
                    {
                        combatEnabledForThisRun = false;
                    }

                    return;
                }

                if (!combatLaunchObserved && Time.unscaledTime - combatFireRequestedAt > 2f)
                {
                    combatFireRequested = false;
                }

                return;
            }

            var broadside = SeaRuntimeValidationRules.PlanBroadside(
                playerPosition,
                LiveHeading(player),
                targetPosition);
            if (!broadside.CanFire)
            {
                if (Time.unscaledTime >= nextCombatCourseTime)
                {
                    var desiredHeading = broadside.DesiredHeadingDegrees * Mathf.Deg2Rad;
                    var turnDestination = playerPosition + new Vector2(
                        Mathf.Sin(desiredHeading),
                        Mathf.Cos(desiredHeading)) * 10f;
                    SetCombatCourse(
                        Mathf.Clamp(turnDestination.x, -95f, 95f),
                        Mathf.Clamp(turnDestination.y, -95f, 95f));
                    nextCombatCourseTime = Time.unscaledTime + 0.5f;
                }

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
            Issue(
                new ShipCommand.FireBroadside(
                    new FireBroadsideCommand(broadside.Side, "hull")),
                "runtime fire broadside");
        }

        private void SetCombatCourse(float x, float y)
        {
            SetCourse(x, y);
        }

        private void SetCourse(float x, float y) => Issue(
            new ShipCommand.SetCourse(new SetCourseCommand(x, y)),
            "runtime set course");

        private void StopCourse() => Issue(
            new ShipCommand.StopCourse(new StopCourseCommand()),
            "runtime stop course");

        private void Issue(ShipCommand command, string description) =>
            connection.IssueCommand(command, description);

        private Vector2 LivePosition(Ship ship)
        {
            var movement = connection.Connection.Db.ShipMovement.EntityId.Find(ship.EntityId);
            return movement == null
                ? new Vector2(ship.PositionX, ship.PositionY)
                : new Vector2(movement.PositionX, movement.PositionY);
        }

        private float LiveHeading(Ship ship)
        {
            var movement = connection.Connection.Db.ShipMovement.EntityId.Find(ship.EntityId);
            return movement?.HeadingDegrees ?? ship.HeadingDegrees;
        }

    }
}
