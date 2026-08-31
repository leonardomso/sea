#if UNITY_EDITOR
using UnityEditor;

namespace Sea.Editor
{
    public sealed class SeaShipAssetPostprocessor : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Art/Ships/Apricum/Apricum.fbx";
        private const string TextureRoot = "Assets/Art/Ships/Apricum/Textures/";

        private void OnPreprocessModel()
        {
            if (assetPath != ModelPath)
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
            if (!assetPath.StartsWith(TextureRoot, System.StringComparison.Ordinal))
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
