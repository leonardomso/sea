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

        private bool coordinateNavigatorOpen;
        private string coordinateInput = "AX 59";
        private string coordinateError = string.Empty;

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

            if (coordinateNavigatorOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    NavigateToCoordinate();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    coordinateNavigatorOpen = false;
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                coordinateNavigatorOpen = true;
                coordinateError = string.Empty;
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

            if (Input.GetMouseButtonDown(1))
            {
                connection.Connection.Reducers.StopCourse();
                LastAction = "Stopping ship.";
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
            GUI.Box(new Rect(margin, Screen.height - 168, width, 144), "SEA // STARTER COVE");
            GUI.Label(new Rect(margin + 16, Screen.height - 134, width - 32, 22), LastAction);
            GUI.Label(new Rect(margin + 16, Screen.height - 108, width - 32, 22), "Click: sail    Right-click: stop    WASD: chart    Space: recenter    N: coordinate");

            if (TryGetLocalShip(out var ship))
            {
                var gold = GetLocalGold();
                var state = ship.IsStopping ? "STOPPING" : ship.IsMoving ? "SAILING" : ship.IsEngaged ? "ENGAGED" : "READY";
                GUI.Label(new Rect(margin + 16, Screen.height - 82, width - 32, 22),
                    $"Hull {ship.Hull}    Cannon {ship.CannonDamage}    Gold {gold}    {state}");
                GUI.Label(new Rect(margin + 16, Screen.height - 56, width - 32, 22),
                    $"Chart {SeaChartCoordinates.LabelAt(ship.PositionX, ship.PositionY)}    Speed {ship.Speed:0.0}    Heading {ship.HeadingDegrees:000}°");
            }

            if (coordinateNavigatorOpen)
            {
                DrawCoordinateNavigator();
            }
        }

        private void DrawCoordinateNavigator()
        {
            var width = 320f;
            var height = 150f;
            var left = (Screen.width - width) * 0.5f;
            var top = (Screen.height - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height), "SAIL TO COORDINATE");
            GUI.Label(new Rect(left + 18f, top + 34f, width - 36f, 22f), "A–BZ / 0–60, for example AX 59");
            GUI.SetNextControlName("CoordinateInput");
            coordinateInput = GUI.TextField(
                new Rect(left + 18f, top + 62f, width - 36f, 28f),
                coordinateInput,
                8).ToUpperInvariant();
            GUI.FocusControl("CoordinateInput");
            if (GUI.Button(new Rect(left + 18f, top + 100f, 130f, 30f), "Set course"))
            {
                NavigateToCoordinate();
            }
            if (GUI.Button(new Rect(left + 172f, top + 100f, 130f, 30f), "Cancel"))
            {
                coordinateNavigatorOpen = false;
            }

            if (!string.IsNullOrEmpty(coordinateError))
            {
                GUI.Label(new Rect(left + 18f, top + 130f, width - 36f, 20f), coordinateError);
            }
        }

        private void NavigateToCoordinate()
        {
            if (!SeaChartCoordinates.TryCellCenter(coordinateInput, out var cell))
            {
                coordinateError = "Enter a coordinate from A 0 through BZ 60.";
                return;
            }

            connection.Connection.Reducers.SetCourse(cell.X, cell.Y);
            LastAction = $"Sailing to {SeaChartCoordinates.LabelAt(cell.X, cell.Y)}.";
            coordinateNavigatorOpen = false;
            coordinateError = string.Empty;
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
