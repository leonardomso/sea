#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sea.Client;

namespace Sea.Editor
{
    public static class SeaSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ShipModelPath = "Assets/Art/Ships/StarterShip/StarterShip.fbx";

        [MenuItem("Sea/Build Main Scene")]
        public static void CreateMainScene()
        {
            EnsureDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SeaGame");
            root.AddComponent<SeaConnectionController>();
            root.AddComponent<SeaConnectionOverlay>();
            root.AddComponent<SeaFrameRateController>();
            root.AddComponent<SeaGameController>();
            root.AddComponent<SeaRuntimeValidationProbe>();
            var world = root.AddComponent<SeaWorldView>();
            var shipModel = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);
            if (shipModel == null)
            {
                throw new System.InvalidOperationException("Starter ship model is missing at " + ShipModelPath);
            }

            world.ConfigureShipModel(shipModel);

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
