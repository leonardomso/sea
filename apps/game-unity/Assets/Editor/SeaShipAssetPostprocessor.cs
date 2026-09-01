#if UNITY_EDITOR
using UnityEditor;

namespace Sea.Editor
{
    public sealed class SeaShipAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!SeaOwnedAssetEditorLifecycle.IsShipModelPath(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }

        private void OnPreprocessTexture()
        {
            if (!SeaOwnedAssetEditorLifecycle.IsShipTexturePath(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            if (assetPath.EndsWith("_Normal.png", System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.flipGreenChannel = false;
                return;
            }

            if (assetPath.EndsWith("_MetallicSmoothness.png", System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
        }
    }
}
#endif
