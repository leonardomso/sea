#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sea.Client;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sea.Editor
{
    public static class SeaOwnedAssetEditorLifecycle
    {
        public const string CatalogPath = "Assets/Art/SeaOwnedAssets.asset";
        public const string ShipModelPath = "Assets/Art/Ships/Apricum/Apricum.fbx";
        public const string ShipMaterialPath = "Assets/Art/Ships/Apricum/Apricum.mat";
        public const string ShipBaseColorPath =
            "Assets/Art/Ships/Apricum/Textures/Apricum_BaseColor.png";
        public const string ShipNormalPath =
            "Assets/Art/Ships/Apricum/Textures/Apricum_Normal.png";
        public const string ShipMetallicSmoothnessPath =
            "Assets/Art/Ships/Apricum/Textures/Apricum_MetallicSmoothness.png";
        public const string ShipTextureRoot = "Assets/Art/Ships/Apricum/Textures/";

        private const string SettingsFolder = "Assets/AddressableAssetsData";
        private const string SettingsName = "AddressableAssetSettings";
        private const string GroupName = "Sea Owned Assets";
        private const string ContentStatePath = "Build/AddressablesContentState";
        private const string ShipAddress = "sea/ships/apricum";
        private const string MaterialAddress = "sea/materials/apricum";

        public static SeaOwnedAssetCatalog EnsureCatalog()
        {
            EnsureShipMaterial();
            var settings = EnsureAddressableSettings();
            var catalog = AssetDatabase.LoadAssetAtPath<SeaOwnedAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SeaOwnedAssetCatalog>();
                catalog.name = "Sea Owned Assets";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = SeaOwnedAssetPolicy.Definitions
                .Select(definition => CreateCatalogEntry(settings, definition))
                .ToArray();
            catalog.Configure(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static IReadOnlyList<string> Validate(SeaOwnedAssetCatalog catalog) =>
            SeaOwnedAssetValidator.Validate(catalog, EnsureAddressableSettings());

        public static void PrepareForBuild()
        {
            var catalog = EnsureCatalog();
            var errors = Validate(catalog);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }
        }

        public static bool IsShipModelPath(string path) =>
            string.Equals(path, ShipModelPath, StringComparison.Ordinal);

        public static bool IsShipTexturePath(string path) =>
            path.StartsWith(ShipTextureRoot, StringComparison.Ordinal);

        public static string ExpectedAddress(string slotId) => slotId switch
        {
            SeaOwnedAssetSlots.PlayerShip or
            SeaOwnedAssetSlots.SkiffShip or
            SeaOwnedAssetSlots.ReefCrabShip or
            SeaOwnedAssetSlots.FancyShip or
            SeaOwnedAssetSlots.RedMaryShip => ShipAddress,
            SeaOwnedAssetSlots.ShipMaterial => MaterialAddress,
            _ => string.Empty,
        };

        private static SeaOwnedAssetEntry CreateCatalogEntry(
            AddressableAssetSettings settings,
            SeaOwnedAssetDefinition definition)
        {
            var path = AssetPath(definition.Id);
            AssetReference reference = null;
            if (!string.IsNullOrEmpty(path))
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException(
                        $"Owned asset '{definition.Id}' is missing at {path}.");
                }

                var group = EnsureOwnedGroup(settings);
                var addressableEntry = settings.CreateOrMoveEntry(guid, group);
                addressableEntry.address = ExpectedAddress(definition.Id);
                addressableEntry.SetLabel("sea-owned", true, true, false);
                reference = new AssetReference(guid);
            }

            return new SeaOwnedAssetEntry(
                definition.Id,
                definition.Required,
                definition.Fallback,
                reference);
        }

        private static AddressableAssetSettings EnsureAddressableSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    SettingsFolder,
                    SettingsName,
                    createDefaultGroups: true,
                    isPersisted: true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }

            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            settings.ContentStateBuildPath = ContentStatePath;
            EnsureOwnedGroup(settings);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static AddressableAssetGroup EnsureOwnedGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(GroupName);
            if (group != null)
            {
                return group;
            }

            if (settings.DefaultGroup == null)
            {
                throw new InvalidOperationException("Addressables has no default local group.");
            }

            return settings.CreateGroup(
                GroupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: true,
                schemasToCopy: settings.DefaultGroup.Schemas.ToList());
        }

        private static string AssetPath(string slotId) => slotId switch
        {
            SeaOwnedAssetSlots.PlayerShip or
            SeaOwnedAssetSlots.SkiffShip or
            SeaOwnedAssetSlots.ReefCrabShip or
            SeaOwnedAssetSlots.FancyShip or
            SeaOwnedAssetSlots.RedMaryShip => ShipModelPath,
            SeaOwnedAssetSlots.ShipMaterial => ShipMaterialPath,
            _ => string.Empty,
        };

        private static void EnsureShipMaterial()
        {
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipBaseColorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipNormalPath);
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipMetallicSmoothnessPath);
            if (baseColor == null || normal == null || metallic == null)
            {
                throw new InvalidOperationException("Owned ship PBR textures are incomplete.");
            }

            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported lit shader is available.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(ShipMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Apricum" };
                AssetDatabase.CreateAsset(material, ShipMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTexture(material, "_BaseMap", baseColor);
            SetTexture(material, "_MainTex", baseColor);
            SetTexture(material, "_BumpMap", normal);
            SetTexture(material, "_MetallicGlossMap", metallic);
            SetFloat(material, "_Metallic", 1f);
            SetFloat(material, "_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
#endif
