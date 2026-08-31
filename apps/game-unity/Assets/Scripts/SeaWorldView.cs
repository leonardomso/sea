using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaWorldView : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private float interpolationSpeed = 10f;

        private readonly Dictionary<ulong, GameObject> entities = new();
        private readonly Dictionary<ulong, GameObject> mapGeometry = new();
        private readonly Dictionary<ulong, Vector3> targets = new();
        private GameObject playerObject;
        private GameObject targetRing;
        private Material waterMaterial;
        private Material islandMaterial;
        private Material reefMaterial;
        private Material playerMaterial;
        private Material enemyMaterial;
        private bool sceneCreated;

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
            InterpolateObjects();
        }

        private void CreateMaterials()
        {
            waterMaterial = CreateMaterial(new Color(0.035f, 0.22f, 0.30f, 1f));
            islandMaterial = CreateMaterial(new Color(0.48f, 0.34f, 0.18f, 1f));
            reefMaterial = CreateMaterial(new Color(0.82f, 0.39f, 0.20f, 1f));
            playerMaterial = CreateMaterial(new Color(0.24f, 0.87f, 0.78f, 1f));
            enemyMaterial = CreateMaterial(new Color(0.94f, 0.31f, 0.28f, 1f));
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            return material;
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
            foreach (var entity in connection.Connection.Db.MapEntity.Iter())
            {
                if (entity.Kind == "harbor")
                {
                    continue;
                }

                var geometry = GameObject.CreatePrimitive(entity.Kind == "island" ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                geometry.name = $"Map {entity.Kind} {entity.EntityId}";
                geometry.transform.position = ToWorld(entity.PositionX, entity.PositionY, 0f);
                var radius = entity.InteractionRadius;
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
            foreach (var entity in connection.Connection.Db.MapEntity.Iter())
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
            foreach (var enemy in connection.Connection.Db.NpcShip.Iter())
            {
                if (!entities.TryGetValue(enemy.EntityId, out var enemyObject))
                {
                    enemyObject = CreateShip($"Enemy Ship {enemy.EntityId}", enemyMaterial);
                    entities.Add(enemy.EntityId, enemyObject);
                }

                enemyObject.SetActive(enemy.IsActive);
                targets[enemy.EntityId] = ToWorld(enemy.PositionX, enemy.PositionY, 1.2f);
                UpdateHealthBar(enemyObject, enemy.Health, enemy.MaxHealth);
            }
        }

        private void SyncPlayerShip()
        {
            foreach (var ship in connection.Connection.Db.PlayerShip.Iter())
            {
                if (ship.Owner != connection.LocalIdentity)
                {
                    continue;
                }

                if (playerObject == null)
                {
                    playerObject = CreateShip("Player Ship", playerMaterial);
                }

                targets[0] = ToWorld(ship.PositionX, ship.PositionY, 1.2f);
                UpdateHealthBar(playerObject, ship.Health, 100u);
                UpdateTargetRing(ship);
                break;
            }
        }

        private void InterpolateObjects()
        {
            foreach (var target in targets)
            {
                if (target.Key == 0)
                {
                    if (playerObject != null)
                    {
                        playerObject.transform.position = Vector3.Lerp(playerObject.transform.position, target.Value, Time.deltaTime * interpolationSpeed);
                    }

                    continue;
                }

                if (entities.TryGetValue(target.Key, out var entityObject))
                {
                    entityObject.transform.position = Vector3.Lerp(entityObject.transform.position, target.Value, Time.deltaTime * interpolationSpeed);
                }
            }
        }

        private GameObject CreateShip(string name, Material material)
        {
            var ship = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ship.name = name;
            ship.transform.localScale = new Vector3(2.2f, 0.7f, 4.2f);
            ship.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            ship.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(ship.GetComponent<Collider>());
            return ship;
        }

        private void UpdateTargetRing(PlayerShip ship)
        {
            if (!ship.HasSelectedTarget || !entities.TryGetValue(ship.SelectedTargetId, out var selectedObject))
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
                targetRing.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(1f, 0.85f, 0.25f, 1f));
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
                bar = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                bar.name = "Health";
                bar.SetParent(ship.transform, false);
                bar.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.28f, 0.95f, 0.45f, 1f));
                Destroy(bar.GetComponent<Collider>());
            }

            bar.localPosition = new Vector3(0f, 1.1f, 0f);
            bar.localScale = new Vector3(4f * Mathf.Clamp01(maxHealth == 0 ? 0f : (float)health / maxHealth), 0.12f, 0.12f);
        }

        private static Vector3 ToWorld(float x, float y, float height) => new(x, height, y);
    }
}
