using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView : MonoBehaviour
    {
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private GameObject shipModel;
        [SerializeField] private Material shipMaterial;
        [SerializeField] private Shader fogShader;
        [SerializeField] private float presentationMovementSpeed = 18f;
        [SerializeField] private float turnSpeedDegrees = 720f;
        [SerializeField] private float modelYawOffset = 270f;

        private const float ShipFootprint = 10f;
        private const int MainChartFogLayer = 8;
        public const float VisionRadius = 44f;
        public const float WaterSurfaceHeight = -0.35f;
        public const float ShipRootHeight = -0.43f;

        private readonly Dictionary<ulong, GameObject> entities = new();
        private readonly Dictionary<ulong, GameObject> mapGeometry = new();
        private readonly Dictionary<ulong, PresentationTarget> targets = new();
        private readonly Dictionary<ulong, SeaShipFeedback> shipFeedback = new();
        private GameObject playerObject;
        private SeaShipFeedback playerFeedback;
        private GameObject targetRing;
        private Material waterMaterial;
        private Material sandMaterial;
        private Material rockMaterial;
        private Material landMaterial;
        private Material shallowsMaterial;
        private Material dockMaterial;
        private Material wakeMaterial;
        private Material waterlineShadowMaterial;
        private Material fogMaterial;
        private Material cannonballMaterial;
        private Material combatEffectMaterial;
        private Material shoalMaterial;
        private Material stormMaterial;
        private SeaCombatPresenter combatPresenter;
        private ulong playerEntityId;
        private LineRenderer courseLine;
        private LineRenderer destinationRing;

        public GameObject ShipModel => shipModel;
        public Material ShipMaterial => shipMaterial;
        public Shader FogShader => fogShader;
        public float ModelYawOffset => modelYawOffset;
        public float PresentationTurnSpeed => turnSpeedDegrees;

        public bool TryGetPlayerPresentationPosition(out Vector3 position)
        {
            if (playerObject == null)
            {
                position = default;
                return false;
            }

            position = playerObject.transform.position;
            return true;
        }

        public void ConfigureShipAssets(GameObject model, Material material)
        {
            shipModel = model;
            shipMaterial = material;
        }

        public void ConfigureFogShader(Shader shader)
        {
            fogShader = shader;
        }

        public void ConfigureDependencies(SeaConnectionController connectionController)
        {
            connection = connectionController;
            BindInterestCallbacks(connectionController);
        }

        private void Awake()
        {
            BindInterestCallbacks(connection);
            CreateMaterials();
            CreateWater();
            CreateFog();
        }

        private void Update()
        {
            if (connection?.Connection == null || !connection.IsSubscribed)
            {
                return;
            }

            EnsureWorldGeometry();
            SyncMapEntities();
            SyncEnemyShips();
            SyncPlayerShip();
            UpdateEntityTransforms();
            SyncCombatPresentation();
        }

        private void CreateMaterials()
        {
            waterMaterial = SeaMaterialFactory.CreateChartWater();
            sandMaterial = SeaMaterialFactory.Create(new Color(0.68f, 0.52f, 0.29f, 1f));
            rockMaterial = SeaMaterialFactory.Create(new Color(0.19f, 0.20f, 0.18f, 1f));
            landMaterial = SeaMaterialFactory.Create(new Color(0.16f, 0.31f, 0.20f, 1f));
            shallowsMaterial = SeaMaterialFactory.Create(new Color(0.15f, 0.49f, 0.47f, 1f));
            dockMaterial = SeaMaterialFactory.Create(new Color(0.30f, 0.17f, 0.08f, 1f));
            wakeMaterial = SeaMaterialFactory.CreateTransparent(new Color(0.76f, 0.92f, 0.88f, 0.48f));
            waterlineShadowMaterial = SeaMaterialFactory.CreateTransparent(new Color(0.01f, 0.06f, 0.07f, 0.32f));
            fogShader ??= Shader.Find("Sea/Chart Fog");
            if (fogShader == null)
            {
                throw new System.InvalidOperationException("The chart fog shader is missing.");
            }

            fogMaterial = new Material(fogShader) { name = "Player Vision Fog" };
            fogMaterial.SetFloat("_VisionRadius", VisionRadius);
            fogMaterial.SetFloat("_FadeWidth", 12f);
            fogMaterial.SetColor("_FogColor", new Color(0.015f, 0.05f, 0.065f, 0.96f));
            cannonballMaterial = SeaMaterialFactory.Create(new Color(0.04f, 0.035f, 0.03f, 1f));
            combatEffectMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.78f, 0.87f, 0.90f, 0.9f));
            shoalMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.18f, 0.78f, 0.68f, 0.34f));
            stormMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.10f, 0.14f, 0.18f, 0.82f));
            combatPresenter = new SeaCombatPresenter(cannonballMaterial, combatEffectMaterial);
        }

        private void CreateWater()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water Surface";
            water.transform.position = new Vector3(0f, WaterSurfaceHeight, 0f);
            water.transform.localScale = new Vector3(26f, 1f, 26f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Destroy(water.GetComponent<Collider>());
            CreateCourseIndicator();
        }

        private void CreateFog()
        {
            var fog = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fog.name = "Player Vision Fog";
            fog.layer = MainChartFogLayer;
            fog.transform.position = new Vector3(0f, 8f, 0f);
            fog.transform.localScale = new Vector3(26f, 1f, 26f);
            var renderer = fog.GetComponent<Renderer>();
            renderer.sharedMaterial = fogMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(fog.GetComponent<Collider>());
        }

        private void EnsureWorldGeometry()
        {
            foreach (var entity in connection.Connection.Db.WorldObject.Iter())
            {
                if (mapGeometry.ContainsKey(entity.EntityId))
                {
                    continue;
                }

                var position = ToWorld(entity.PositionX, entity.PositionY, 0f);
                var geometry = entity.Kind switch
                {
                    "island" => SeaWorldGeometryFactory.CreateIsland(
                        $"Map island {entity.EntityId}",
                        position,
                        entity.Radius,
                        sandMaterial,
                        rockMaterial,
                        landMaterial),
                    "reef" => SeaWorldGeometryFactory.CreateReef(
                        $"Map reef {entity.EntityId}",
                        position,
                        entity.Radius,
                        shallowsMaterial,
                        rockMaterial),
                    "harbor" => SeaWorldGeometryFactory.CreateHarbor(
                        $"Map harbor {entity.EntityId}",
                        position,
                        entity.Radius,
                        shallowsMaterial,
                        dockMaterial),
                    "shoal" => SeaWorldGeometryFactory.CreateShoal(
                        $"Map shoal {entity.EntityId}",
                        position,
                        entity.Radius,
                        shoalMaterial),
                    "storm" => SeaWorldGeometryFactory.CreateStorm(
                        $"Map storm {entity.EntityId}",
                        position,
                        entity.Radius,
                        stormMaterial),
                    _ => null,
                };
                if (geometry == null)
                {
                    continue;
                }

                mapGeometry[entity.EntityId] = geometry;
            }
        }

        private void SyncMapEntities()
        {
            foreach (var entity in connection.Connection.Db.WorldObject.Iter())
            {
                if (!mapGeometry.TryGetValue(entity.EntityId, out var geometry))
                {
                    continue;
                }

                geometry.SetActive(entity.IsActive);
                geometry.transform.position = ToWorld(entity.PositionX, entity.PositionY, 0f);
            }
        }

        private void SyncEnemyShips()
        {
            foreach (var enemy in connection.Connection.Db.Ship.Iter())
            {
                if (enemy.FactionCode != 2)
                {
                    continue;
                }

                if (!entities.TryGetValue(enemy.EntityId, out var enemyObject))
                {
                    enemyObject = CreateShip($"Enemy Ship {enemy.EntityId}", enemy.EntityId);
                    entities.Add(enemy.EntityId, enemyObject);
                    enemyObject.transform.position = ToWorld(enemy.PositionX, enemy.PositionY, ShipRootHeight);
                }

                enemyObject.SetActive(enemy.IsActive);
                targets[enemy.EntityId] = new PresentationTarget(
                    ToWorld(enemy.PositionX, enemy.PositionY, ShipRootHeight),
                    enemy.HeadingDegrees,
                    enemy.Speed);
                UpdateHealthBar(enemyObject, enemy.Hull, enemy.MaxHull);
                shipFeedback[enemy.EntityId].SetMotion(enemy.Speed, enemy.MaximumSpeed);
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
                playerObject = CreateShip("Player Ship", ship.EntityId);
                playerFeedback = shipFeedback[ship.EntityId];
                playerObject.transform.position = ToWorld(ship.PositionX, ship.PositionY, ShipRootHeight);
            }

            playerEntityId = ship.EntityId;

            targets[0] = new PresentationTarget(
                ToWorld(ship.PositionX, ship.PositionY, ShipRootHeight),
                ship.HeadingDegrees,
                ship.Speed);
            fogMaterial.SetVector(
                "_PlayerPosition",
                new Vector4(ship.PositionX, ship.PositionY, 0f, 0f));
            UpdateTargetRing(ship);
            UpdateCourseIndicator(ship);
            playerFeedback.SetMotion(ship.Speed, ship.MaximumSpeed);
        }

        private void SyncCombatPresentation()
        {
            var world = connection.Connection.Db.WorldState.Id.Find(1);
            if (world == null || combatPresenter == null)
            {
                return;
            }

            combatPresenter.BeginFrame();
            foreach (var volley in connection.Connection.Db.Volley.Iter())
            {
                if (!volley.IsActive)
                {
                    continue;
                }

                combatPresenter.Show(
                    volley,
                    world.Tick,
                    FindShipTransform(volley.SourceEntityId),
                    FindShipTransform(volley.TargetEntityId),
                    shipFeedback.TryGetValue(volley.SourceEntityId, out var feedback)
                        ? feedback
                        : null);
            }

            combatPresenter.EndFrame();
        }

        private Transform FindShipTransform(ulong entityId)
        {
            if (entityId == playerEntityId)
            {
                return playerObject != null ? playerObject.transform : null;
            }

            return entities.TryGetValue(entityId, out var ship) ? ship.transform : null;
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
                            target.Value.Position,
                            target.Value.HeadingDegrees,
                            Time.deltaTime,
                            Mathf.Max(presentationMovementSpeed, target.Value.Speed * 1.5f),
                            turnSpeedDegrees);
                    }

                    continue;
                }

                if (entities.TryGetValue(target.Key, out var entityObject))
                {
                    SeaShipMotion.Step(
                        entityObject.transform,
                        target.Value.Position,
                        target.Value.HeadingDegrees,
                        Time.deltaTime,
                        Mathf.Max(presentationMovementSpeed, target.Value.Speed * 1.5f),
                        turnSpeedDegrees);
                }
            }
        }

        private GameObject CreateShip(string name, ulong entityId)
        {
            if (shipModel == null)
            {
                throw new System.InvalidOperationException("The Apricum ship model is not configured.");
            }

            var ship = SeaShipVisualFactory.Create(
                shipModel,
                name,
                ShipFootprint,
                shipMaterial,
                modelYawOffset);
            var feedback = ship.AddComponent<SeaShipFeedback>();
            feedback.Configure(
                ship.transform.Find("Visual"),
                wakeMaterial,
                waterlineShadowMaterial,
                entityId * 0.37f);
            shipFeedback[entityId] = feedback;
            return ship;
        }

        private void CreateCourseIndicator()
        {
            var routeObject = new GameObject("Plotted Course");
            courseLine = routeObject.AddComponent<LineRenderer>();
            courseLine.sharedMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.91f, 0.72f, 0.39f, 0.58f));
            courseLine.positionCount = 2;
            courseLine.startWidth = 0.13f;
            courseLine.endWidth = 0.04f;
            courseLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var markerObject = new GameObject("Course Destination");
            destinationRing = markerObject.AddComponent<LineRenderer>();
            destinationRing.sharedMaterial = courseLine.sharedMaterial;
            destinationRing.loop = true;
            destinationRing.positionCount = 40;
            destinationRing.startWidth = 0.14f;
            destinationRing.endWidth = 0.14f;
            destinationRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            courseLine.gameObject.SetActive(false);
            destinationRing.gameObject.SetActive(false);
        }

        private void UpdateCourseIndicator(Ship ship)
        {
            var show = ship.HasCourse && playerObject != null;
            courseLine.gameObject.SetActive(show);
            destinationRing.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var start = playerObject.transform.position + Vector3.up * 0.18f;
            var destination = ToWorld(ship.DestinationX, ship.DestinationY, 0.08f);
            courseLine.positionCount = ship.HasWaypoint ? 3 : 2;
            courseLine.SetPosition(0, start);
            if (ship.HasWaypoint)
            {
                courseLine.SetPosition(1, ToWorld(ship.WaypointX, ship.WaypointY, 0.08f));
                courseLine.SetPosition(2, destination);
            }
            else
            {
                courseLine.SetPosition(1, destination);
            }
            const float markerRadius = 1.55f;
            for (var index = 0; index < destinationRing.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / destinationRing.positionCount;
                destinationRing.SetPosition(
                    index,
                    destination + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * markerRadius);
            }
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
            targetRing.transform.position = new Vector3(
                selectedObject.transform.position.x,
                WaterSurfaceHeight + 0.025f,
                selectedObject.transform.position.z);
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

    }
}
