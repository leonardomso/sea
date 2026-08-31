using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaWorldView : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private GameObject shipModel;
        [SerializeField] private float presentationMovementSpeed = 18f;
        [SerializeField] private float turnSpeedDegrees = 120f;
        [SerializeField] private float modelYawOffset = -90f;

        private const float ShipFootprint = 10f;

        private readonly Dictionary<ulong, GameObject> entities = new();
        private readonly Dictionary<ulong, GameObject> mapGeometry = new();
        private readonly Dictionary<ulong, Vector3> targets = new();
        private GameObject playerObject;
        private GameObject targetRing;
        private Material waterMaterial;
        private Material islandMaterial;
        private Material reefMaterial;
        private bool sceneCreated;

        public GameObject ShipModel => shipModel;

        public void ConfigureShipModel(GameObject model)
        {
            shipModel = model;
        }

        private void Awake()
        {
            connection ??= FindFirstObjectByType<SeaConnectionController>();
            CreateMaterials();
            CreateWater();
        }

        private void Update()
        {
            if (connection?.Connection == null || !connection.IsSubscribed)
            {
                return;
            }

            if (!sceneCreated)
            {
                CreateWorldGeometry();
                sceneCreated = true;
            }

            SyncMapEntities();
            SyncEnemyShips();
            SyncPlayerShip();
            UpdateEntityTransforms();
        }

        private void CreateMaterials()
        {
            waterMaterial = SeaMaterialFactory.Create(new Color(0.035f, 0.22f, 0.30f, 1f));
            islandMaterial = SeaMaterialFactory.Create(new Color(0.48f, 0.34f, 0.18f, 1f));
            reefMaterial = SeaMaterialFactory.Create(new Color(0.82f, 0.39f, 0.20f, 1f));
        }

        private void CreateWater()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water Surface";
            water.transform.position = new Vector3(0f, -0.2f, 0f);
            water.transform.localScale = new Vector3(20f, 1f, 20f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Destroy(water.GetComponent<Collider>());
        }

        private void CreateWorldGeometry()
        {
            foreach (var entity in connection.Connection.Db.WorldObject.Iter())
            {
                if (entity.Kind == "harbor")
                {
                    continue;
                }

                var geometry = GameObject.CreatePrimitive(entity.Kind == "island" ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                geometry.name = $"Map {entity.Kind} {entity.EntityId}";
                geometry.transform.position = ToWorld(entity.PositionX, entity.PositionY, 0f);
                var radius = entity.Radius;
                geometry.transform.localScale = entity.Kind == "island"
                    ? new Vector3(radius * 2f, 1.2f, radius * 2f)
                    : new Vector3(radius * 2f, 0.5f, radius * 2f);
                geometry.GetComponent<Renderer>().sharedMaterial = entity.Kind == "island" ? islandMaterial : reefMaterial;
                Destroy(geometry.GetComponent<Collider>());
                mapGeometry[entity.EntityId] = geometry;
            }
        }

        private void SyncMapEntities()
        {
            foreach (var entity in connection.Connection.Db.WorldObject.Iter())
            {
                if (entity.Kind == "harbor")
                {
                    continue;
                }

                if (!mapGeometry.TryGetValue(entity.EntityId, out var geometry))
                {
                    continue;
                }

                geometry.SetActive(entity.IsActive);
            }
        }

        private void SyncEnemyShips()
        {
            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (enemy.Faction != "npc")
                {
                    continue;
                }

                if (!entities.TryGetValue(enemy.EntityId, out var enemyObject))
                {
                    enemyObject = CreateShip($"Enemy Ship {enemy.EntityId}");
                    entities.Add(enemy.EntityId, enemyObject);
                    enemyObject.transform.position = ToWorld(enemy.PositionX, enemy.PositionY, 1.2f);
                }

                enemyObject.SetActive(enemy.IsActive);
                targets[enemy.EntityId] = ToWorld(enemy.PositionX, enemy.PositionY, 1.2f);
                UpdateHealthBar(enemyObject, enemy.Hull, enemy.MaxHull);
            }
        }

        private void SyncPlayerShip()
        {
            var ownership = connection.Connection.Db.PlayerOwnership.Owner.Find(connection.LocalIdentity);
            if (ownership == null)
            {
                return;
            }

            var ship = connection.Connection.Db.Ship.EntityId.Find(ownership.ShipEntityId);
            if (ship == null)
            {
                return;
            }

            if (playerObject == null)
            {
                playerObject = CreateShip("Player Ship");
                playerObject.transform.position = ToWorld(ship.PositionX, ship.PositionY, 1.2f);
            }

            targets[0] = ToWorld(ship.PositionX, ship.PositionY, 1.2f);
            UpdateHealthBar(playerObject, ship.Hull, ship.MaxHull);
            UpdateTargetRing(ship);
        }

        private void UpdateEntityTransforms()
        {
            foreach (var target in targets)
            {
                if (target.Key == 0)
                {
                    if (playerObject != null)
                    {
                        SeaShipMotion.Step(
                            playerObject.transform,
                            target.Value,
                            Time.deltaTime,
                            presentationMovementSpeed,
                            turnSpeedDegrees,
                            modelYawOffset);
                    }

                    continue;
                }

                if (entities.TryGetValue(target.Key, out var entityObject))
                {
                    SeaShipMotion.Step(
                        entityObject.transform,
                        target.Value,
                        Time.deltaTime,
                        presentationMovementSpeed,
                        turnSpeedDegrees,
                        modelYawOffset);
                }
            }
        }

        private GameObject CreateShip(string name)
        {
            if (shipModel == null)
            {
                throw new System.InvalidOperationException("The starter ship model is not configured.");
            }

            return SeaShipVisualFactory.Create(shipModel, name, ShipFootprint);
        }

        private void UpdateTargetRing(Ship ship)
        {
            if (ship.TargetEntityId == 0 || !entities.TryGetValue(ship.TargetEntityId, out var selectedObject))
            {
                if (targetRing != null)
                {
                    targetRing.SetActive(false);
                }

                return;
            }

            if (targetRing == null)
            {
                targetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                targetRing.name = "Selected Target Ring";
                targetRing.transform.localScale = new Vector3(4.5f, 0.04f, 4.5f);
                targetRing.GetComponent<Renderer>().sharedMaterial = SeaMaterialFactory.Create(new Color(1f, 0.85f, 0.25f, 1f));
                Destroy(targetRing.GetComponent<Collider>());
            }

            targetRing.SetActive(selectedObject.activeSelf);
            targetRing.transform.position = selectedObject.transform.position + Vector3.down * 0.7f;
        }

        private void UpdateHealthBar(GameObject ship, uint health, uint maxHealth)
        {
            var bar = ship.transform.Find("Health");
            if (bar == null)
            {
                var modelBounds = SeaShipVisualFactory.CalculateRendererBounds(ship);
                var modelTop = ship.transform.InverseTransformPoint(
                    new Vector3(modelBounds.center.x, modelBounds.max.y, modelBounds.center.z));
                bar = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                bar.name = "Health";
                bar.SetParent(ship.transform, false);
                bar.GetComponent<Renderer>().sharedMaterial = SeaMaterialFactory.Create(new Color(0.28f, 0.95f, 0.45f, 1f));
                Destroy(bar.GetComponent<Collider>());
                bar.localPosition = new Vector3(0f, modelTop.y + 0.6f, 0f);
            }

            bar.localScale = new Vector3(4f * Mathf.Clamp01(maxHealth == 0 ? 0f : (float)health / maxHealth), 0.12f, 0.12f);
        }

        private static Vector3 ToWorld(float x, float y, float height) => new(x, height, y);
    }
}
