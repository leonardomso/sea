using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaWorldView
    {
        private const int MaximumVisibleLoot = 256;
        private readonly Dictionary<ulong, GameObject> lootPresentations = new();
        private readonly List<ulong> lootReleaseIds = new(MaximumVisibleLoot);
        private SeaBoundedPool<GameObject> lootPool;
        private Material lootMaterial;

        private void InitializeLootPresentation()
        {
            lootMaterial = SeaMaterialFactory.Create(new Color(0.94f, 0.68f, 0.18f, 1f));
            lootPool = new SeaBoundedPool<GameObject>(
                CreateLootPresentation,
                ResetLootPresentation,
                initialCapacity: 8,
                maximumCapacity: MaximumVisibleLoot);
        }

        private void HandleLootChanged(Loot loot)
        {
            if (!loot.IsActive)
            {
                HandleLootRemoved(loot.LootId);
                return;
            }

            if (!lootPresentations.TryGetValue(loot.LootId, out var presentation))
            {
                if (!lootPool.TryAcquire(out presentation))
                {
                    return;
                }

                lootPresentations.Add(loot.LootId, presentation);
                presentation.name = $"Loot {loot.LootId}";
                presentation.SetActive(true);
            }

            presentation.transform.position = ToWorld(
                loot.PositionX,
                loot.PositionY,
                WaterSurfaceHeight + 0.35f);
        }

        private void HandleLootRemoved(ulong lootId)
        {
            if (lootPresentations.Remove(lootId, out var presentation) && presentation != null)
            {
                lootPool.Release(presentation);
            }
        }

        private void ResetLootPresentations()
        {
            lootReleaseIds.Clear();
            foreach (var lootId in lootPresentations.Keys)
            {
                lootReleaseIds.Add(lootId);
            }

            foreach (var lootId in lootReleaseIds)
            {
                HandleLootRemoved(lootId);
            }
        }

        private GameObject CreateLootPresentation()
        {
            var loot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            loot.name = "Pooled Loot";
            loot.transform.localScale = new Vector3(1.1f, 0.55f, 0.8f);
            loot.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            loot.GetComponent<Renderer>().sharedMaterial = lootMaterial;
            Destroy(loot.GetComponent<Collider>());
            return loot;
        }

        private static void ResetLootPresentation(GameObject loot)
        {
            if (loot != null)
            {
                loot.SetActive(false);
            }
        }
    }
}
