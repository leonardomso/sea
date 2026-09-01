#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sea.Client;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Sea.Editor
{
    public static class SeaOwnedAssetValidator
    {
        private const long MaximumShipTriangles = 30_000;

        public static IReadOnlyList<string> Validate(
            SeaOwnedAssetCatalog catalog,
            AddressableAssetSettings settings)
        {
            var errors = new List<string>();
            ValidateCatalog(catalog, settings, errors);
            ValidateModel(errors);
            ValidateTextures(errors);
            ValidateMaterial(errors);
            ValidateRuntimeTransform(errors);
            return errors;
        }

        private static void ValidateCatalog(
            SeaOwnedAssetCatalog catalog,
            AddressableAssetSettings settings,
            List<string> errors)
        {
            if (!settings.ContentStateBuildPath.StartsWith("Build/", StringComparison.Ordinal))
            {
                errors.Add("Addressables content-state output must stay in ignored build artifacts.");
            }

            if (catalog == null)
            {
                errors.Add("The owned asset catalog is missing.");
                return;
            }

            var definitions = SeaOwnedAssetPolicy.Definitions.ToDictionary(value => value.Id);
            var duplicate = catalog.Entries.GroupBy(value => value.SlotId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                errors.Add($"Owned asset slot '{duplicate.Key}' appears more than once.");
            }

            foreach (var definition in definitions.Values)
            {
                var entries = catalog.Entries.Where(value =>
                    string.Equals(value.SlotId, definition.Id, StringComparison.Ordinal)).ToArray();
                if (entries.Length != 1)
                {
                    errors.Add($"Owned asset slot '{definition.Id}' must appear exactly once.");
                    continue;
                }

                var entry = entries[0];
                if (definition.Required && !entry.HasValidReference)
                {
                    errors.Add($"Required owned asset slot '{definition.Id}' has no reference.");
                    continue;
                }

                if (!definition.Required && entry.Fallback == SeaOwnedAssetFallback.None)
                {
                    errors.Add($"Optional owned asset slot '{definition.Id}' has no fallback.");
                }

                if (!entry.HasValidReference)
                {
                    continue;
                }

                var addressable = settings.FindAssetEntry(entry.AssetGuid);
                var expectedAddress = SeaOwnedAssetEditorLifecycle.ExpectedAddress(entry.SlotId);
                if (addressable == null ||
                    !string.Equals(addressable.address, expectedAddress, StringComparison.Ordinal))
                {
                    errors.Add($"Owned asset slot '{definition.Id}' has a broken Addressable entry.");
                }
            }

            foreach (var unknown in catalog.Entries.Where(value =>
                         !definitions.ContainsKey(value.SlotId)))
            {
                errors.Add($"Unknown owned asset slot '{unknown.SlotId}' is present.");
            }
        }

        private static void ValidateModel(List<string> errors)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                SeaOwnedAssetEditorLifecycle.ShipModelPath);
            var importer = AssetImporter.GetAtPath(
                SeaOwnedAssetEditorLifecycle.ShipModelPath) as ModelImporter;
            if (model == null || importer == null)
            {
                errors.Add("The owned ship FBX is missing or has the wrong importer.");
                return;
            }

            if (importer.importAnimation || importer.importCameras || importer.importLights ||
                importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                errors.Add("The owned ship FBX import policy is not applied.");
            }

            var meshes = model.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .ToArray();
            if (meshes.Length != 1)
            {
                errors.Add("The owned ship FBX must contain one runtime mesh and no studio geometry.");
                return;
            }

            var triangles = meshes.Sum(mesh => Enumerable.Range(0, mesh.subMeshCount)
                .Sum(subMesh => (long)mesh.GetIndexCount(subMesh) / 3));
            if (triangles > MaximumShipTriangles)
            {
                errors.Add($"The owned ship FBX has {triangles} triangles; limit is {MaximumShipTriangles}.");
            }
        }

        private static void ValidateTextures(List<string> errors)
        {
            var baseColor = AssetImporter.GetAtPath(
                SeaOwnedAssetEditorLifecycle.ShipBaseColorPath) as TextureImporter;
            var normal = AssetImporter.GetAtPath(
                SeaOwnedAssetEditorLifecycle.ShipNormalPath) as TextureImporter;
            var metallic = AssetImporter.GetAtPath(
                SeaOwnedAssetEditorLifecycle.ShipMetallicSmoothnessPath) as TextureImporter;
            if (baseColor == null || normal == null || metallic == null)
            {
                errors.Add("The owned ship texture set is incomplete.");
                return;
            }

            if (!baseColor.sRGBTexture || normal.textureType != TextureImporterType.NormalMap ||
                normal.flipGreenChannel || metallic.sRGBTexture ||
                metallic.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                errors.Add("The owned ship texture color-space or normal-map policy is invalid.");
            }
        }

        private static void ValidateMaterial(List<string> errors)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                SeaOwnedAssetEditorLifecycle.ShipMaterialPath);
            if (material == null || material.shader == null || !material.shader.isSupported ||
                string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal))
            {
                errors.Add("The owned ship material is missing, pink, or unsupported.");
                return;
            }

            if ((material.GetTexture("_BaseMap") ?? material.GetTexture("_MainTex")) == null ||
                material.GetTexture("_BumpMap") == null ||
                material.GetTexture("_MetallicGlossMap") == null)
            {
                errors.Add("The owned ship material is missing one or more PBR textures.");
            }
        }

        private static void ValidateRuntimeTransform(List<string> errors)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                SeaOwnedAssetEditorLifecycle.ShipModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                SeaOwnedAssetEditorLifecycle.ShipMaterialPath);
            if (model == null || material == null)
            {
                return;
            }

            var ship = SeaShipVisualFactory.Create(
                model,
                "Owned ship validation",
                targetFootprint: 10f,
                authoredMaterial: material,
                modelYawOffsetDegrees: 270f);
            try
            {
                ship.transform.position = Vector3.up * SeaWorldView.ShipRootHeight;
                var bounds = SeaShipVisualFactory.CalculateRendererBounds(ship);
                var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                var submerged = SeaWorldView.WaterSurfaceHeight - bounds.min.y;
                if (Mathf.Abs(footprint - 10f) > 0.05f || submerged < 0.04f || submerged > 0.16f)
                {
                    errors.Add("The owned ship scale, pivot, or waterline alignment is invalid.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ship);
            }
        }
    }
}
#endif
