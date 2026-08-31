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
        private const string ShipModelPath = "Assets/Art/Ships/StarterShip/StarterShip.fbx";
        private const string InputActionsPath = "Assets/Input/SeaControls.inputactions";
        private const string HudDocumentPath = "Assets/UI/SeaHud.uxml";
        private const string HudStylePath = "Assets/UI/SeaHud.uss";
        private const string PanelSettingsPath = "Assets/UI/SeaPanelSettings.asset";

        [MenuItem("Sea/Build Main Scene")]
        public static void CreateMainScene()
        {
            EnsureDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SeaGame");
            root.AddComponent<SeaConnectionController>();
            root.AddComponent<SeaFrameRateController>();
            root.AddComponent<SeaChartCameraController>();
            root.AddComponent<SeaGameController>();
            root.AddComponent<SeaRuntimeValidationProbe>();
            var world = root.AddComponent<SeaWorldView>();
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);
            if (shipModel == null)
            {
                throw new System.InvalidOperationException("Starter ship model is missing at " + ShipModelPath);
            }

            world.ConfigureShipModel(shipModel);

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
            camera.orthographicSize = 45f;
            camera.transform.position = new Vector3(0f, 70f, -50f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.09f, 0.15f, 1f);

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
