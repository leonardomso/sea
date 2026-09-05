#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Sea.Client;
using Sea.Bootstrap;

namespace Sea.Editor
{
    public static class SeaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
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
            var connection = root.AddComponent<SeaConnectionController>();
            root.AddComponent<SeaFrameRateController>();
            var chartCameraController = root.AddComponent<SeaChartCameraController>();
            var game = root.AddComponent<SeaGameController>();
            var validationProbe = root.AddComponent<SeaRuntimeValidationProbe>();
            var world = root.AddComponent<SeaWorldView>();
            world.ConfigureOwnedAssets(SeaOwnedAssetEditorLifecycle.EnsureCatalog());
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
            var hud = root.AddComponent<SeaHudController>();
            hud.Configure(hudStyle);
            var input = root.AddComponent<SeaInputController>();
            input.Configure(inputActions);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = SeaChartCameraRules.DefaultZoom;
            camera.transform.position = SeaChartCameraRules.ChartCameraStartPosition();
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.09f, 0.15f, 1f);

            var miniMapObject = new GameObject("Mini Map Camera");
            var miniMapCamera = miniMapObject.AddComponent<Camera>();
            miniMapCamera.orthographic = true;
            // The minimap shows exactly the map; the HUD keeps its viewport square.
            miniMapCamera.orthographicSize = SeaChartCameraRules.MiniMapOrthographicSize;
            miniMapCamera.transform.position = SeaChartCameraRules.MiniMapCameraPosition();
            miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = new Color(0.018f, 0.10f, 0.13f, 1f);
            miniMapCamera.depth = 1f;
            miniMapCamera.rect = new Rect(0.82f, 0.69f, 0.17f, 0.21f);
            miniMapCamera.cullingMask &= ~(1 << 8);
            chartCameraController.Configure(camera, miniMapCamera);
            root.AddComponent<SeaLifetimeScope>();
            root.AddComponent<SeaSceneComposer>().Configure(
                connection,
                game,
                chartCameraController,
                world,
                hud,
                input,
                validationProbe,
                camera);

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
