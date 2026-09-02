using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sea.Client
{
    public sealed class SeaOwnedAssetSet
    {
        private readonly IReadOnlyDictionary<SeaOwnedShipRole, GameObject> shipModels;

        public SeaOwnedAssetSet(
            IReadOnlyDictionary<SeaOwnedShipRole, GameObject> models,
            Material shipMaterial)
        {
            if (models == null)
            {
                throw new ArgumentNullException(nameof(models));
            }

            var validatedModels = new Dictionary<SeaOwnedShipRole, GameObject>(4);
            foreach (SeaOwnedShipRole role in Enum.GetValues(typeof(SeaOwnedShipRole)))
            {
                if (!models.TryGetValue(role, out var model) || model == null)
                {
                    throw new ArgumentException(
                        $"Owned ship role '{role}' has no model.",
                        nameof(models));
                }

                validatedModels.Add(role, model);
            }

            shipModels = validatedModels;
            ShipMaterial = shipMaterial != null
                ? shipMaterial
                : throw new ArgumentNullException(nameof(shipMaterial));
        }

        public Material ShipMaterial { get; }

        public GameObject ShipModel(SeaOwnedShipRole role) =>
            shipModels.TryGetValue(role, out var model) && model != null
                ? model
                : throw new InvalidOperationException($"Owned ship role '{role}' is not loaded.");
    }

    public sealed class SeaOwnedAssetLease
    {
        private static readonly SeaOwnedShipRole[] ShipRoles =
        {
            SeaOwnedShipRole.Player,
            SeaOwnedShipRole.Patrol,
            SeaOwnedShipRole.Raider,
            SeaOwnedShipRole.Gunship,
        };

        private readonly AsyncOperationHandle<GameObject>[] shipHandles =
            new AsyncOperationHandle<GameObject>[ShipRoles.Length];
        private readonly GameObject[] shipModels = new GameObject[ShipRoles.Length];
        private AsyncOperationHandle<Material> materialHandle;
        private Action<SeaOwnedAssetSet> onReady;
        private Action<Exception> onFailed;
        private Material material;
        private int loadedShipCount;
        private bool materialLoaded;
        private bool completed;
        private bool failed;
        private bool released;

        public bool IsReady => loadedShipCount == ShipRoles.Length &&
            materialLoaded && !failed && !released;
        public bool IsReleased => released;

        public void Load(
            SeaOwnedAssetCatalog catalog,
            Action<SeaOwnedAssetSet> ready,
            Action<Exception> failedCallback)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (onReady != null || released)
            {
                throw new InvalidOperationException("Owned assets can be loaded once per lease.");
            }

            onReady = ready ?? throw new ArgumentNullException(nameof(ready));
            onFailed = failedCallback ?? throw new ArgumentNullException(nameof(failedCallback));
            var materialEntry = catalog.Require(SeaOwnedAssetSlots.ShipMaterial);
            if (!materialEntry.HasValidReference)
            {
                throw new InvalidOperationException("Required owned asset references are invalid.");
            }

            var shipEntries = new SeaOwnedAssetEntry[ShipRoles.Length];
            for (var index = 0; index < ShipRoles.Length; index++)
            {
                var entry = catalog.Require(SeaOwnedAssetPolicy.ShipSlot(ShipRoles[index]));
                if (!entry.HasValidReference)
                {
                    throw new InvalidOperationException("Required owned asset references are invalid.");
                }

                shipEntries[index] = entry;
            }

            for (var index = 0; index < ShipRoles.Length; index++)
            {
                var roleIndex = index;
                shipHandles[index] = Addressables.LoadAssetAsync<GameObject>(
                    shipEntries[index].Reference.RuntimeKey);
                shipHandles[index].Completed += operation => HandleShipLoaded(roleIndex, operation);
            }

            materialHandle = Addressables.LoadAssetAsync<Material>(materialEntry.Reference.RuntimeKey);
            materialHandle.Completed += HandleMaterialLoaded;
        }

        public void Release()
        {
            if (released)
            {
                return;
            }

            released = true;
            foreach (var handle in shipHandles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            if (materialHandle.IsValid())
            {
                Addressables.Release(materialHandle);
            }

            Array.Clear(shipModels, 0, shipModels.Length);
            material = null;
            onReady = null;
            onFailed = null;
        }

        private void HandleShipLoaded(
            int roleIndex,
            AsyncOperationHandle<GameObject> operation)
        {
            if (released)
            {
                return;
            }

            if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
            {
                Fail(operation.OperationException ??
                    new InvalidOperationException("The owned ship model did not load."));
                return;
            }

            shipModels[roleIndex] = operation.Result;
            loadedShipCount++;
            CompleteIfReady();
        }

        private void HandleMaterialLoaded(AsyncOperationHandle<Material> operation)
        {
            if (released)
            {
                return;
            }

            if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
            {
                Fail(operation.OperationException ??
                    new InvalidOperationException("The owned ship material did not load."));
                return;
            }

            material = operation.Result;
            materialLoaded = true;
            CompleteIfReady();
        }

        private void CompleteIfReady()
        {
            if (IsReady && !completed)
            {
                completed = true;
                var models = new Dictionary<SeaOwnedShipRole, GameObject>(ShipRoles.Length);
                for (var index = 0; index < ShipRoles.Length; index++)
                {
                    models.Add(ShipRoles[index], shipModels[index]);
                }

                onReady(new SeaOwnedAssetSet(models, material));
            }
        }

        private void Fail(Exception error)
        {
            if (failed || released)
            {
                return;
            }

            failed = true;
            var callback = onFailed;
            try
            {
                callback(error);
            }
            finally
            {
                Release();
            }
        }
    }
}
