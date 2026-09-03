using System.Collections.Generic;
using SpacetimeDB.Types;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Profiling;

namespace Sea.Client
{
    public sealed partial class SeaWorldView : MonoBehaviour
    {
        private static readonly ProfilerMarker VisibilityMarker =
            new("Sea.Presentation.Visibility");
        private static readonly ProfilerMarker InterpolationMarker =
            new("Sea.Presentation.Interpolation");
        private static readonly ProfilerMarker EffectsMarker =
            new("Sea.Presentation.Effects");

        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private Shader fogShader;
        [SerializeField] private float modelYawOffset = 270f;

        private const float ShipFootprint = 10f;
        private const int MaximumTrackedShipRows = 6000;
        private const int MainChartFogLayer = 8;
        // A Unity plane is ten units across per unit of scale. The water and fog reach one camera
        // margin past each map edge, because the camera stays centred on a ship sailing along the
        // edge and would otherwise frame the void beyond it.
        private const float MapPlaneSpan =
            SeaChartCoordinates.MapMaximum - SeaChartCoordinates.MapMinimum
            + (2f * SeaChartCameraRules.MapMargin);
        private static readonly Vector3 MapPlaneScale = new(
            MapPlaneSpan / 10f,
            1f,
            MapPlaneSpan / 10f);
        public const float WaterSurfaceHeight = -0.35f;
        public const float ShipRootHeight = -0.43f;

        private readonly Dictionary<ulong, GameObject> entities = new();
        private readonly Dictionary<ulong, GameObject> mapGeometry = new();
        private readonly SeaRowRegistry<ulong, Ship> shipRows = new();
        private readonly SeaRowRegistry<ulong, Volley> volleyRows = new();
        private readonly Dictionary<ulong, SeaMotionTimeline> targets = new();
        private SeaSnapshotClock snapshotClock;
        private readonly Dictionary<ulong, SeaShipFeedback> shipFeedback = new();
        private readonly List<SeaVisibilityCandidate> visibilityCandidates = new(256);
        private readonly HashSet<ulong> desiredPresentations = new();
        private readonly List<ulong> releaseEntityIds = new(256);
        private readonly ulong[] visibilityEntityIds = new ulong[MaximumTrackedShipRows];
        private NativeArray<float2> visibilityPositions;
        private NativeArray<float> visibilitySquaredDistances;
        private GameObject playerObject;
        private SeaShipFeedback playerFeedback;
        private GameObject targetRing;
        private GameObject ownShipRing;
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
        private Material healthMaterial;
        private Material targetMaterial;
        private Material ownShipMaterial;
        private SeaCombatPresenter combatPresenter;
        private ulong playerEntityId;
        private ulong worldTick;
        private Ship localShip;
        private Transform chartCameraTransform;
        private Vector3 previousVisibilityOrigin = new(float.PositiveInfinity, 0f, 0f);
        private bool visibilityDirty = true;
        private LineRenderer courseLine;
        private LineRenderer destinationRing;

        public Shader FogShader => fogShader;
        public float ModelYawOffset => modelYawOffset;

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

        public void ConfigureFogShader(Shader shader)
        {
            fogShader = shader;
        }

        public void ConfigureDependencies(
            SeaConnectionController connectionController,
            Camera chartCamera)
        {
            connection = connectionController;
            chartCameraTransform = chartCamera != null ? chartCamera.transform : null;
            BindInterestCallbacks(connectionController);
        }

        // Camera.main tags-searches the scene on every call, so the composer hands the
        // chart camera over and the search only runs when nothing was wired.
        private Transform ChartCameraTransform()
        {
            if (chartCameraTransform == null && Camera.main != null)
            {
                chartCameraTransform = Camera.main.transform;
            }

            return chartCameraTransform;
        }

        private void Awake()
        {
            BindInterestCallbacks(connection);
            CreateMaterials();
            CreateWater();
            CreateFog();
            BeginOwnedAssetLoad();
            InitializeLootPresentation();
            visibilityPositions = new NativeArray<float2>(
                MaximumTrackedShipRows,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            visibilitySquaredDistances = new NativeArray<float>(
                MaximumTrackedShipRows,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void Update()
        {
            if (connection?.Connection == null || !connection.IsSubscribed)
            {
                return;
            }

            if (!assetsReady)
            {
                return;
            }

            worldTick = connection.CurrentWorldTick;

            using (VisibilityMarker.Auto())
            {
                ReconcileVisibility();
            }

            using (InterpolationMarker.Auto())
            {
                UpdateEntityTransforms();
            }

            UpdateLocalPresentation();
            using (EffectsMarker.Auto())
            {
                SyncCombatPresentation();
            }
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
            fogMaterial.SetFloat("_VisionRadius", SeaPresentationRules.VisionRadius);
            fogMaterial.SetFloat("_FadeWidth", 12f);
            fogMaterial.SetColor("_FogColor", new Color(0.015f, 0.05f, 0.065f, 0.96f));
            cannonballMaterial = SeaMaterialFactory.Create(new Color(0.04f, 0.035f, 0.03f, 1f));
            combatEffectMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.78f, 0.87f, 0.90f, 0.9f));
            shoalMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.18f, 0.78f, 0.68f, 0.34f));
            stormMaterial = SeaMaterialFactory.CreateTransparent(
                new Color(0.10f, 0.14f, 0.18f, 0.82f));
            healthMaterial = SeaMaterialFactory.Create(Color.white);
            targetMaterial = SeaMaterialFactory.Create(new Color(1f, 0.85f, 0.25f, 1f));
            ownShipMaterial = SeaMaterialFactory.Create(new Color(0.2f, 0.9f, 0.35f, 1f));
            combatPresenter = new SeaCombatPresenter(cannonballMaterial, combatEffectMaterial);
        }

        private void CreateWater()
        {
            var water = SeaPrimitive.Create(PrimitiveType.Plane, "Water Surface", waterMaterial);
            water.transform.position = new Vector3(0f, WaterSurfaceHeight, 0f);
            water.transform.localScale = MapPlaneScale;
            CreateCourseIndicator();
        }

        private void CreateFog()
        {
            var fog = SeaPrimitive.Create(PrimitiveType.Plane, "Player Vision Fog", fogMaterial);
            fog.layer = MainChartFogLayer;
            fog.transform.position = new Vector3(0f, 8f, 0f);
            fog.transform.localScale = MapPlaneScale;
            var renderer = fog.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void SyncCombatPresentation()
        {
            if (combatPresenter == null)
            {
                return;
            }

            combatPresenter.BeginFrame();
            foreach (var volley in volleyRows.Values)
            {
                if (!volley.IsActive)
                {
                    continue;
                }

                combatPresenter.Show(
                    volley,
                    worldTick,
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
            if (snapshotClock == null)
            {
                return;
            }

            var renderTick = snapshotClock.RenderTick(Time.realtimeSinceStartupAsDouble);
            foreach (var target in targets)
            {
                if (target.Value.HasSamples &&
                    entities.TryGetValue(target.Key, out var entityObject))
                {
                    var sample = target.Value.Sample(renderTick);
                    entityObject.transform.SetPositionAndRotation(
                        sample.Position,
                        Quaternion.Euler(0f, sample.HeadingDegrees, 0f));
                }
            }
        }

        private GameObject CreateShip(string name, Ship row)
        {
            var role = SeaOwnedAssetPolicy.ShipRole(row.FactionCode, row.ArchetypeCode);
            if (!shipPool.TryAcquire(role, out var ship))
            {
                return null;
            }

            var presentation = ship.GetComponent<SeaShipPresentation>();
            presentation.Bind(row.EntityId, name);
            shipFeedback[row.EntityId] = presentation.Feedback;
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
            if (ship.TargetEntityId == 0 ||
                !entities.TryGetValue(ship.TargetEntityId, out var selectedObject) ||
                !SeaPresentationRules.IsInVision(Vector3.Distance(
                    PlayerChartPosition(),
                    selectedObject.transform.position)))
            {
                if (targetRing != null)
                {
                    targetRing.SetActive(false);
                }

                return;
            }

            targetRing ??= CreateRing("Selected Target Ring", 4.5f, targetMaterial);
            PlaceRing(targetRing, selectedObject);
        }

        // The green disc marks the player's own ship so it reads at a glance among NPCs.
        private void UpdateOwnShipRing()
        {
            if (playerObject == null)
            {
                if (ownShipRing != null)
                {
                    ownShipRing.SetActive(false);
                }

                return;
            }

            ownShipRing ??= CreateRing("Own Ship Ring", 5.5f, ownShipMaterial);
            PlaceRing(ownShipRing, playerObject);
        }

        private GameObject CreateRing(string name, float diameter, Material material)
        {
            var ring = SeaPrimitive.Create(PrimitiveType.Cylinder, name, material);
            ring.transform.localScale = new Vector3(diameter, 0.04f, diameter);
            return ring;
        }

        private static void PlaceRing(GameObject ring, GameObject ship)
        {
            ring.SetActive(ship.activeSelf);
            ring.transform.position = new Vector3(
                ship.transform.position.x,
                WaterSurfaceHeight + 0.025f,
                ship.transform.position.z);
        }

    }
}
