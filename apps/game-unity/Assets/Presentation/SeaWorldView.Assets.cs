using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        [SerializeField] private SeaOwnedAssetCatalog ownedAssets;

        private SeaOwnedAssetLease ownedAssetLease;
        private SeaOwnedAssetSet ownedAssetSet;
        private SeaKeyedBoundedPool<SeaOwnedShipRole, GameObject> shipPool;
        private bool assetsReady;

        public SeaOwnedAssetCatalog OwnedAssets => ownedAssets;
        public GameObject ShipModel => ownedAssetSet?.ShipModel(SeaOwnedShipRole.Player);
        public Material ShipMaterial => ownedAssetSet?.ShipMaterial;

        public void ConfigureOwnedAssets(SeaOwnedAssetCatalog catalog) => ownedAssets = catalog;

        private void BeginOwnedAssetLoad()
        {
            if (ownedAssetSet != null)
            {
                assetsReady = true;
                return;
            }

            if (ownedAssets == null)
            {
                return;
            }

            ownedAssetLease = new SeaOwnedAssetLease();
            ownedAssetLease.Load(
                ownedAssets,
                ConfigureLoadedAssets,
                error => Debug.LogException(error, this));
        }

        private void ConfigureLoadedAssets(SeaOwnedAssetSet assets)
        {
            ownedAssetSet = assets;
            var visibleLimit = SeaPresentationRules.VisibleShipLimit(
                SeaPresentationRules.CurrentPlatform());
            shipPool = new SeaKeyedBoundedPool<SeaOwnedShipRole, GameObject>(
                CreatePooledShip,
                ResetPooledShip,
                visibleLimit);
            assetsReady = true;
            visibilityDirty = true;
        }

        private GameObject CreatePooledShip(SeaOwnedShipRole role)
        {
            var material = ownedAssetSet.ShipMaterial;
            var ship = SeaShipVisualFactory.Create(
                ownedAssetSet.ShipModel(role),
                $"Pooled {role} Ship",
                ShipFootprint,
                material,
                modelYawOffset);
            var feedback = ship.AddComponent<SeaShipFeedback>();
            var visual = ship.transform.Find("Visual");
            feedback.Configure(visual, wakeMaterial, waterlineShadowMaterial, 0f);

            var modelBounds = SeaShipVisualFactory.CalculateRendererBounds(ship);
            var modelTop = ship.transform.InverseTransformPoint(
                new Vector3(modelBounds.center.x, modelBounds.max.y, modelBounds.center.z));
            var health = SeaPrimitive.Create(PrimitiveType.Cube, "Health", healthMaterial);
            health.transform.SetParent(ship.transform, false);
            health.transform.localPosition = new Vector3(0f, modelTop.y + 0.6f, 0f);

            var presentation = ship.AddComponent<SeaShipPresentation>();
            presentation.Configure(visual, feedback, health.transform);
            return ship;
        }

        private static void ResetPooledShip(GameObject ship)
        {
            if (ship != null)
            {
                ship.GetComponent<SeaShipPresentation>().ResetForPool();
            }
        }
    }
}
