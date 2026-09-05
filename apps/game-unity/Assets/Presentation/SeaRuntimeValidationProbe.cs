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
        private uint combatInitialVolleys;
        private ulong combatInitialShotTick;
        private float nextCombatCourseTime;
        private float combatFireRequestedAt;
        private float nextProgressReportTime;
        private float nextRespawnRequestTime;

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
                ReportRuntimeProgress(ship);
                if (RaiseTheWreck(ship))
                {
                    return;
                }

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

        /// <summary>ShipMode.Sunk.</summary>
        private const byte SunkModeCode = 2;

        /// <summary>SeaGameController.HomePortRespawn.</summary>
        private const byte HomePortRespawn = 1;

        /// <summary>
        /// A hostile shoots back, and a probe that loiters inside its reach for a minute is
        /// going to be sunk at least once. The wreck stays on the seabed until the captain
        /// asks for Port Lowell, so the run asks, and takes the fight up again from the
        /// harbour with nothing carried over from the engagement that killed her.
        /// </summary>
        private bool RaiseTheWreck(Ship player)
        {
            if (player.ModeCode != SunkModeCode)
            {
                return false;
            }

            if (Time.unscaledTime >= nextRespawnRequestTime)
            {
                nextRespawnRequestTime = Time.unscaledTime + 2f;
                Issue(
                    new ShipCommand.ChooseRespawn(new ChooseRespawnCommand(HomePortRespawn)),
                    "runtime choose respawn");
            }

            combatApproachRequested = false;
            combatTargetRequested = false;
            combatFireRequested = false;
            combatLaunchObserved = false;

            // A raised wreck is a whole hull, so the storm leg has to be sailed again from
            // its own baseline: counting the new hull as a repair would pass the scenario on
            // the sinking rather than on the pumps.
            tacticalHullSampled = false;
            tacticalStormCourseRequested = false;
            tacticalDamageObserved = false;
            tacticalRetreatRequested = false;
            tacticalRepairRequested = false;
            tacticalRepairObserved = false;
            nextCombatCourseTime = 0f;
            nextTacticalCourseTime = 0f;
            return true;
        }

        // A stalled scenario is silent otherwise: the run just ends on its timeout with no
        // hint of which leg it was still sailing. This says where it had got to, often
        // enough to read the story off the log and rarely enough to be free.
        private void ReportRuntimeProgress(Ship player)
        {
            if (Time.unscaledTime < nextProgressReportTime)
            {
                return;
            }

            nextProgressReportTime = Time.unscaledTime + 10f;
            var target = combatTargetId == 0
                ? null
                : connection.Connection.Db.Ship.EntityId.Find(combatTargetId);
            var range = target == null
                ? -1f
                : Vector2.Distance(LivePosition(player), LivePosition(target));
            Debug.Log(
                $"Sea runtime progress: move={movementValidated} combat={combatValidated} " +
                $"sunk={progressionSunkObserved} loot={progressionLootObserved} " +
                $"tacticalDamage={tacticalDamageObserved} repair={tacticalRepairObserved} " +
                $"target={combatTargetId} range={range:F1} " +
                $"locked={player.TargetEntityId} volleys={player.ReadyVolleys} " +
                $"hull={player.Hull} mode={player.ModeCode}",
                this);
        }

        private void ObserveShip(Ship ship, ShipMovement movement)
        {
            var position = new Vector2(movement.PositionX, movement.PositionY);
            if (!moveRequested)
            {
                start = position;
                // Twelve squares east, or twelve west if east is off the chart. The edges read
                // 95 and -95, which were the edges of a map centred on zero.
                destination = new Vector2(
                    Mathf.Min(position.x + 12f, SeaChartCoordinates.MapMaximum),
                    position.y);
                if (Mathf.Approximately(destination.x, position.x))
                {
                    destination.x = Mathf.Max(position.x - 12f, SeaChartCoordinates.MapMinimum);
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
                    // A stop takes her way off on the tick it lands (SEA_5 4.2), so the
                    // only stop there is to observe is the one that has already finished.
                    isStopping: false))
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
                    // On the chart, not on the vanished centre-origin map this read (20, -35)
                    // for -- a course to a square north of the northern edge, which is no course
                    // at all, so the sweep for a target never started.
                    SetCombatCourse(240f, 130f);
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

                    var approach = SeaChartCoordinates.ClampToMap(targetPosition +
                        outward * SeaRuntimeValidationRules.CombatApproachRange);
                    SetCombatCourse(approach.x, approach.y);
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

            if (combatFireRequested)
            {
                if (SeaRuntimeValidationRules.HasLaunchedVolley(
                        combatInitialVolleys,
                        player.ReadyVolleys,
                        combatInitialShotTick,
                        player.LastShotTick))
                {
                    combatLaunchObserved = true;
                }

                if (combatLaunchObserved && target.Hull < combatInitialHull)
                {
                    if (!combatValidated)
                    {
                        combatValidated = true;
                        MarkRuntimeMilestone(SeaRuntimeMilestone.Combat);
                        Debug.Log("Sea runtime observed authoritative manual magazine combat.", this);
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

            var firing = SeaRuntimeValidationRules.PlanFire(
                playerPosition,
                LiveHeading(player),
                targetPosition);
            if (!firing.CanFire)
            {
                if (Time.unscaledTime >= nextCombatCourseTime)
                {
                    // A chart bearing turned back into a chart step: north is the smaller y.
                    var desiredHeading = firing.DesiredHeadingDegrees * Mathf.Deg2Rad;
                    var turnDestination = SeaChartCoordinates.ClampToMap(playerPosition + new Vector2(
                        Mathf.Sin(desiredHeading),
                        0f - Mathf.Cos(desiredHeading)) * 10f);
                    SetCombatCourse(turnDestination.x, turnDestination.y);
                    nextCombatCourseTime = Time.unscaledTime + 0.5f;
                }

                return;
            }

            // The racks, not the hold: ammunition is unlimited, so a reload is the only thing
            // that keeps a shot from leaving.
            if (player.ReadyVolleys == 0)
            {
                return;
            }

            combatInitialVolleys = player.ReadyVolleys;
            combatInitialShotTick = player.LastShotTick;
            combatInitialHull = target.Hull;
            combatFireRequested = true;
            combatFireRequestedAt = Time.unscaledTime;
            Issue(new ShipCommand.Fire(new FireCommand()), "runtime fire");
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
