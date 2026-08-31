#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Sea.Client;

namespace Sea.Editor
{
    public static class SeaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ShipModelPath = "Assets/Art/Ships/Apricum/Apricum.fbx";
        private const string ShipMaterialPath = "Assets/Art/Ships/Apricum/Apricum.mat";
        private const string ShipBaseColorPath = "Assets/Art/Ships/Apricum/Textures/Apricum_BaseColor.png";
        private const string ShipNormalPath = "Assets/Art/Ships/Apricum/Textures/Apricum_Normal.png";
        private const string ShipMetallicSmoothnessPath =
            "Assets/Art/Ships/Apricum/Textures/Apricum_MetallicSmoothness.png";
        private const string InputActionsPath = "Assets/Input/SeaControls.inputactions";
        private const string HudDocumentPath = "Assets/UI/SeaHud.uxml";
        private const string HudStylePath = "Assets/UI/SeaHud.uss";
        private const string PanelSettingsPath = "Assets/UI/SeaPanelSettings.asset";
        private const string FogShaderPath = "Assets/Shaders/SeaChartFog.shader";

        [MenuItem("Sea/Build Main Scene")]
        public static void CreateMainScene()
        {
            EnsureDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SeaGame");
            root.AddComponent<SeaConnectionController>();
            root.AddComponent<SeaFrameRateController>();
            var chartCameraController = root.AddComponent<SeaChartCameraController>();
            root.AddComponent<SeaGameController>();
            root.AddComponent<SeaRuntimeValidationProbe>();
            var world = root.AddComponent<SeaWorldView>();
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);
            if (shipModel == null)
            {
                throw new System.InvalidOperationException("Apricum ship model is missing at " + ShipModelPath);
            }

            world.ConfigureShipAssets(shipModel, EnsureShipMaterial());
            var fogShader = AssetDatabase.LoadAssetAtPath<Shader>(FogShaderPath);
            if (fogShader == null)
            {
                throw new System.InvalidOperationException("Chart fog shader is missing at " + FogShaderPath);
            }

            world.ConfigureFogShader(fogShader);

            var hudDocument = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudDocumentPath);
            var hudStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudStylePath);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (hudDocument == null || hudStyle == null || inputActions == null)
            {
                throw new System.InvalidOperationException("HUD or input assets are missing.");
            }

            var uiDocument = root.AddComponent<UIDocument>();
            uiDocument.panelSettings = EnsurePanelSettings();
            uiDocument.visualTreeAsset = hudDocument;
            uiDocument.sortingOrder = 100;
            root.AddComponent<SeaHudController>().Configure(hudStyle);
            root.AddComponent<SeaInputController>().Configure(inputActions);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 34f;
            camera.transform.position = new Vector3(0f, 70f, -50f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.09f, 0.15f, 1f);

            var miniMapObject = new GameObject("Mini Map Camera");
            var miniMapCamera = miniMapObject.AddComponent<Camera>();
            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = 108f;
            miniMapCamera.transform.position = new Vector3(0f, 180f, 0f);
            miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = new Color(0.018f, 0.10f, 0.13f, 1f);
            miniMapCamera.depth = 1f;
            miniMapCamera.rect = new Rect(0.82f, 0.69f, 0.17f, 0.21f);
            miniMapCamera.cullingMask &= ~(1 << 8);
            chartCameraController.Configure(camera, miniMapCamera);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Sea main scene created at " + ScenePath);
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "Sea Panel Settings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Material EnsureShipMaterial()
        {
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipBaseColorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipNormalPath);
            var metallicSmoothness = AssetDatabase.LoadAssetAtPath<Texture2D>(ShipMetallicSmoothnessPath);
            if (baseColor == null || normal == null || metallicSmoothness == null)
            {
                throw new System.InvalidOperationException("Apricum PBR textures are incomplete.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(ShipMaterialPath);
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new System.InvalidOperationException("No lit shader is available for Apricum.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "Apricum" };
                AssetDatabase.CreateAsset(material, ShipMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            SetTextureIfSupported(material, "_BaseMap", baseColor);
            SetTextureIfSupported(material, "_MainTex", baseColor);
            SetTextureIfSupported(material, "_BumpMap", normal);
            SetTextureIfSupported(material, "_MetallicGlossMap", metallicSmoothness);
            SetFloatIfSupported(material, "_Metallic", 1f);
            SetFloatIfSupported(material, "_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetTextureIfSupported(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        private static void SetFloatIfSupported(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                var folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
