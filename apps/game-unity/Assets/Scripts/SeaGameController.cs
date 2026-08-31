using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaGameController : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float movePlaneHeight;

        public ulong SelectedTargetId { get; private set; }
        public string SelectedAmmoId { get; private set; } = "round";
        public string SelectedWeakPoint { get; private set; } = "hull";
        public string LastAction { get; private set; } = "Click water to set course.";
        public bool IsReady => connection?.Connection != null && connection.IsSubscribed;

        private void Awake()
        {
            connection ??= FindFirstObjectByType<SeaConnectionController>();
            worldCamera ??= Camera.main;
        }

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

            connection.Connection.Reducers.SetCourse(point.x, point.z);
            LastAction = $"Course set • {SeaChartCoordinates.LabelAt(point.x, point.z)}";
        }

        public void StopCourse()
        {
            if (!IsReady)
            {
                return;
            }

            connection.Connection.Reducers.StopCourse();
            LastAction = "All stop ordered.";
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
                if (enemy.IsActive && enemy.IsAlive && enemy.Faction == "npc")
                {
                    enemies.Add(enemy);
                }
            }

            if (enemies.Count == 0)
            {
                LastAction = "No targets within chart range.";
                return;
            }

            enemies.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            var current = enemies.FindIndex(enemy => enemy.EntityId == SelectedTargetId);
            var next = current < 0
                ? direction < 0 ? enemies.Count - 1 : 0
                : (current + (direction < 0 ? -1 : 1) + enemies.Count) % enemies.Count;
            SelectTarget(enemies[next].EntityId);
        }

        public void SelectTarget(ulong entityId)
        {
            if (!IsReady)
            {
                return;
            }

            connection.Connection.Reducers.SelectTarget(entityId);
            SelectedTargetId = entityId;
            LastAction = $"Target marked • vessel {entityId}";
        }

        public void ClearTarget()
        {
            if (!IsReady)
            {
                return;
            }

            connection.Connection.Reducers.ClearTarget();
            SelectedTargetId = 0;
            LastAction = "Target cleared.";
        }

        public bool TryNavigateToCoordinate(string coordinate, out string error)
        {
            error = string.Empty;
            if (!SeaChartCoordinates.TryCellCenter(coordinate, out var cell))
            {
                error = "Enter A 0 through BZ 60.";
                return false;
            }

            if (!IsReady)
            {
                error = "Chart link is not ready.";
                return false;
            }

            connection.Connection.Reducers.SetCourse(cell.X, cell.Y);
            LastAction = $"Course set • {SeaChartCoordinates.LabelAt(cell.X, cell.Y)}";
            return true;
        }

        public void SetSelectedAmmo(string ammoId)
        {
            SelectedAmmoId = ammoId;
            LastAction = $"{ammoId.ToUpperInvariant()} shot selected.";
        }

        public void SetSelectedWeakPoint(string weakPoint)
        {
            SelectedWeakPoint = weakPoint;
            LastAction = $"Gunners aim for {weakPoint.ToUpperInvariant()}.";
        }

        public void RequestCombatIntent(string order)
        {
            LastAction = order;
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

        private ulong? FindEnemyAt(Vector3 point)
        {
            const float selectionRadius = 7f;
            var closestDistance = selectionRadius * selectionRadius;
            ulong? closestId = null;

            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (!enemy.IsActive || !enemy.IsAlive || enemy.Faction != "npc")
                {
                    continue;
                }

                var dx = point.x - enemy.PositionX;
                var dz = point.z - enemy.PositionY;
                var squaredDistance = dx * dx + dz * dz;
                if (squaredDistance <= closestDistance)
                {
                    closestDistance = squaredDistance;
                    closestId = enemy.EntityId;
                }
            }

            return closestId;
        }
    }
}
