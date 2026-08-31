using System;
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
        public string LastAction { get; private set; } = "Select an enemy or click the water to sail.";

        private void Awake()
        {
            connection ??= FindFirstObjectByType<SeaConnectionController>();
            worldCamera ??= Camera.main;
        }

        private void Update()
        {
            if (connection?.Connection == null || !connection.IsSubscribed)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SelectNextEnemy();
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                EngageSelectedTarget();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                UpgradeCannon();
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandlePrimaryClick(Input.mousePosition);
            }
        }

        public void SelectNextEnemy()
        {
            var enemies = new System.Collections.Generic.List<Ship>();
            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (enemy.IsActive && enemy.IsAlive && enemy.Faction == "npc")
                {
                    enemies.Add(enemy);
                }
            }

            if (enemies.Count == 0)
            {
                LastAction = "No active enemies remain.";
                return;
            }

            enemies.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
            var nextIndex = enemies.FindIndex(enemy => enemy.EntityId == SelectedTargetId) + 1;
            if (nextIndex >= enemies.Count)
            {
                nextIndex = 0;
            }

            SelectTarget(enemies[nextIndex].EntityId);
        }

        public void SelectTarget(ulong entityId)
        {
            connection.Connection.Reducers.SelectTarget(entityId);
            SelectedTargetId = entityId;
            LastAction = $"Target selected: enemy {entityId}. Press E to engage.";
        }

        public void EngageSelectedTarget()
        {
            if (SelectedTargetId == 0)
            {
                LastAction = "Select an enemy before engaging.";
                return;
            }

            connection.Connection.Reducers.Engage();
            LastAction = $"Engaged enemy {SelectedTargetId}. Cannons fire automatically in range.";
        }

        public void UpgradeCannon()
        {
            connection.Connection.Reducers.UpgradeCannon();
            LastAction = "Cannon upgrade requested.";
        }

        private void HandlePrimaryClick(Vector2 screenPosition)
        {
            if (worldCamera == null)
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
            LastAction = $"Sailing to {point.x:0}, {point.z:0}.";
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
                var distance = dx * dx + dz * dz;
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closestId = enemy.EntityId;
                }
            }

            return closestId;
        }

        private void OnGUI()
        {
            if (connection == null || !connection.IsSubscribed)
            {
                return;
            }

            const int margin = 24;
            const int width = 520;
            GUI.Box(new Rect(margin, Screen.height - 142, width, 118), "SEA // STARTER COVE");
            GUI.Label(new Rect(margin + 16, Screen.height - 108, width - 32, 22), LastAction);
            GUI.Label(new Rect(margin + 16, Screen.height - 82, width - 32, 22), "Click water: sail    Click enemy / Tab: select    E: engage    U: upgrade");

            if (TryGetLocalShip(out var ship))
            {
                var gold = GetLocalGold();
                var state = ship.IsMoving ? "SAILING" : ship.IsEngaged ? "ENGAGED" : "READY";
                GUI.Label(new Rect(margin + 16, Screen.height - 56, width - 32, 22),
                    $"Hull {ship.Hull}    Cannon {ship.CannonDamage}    Gold {gold}    {state}");
            }
        }

        private bool TryGetLocalShip(out Ship ship)
        {
            var ownership = connection.Connection.Db.PlayerOwnership.Owner.Find(connection.LocalIdentity);
            if (ownership != null)
            {
                var candidate = connection.Connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
                if (candidate != null)
                {
                    ship = candidate;
                    return true;
                }
            }

            ship = null;
            return false;
        }

        private uint GetLocalGold()
        {
            return connection.Connection.Db.PlayerProgression.Owner
                .Find(connection.LocalIdentity)?.Gold ?? 0;
        }
    }
}
