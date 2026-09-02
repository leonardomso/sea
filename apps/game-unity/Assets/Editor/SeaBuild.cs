#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sea.Editor
{
    public static class SeaBuild
    {
        public static void PerformWebGLBuild()
        {
            Build(BuildTarget.WebGL, "Build/WebGL");
        }

        public static void PerformMacOSBuild()
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
            Build(BuildTarget.StandaloneOSX, "Build/Sea.app");
        }

        private static void Build(BuildTarget target, string fallbackOutputPath)
        {
            SeaOwnedAssetEditorLifecycle.PrepareForBuild();
            AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
            if (!string.IsNullOrEmpty(addressablesResult.Error))
            {
                throw new InvalidOperationException(
                    $"Addressables build failed: {addressablesResult.Error}");
            }

            PlayerSettings.runInBackground = true;
            if (target == BuildTarget.StandaloneOSX)
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
            }

            var outputPath = ReadOutputPath(fallbackOutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured for the build.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.StrictMode,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"{target} build failed: {report.summary.result}");
            }

            Debug.Log($"{target} build succeeded: {outputPath}");
        }

        private static string ReadOutputPath(string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == "-buildOutput")
                {
                    return args[index + 1];
                }
            }

            return fallback;
        }
    }
}
#endif
