using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sea.Client
{
    [Serializable]
    public sealed class SeaOwnedAssetEntry
    {
        [SerializeField] private string slotId;
        [SerializeField] private bool required;
        [SerializeField] private SeaOwnedAssetFallback fallback;
        [SerializeField] private AssetReference reference;

        public SeaOwnedAssetEntry(
            string id,
            bool isRequired,
            SeaOwnedAssetFallback fallbackMode,
            AssetReference assetReference)
        {
            slotId = id;
            required = isRequired;
            fallback = fallbackMode;
            reference = assetReference;
        }

        public string SlotId => slotId;
        public bool Required => required;
        public SeaOwnedAssetFallback Fallback => fallback;
        public AssetReference Reference => reference;
        public string AssetGuid => reference == null ? string.Empty : reference.AssetGUID;
        public bool HasValidReference => reference != null && reference.RuntimeKeyIsValid();
    }

    [CreateAssetMenu(menuName = "Sea/Owned asset catalog", fileName = "SeaOwnedAssets")]
    public sealed class SeaOwnedAssetCatalog : ScriptableObject
    {
        [SerializeField] private List<SeaOwnedAssetEntry> entries = new();

        public IReadOnlyList<SeaOwnedAssetEntry> Entries => entries;

        public SeaOwnedAssetEntry Require(string slotId)
        {
            var entry = entries.FirstOrDefault(value =>
                string.Equals(value.SlotId, slotId, StringComparison.Ordinal));
            return entry ?? throw new InvalidOperationException(
                $"Owned asset slot '{slotId}' is missing from the catalog.");
        }

#if UNITY_EDITOR
        public void Configure(IEnumerable<SeaOwnedAssetEntry> configuredEntries)
        {
            entries = configuredEntries.ToList();
        }
#endif
    }
}
