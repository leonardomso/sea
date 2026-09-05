using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaGameController : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SeaWorldView worldView;
        [SerializeField] private float movePlaneHeight;

        /// <summary>Mirrors <c>RespawnOptionCode.HomePort</c>; the port is the only berth.</summary>
        private const byte HomePortRespawn = 1;

        public ulong SelectedTargetId => TryGetLocalShip(out var ship) ? ship.TargetEntityId : 0;
        public string SelectedAmmoId => TryGetLocalShip(out var ship)
            ? AmmoId(ship.SelectedAmmoCode)
            : "round";
        private string localAction = "Click water to set course.";
        public string LastAction => string.IsNullOrEmpty(connection?.CommandStatus)
            ? localAction
            : connection.CommandStatus;
        public bool IsReady => connection?.Connection != null && connection.IsSubscribed;
        public bool IsSunk => TryGetLocalShip(out var ship) && !ship.IsAlive;

        private static string AmmoId(byte code) => code switch
        {
            2 => "chain",
            3 => "grapeshot",
            4 => "incendiary",
            _ => "round",
        };

        public void ConfigureDependencies(
            SeaConnectionController connectionController,
            Camera camera,
            SeaWorldView view = null)
        {
            connection = connectionController;
            worldCamera = camera;
            worldView = view;
        }

        private void Awake() => worldCamera ??= Camera.main;

        public void HandlePrimaryClick(Vector2 screenPosition)
        {
            if (!IsReady || worldCamera == null)
            {
                return;
            }

            var ray = worldCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, movePlaneHeight, 0f));
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var point = ray.GetPoint(distance);
            var enemy = FindEnemyAt(point);
            if (enemy.HasValue)
            {
                SelectTarget(enemy.Value);
                return;
            }

            var destination = SeaChartCoordinates.ClampToMap(SeaChartCoordinates.ToChart(point));
            foreach (var worldObject in connection.Connection.Db.WorldObject.Iter())
            {
                if (worldObject.IsActive && worldObject.BlocksMovement &&
                    SeaChartCoordinates.IsBlockedDestination(
                        destination,
                        new Vector2(worldObject.PositionX, worldObject.PositionY),
                        worldObject.Radius))
                {
                    localAction = "Land cannot be selected as a sailing destination.";
                    return;
                }
            }

            // Ping before issuing, so the click is answered on the frame it was made rather
            // than a round trip later.
            worldView?.PingChartPosition(destination);
            Issue(
                new ShipCommand.SetCourse(new SetCourseCommand(destination.x, destination.y)),
                $"Set course to {SeaChartCoordinates.LabelAt(destination.x, destination.y)}");
        }

        public void StopCourse()
        {
            if (!IsReady)
            {
                return;
            }

            Issue(new ShipCommand.StopCourse(new StopCourseCommand()), "All stop");
        }

        public void SelectNextEnemy(int direction = 1)
        {
            if (!IsReady)
            {
                return;
            }

            var enemies = new List<Ship>();
            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (enemy.IsActive && enemy.IsAlive && enemy.FactionCode == 2)
                {
                    enemies.Add(enemy);
                }
            }

            if (enemies.Count == 0)
            {
                localAction = "No targets within chart range.";
                return;
            }

            // Tab takes the nearest hostile first and works outwards, which is the order a
            // captain would pick them in; ties fall back to the entity id so the ring is stable.
            var origin = TryGetLocalShip(out var self)
                ? new Vector2(self.PositionX, self.PositionY)
                : Vector2.zero;
            enemies.Sort((left, right) =>
            {
                var comparison = SquaredRange(left, origin).CompareTo(SquaredRange(right, origin));
                return comparison != 0 ? comparison : left.EntityId.CompareTo(right.EntityId);
            });
            var current = enemies.FindIndex(enemy => enemy.EntityId == SelectedTargetId);
            var next = current < 0
                ? direction < 0 ? enemies.Count - 1 : 0
                : (current + (direction < 0 ? -1 : 1) + enemies.Count) % enemies.Count;
            SelectTarget(enemies[next].EntityId);
        }

        private static float SquaredRange(Ship ship, Vector2 origin)
        {
            var dx = ship.PositionX - origin.x;
            var dy = ship.PositionY - origin.y;
            return (dx * dx) + (dy * dy);
        }

        public void SelectTarget(ulong entityId)
        {
            if (!IsReady)
            {
                return;
            }

            Issue(
                new ShipCommand.SelectTarget(new SelectTargetCommand(entityId)),
                $"Select vessel {entityId}");
        }

        public void ClearTarget()
        {
            if (!IsReady)
            {
                return;
            }

            Issue(new ShipCommand.ClearTarget(new ClearTargetCommand()), "Clear target");
        }

        public bool TryNavigateToCoordinate(string coordinate, out string error)
        {
            error = string.Empty;
            if (!SeaChartCoordinates.TryCellCenter(coordinate, out var cell))
            {
                error = "Enter A1 through AN40.";
                return false;
            }

            if (!IsReady)
            {
                error = "Chart link is not ready.";
                return false;
            }

            worldView?.PingChartPosition(new Vector2(cell.X, cell.Y));
            Issue(
                new ShipCommand.SetCourse(new SetCourseCommand(cell.X, cell.Y)),
                $"Set course to {SeaChartCoordinates.LabelAt(cell.X, cell.Y)}");
            return true;
        }

        public void SetSelectedAmmo(string ammoId)
        {
            if (!IsReady)
            {
                return;
            }

            Issue(
                new ShipCommand.SetAmmo(new SetAmmoCommand(ammoId)),
                $"Select {ammoId} shot");
        }

        /// <summary>
        /// Guns bear in every direction now, so firing is one command with no side and no aim
        /// point: the server picks the armour face from where this ship sits relative to its
        /// target.
        /// </summary>
        public void Fire()
        {
            if (!IsReady)
            {
                return;
            }

            if (SelectedTargetId == 0 &&
                (!TryGetLocalShip(out var ship) || ship.TargetEntityId == 0))
            {
                localAction = "Select a target before firing.";
                return;
            }

            Issue(new ShipCommand.Fire(new FireCommand()), "Fire");
        }

        /// <summary>
        /// Whether a held fire key has anything to send: a volley in the racks and something to
        /// send it at. Asking here keeps the repeat off the wire when the server would refuse it.
        /// </summary>
        public bool CanFireNow =>
            IsReady &&
            TryGetLocalShip(out var ship) &&
            ship.IsAlive &&
            ship.ReadyVolleys > 0 &&
            ship.TargetEntityId != 0;

        /// <summary>A key the mechanics sheet reserves but Milestone 1 does not answer yet.</summary>
        public void ReportUnavailable(string feature) =>
            localAction = SeaCommandResultText.NotAvailableYet(feature);

        public void ToggleRepair()
        {
            if (!TryGetLocalShip(out var ship))
            {
                return;
            }

            var channel = connection.Connection.Db.ShipChannel.ShipEntityId.Find(ship.EntityId);
            if (channel != null && channel.IsActive && channel.ChannelType == "repair")
            {
                Issue(new ShipCommand.CancelChannel(new CancelChannelCommand()), "Cancel repair");
                return;
            }

            Issue(new ShipCommand.StartRepair(new StartRepairCommand()), "Start repair");
        }

        /// <summary>
        /// The kit is a crate opened on deck rather than a manoeuvre, so it is the one repair a
        /// ship already channelling -- or already under fire -- can still reach for. It keeps a
        /// slot of its own because it answers a different question than the channel does.
        /// </summary>
        public void UseRepairKit()
        {
            if (!IsReady)
            {
                return;
            }

            Issue(new ShipCommand.UseRepairKit(new UseRepairKitCommand()), "Use repair kit");
        }

        /// <summary>
        /// Port Lowell is the only berth on the chart, so the choice is really a confirmation --
        /// but the wreck stays on the seabed until it is given, which is what keeps a player who
        /// closed the tab from being sailed back out on their behalf.
        /// </summary>
        public void ChooseHomePortRespawn()
        {
            if (!IsReady)
            {
                return;
            }

            Issue(
                new ShipCommand.ChooseRespawn(new ChooseRespawnCommand(HomePortRespawn)),
                "Return to Port Lowell");
        }

        public bool TryGetLocalShip(out Ship ship)
        {
            ship = null;
            if (!IsReady)
            {
                return false;
            }

            var ownership = connection.Connection.Db.PlayerOwnership.Owner.Find(connection.LocalIdentity);
            if (ownership == null)
            {
                return false;
            }

            ship = connection.Connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
            return ship != null;
        }

        private ulong? FindEnemyAt(Vector3 worldPoint)
        {
            const float selectionRadius = 7f;
            var closestDistance = selectionRadius * selectionRadius;
            ulong? closestId = null;
            // The ships come off the wire in chart space; the click arrives in world space.
            var point = SeaChartCoordinates.ToChart(worldPoint);

            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (!enemy.IsActive || !enemy.IsAlive || enemy.FactionCode != 2)
                {
                    continue;
                }

                var dx = point.x - enemy.PositionX;
                var dy = point.y - enemy.PositionY;
                var squaredDistance = dx * dx + dy * dy;
                if (squaredDistance <= closestDistance)
                {
                    closestDistance = squaredDistance;
                    closestId = enemy.EntityId;
                }
            }

            return closestId;
        }

        private void Issue(ShipCommand command, string description)
        {
            connection.IssueCommand(command, description);
        }
    }
}
