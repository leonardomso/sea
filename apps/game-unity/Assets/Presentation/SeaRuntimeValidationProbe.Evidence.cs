using System;
using System.IO;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe
    {
        private int validationRuntimeErrors;
        private int validationMissingAssets;
        private string runtimeEvidencePath;
        private SeaRuntimeScenarioEvidence runtimeEvidence;

        private void ConfigureValidationEvidence()
        {
            presentationPerformanceEnabledForThisRun = HasArgument(
                "-seaPresentationPerformanceTest");
            var runtimeEnabled = enabledForThisRun || combatEnabledForThisRun ||
                progressionEnabledForThisRun || tacticalEnabledForThisRun;
            if (runtimeEnabled)
            {
                runtimeEvidencePath = CommandLineValue("-seaRuntimeEvidencePath") ??
                    Path.Combine(Application.persistentDataPath, "sea-runtime-evidence.json");
                runtimeEvidence = new SeaRuntimeScenarioEvidence
                {
                    movementRequired = enabledForThisRun,
                    combatRequired = combatEnabledForThisRun,
                    progressionRequired = progressionEnabledForThisRun,
                    tacticalRequired = tacticalEnabledForThisRun,
                };
            }

            if (presentationPerformanceEnabledForThisRun)
            {
                presentationEvidencePath = CommandLineValue("-seaPerformanceEvidencePath") ??
                    Path.Combine(Application.persistentDataPath, "sea-client-performance.json");
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 1_000;
            }

            if (runtimeEnabled || presentationPerformanceEnabledForThisRun)
            {
                Application.logMessageReceived += ObserveValidationLog;
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= ObserveValidationLog;
            ReleaseProgressionLootWatch();
        }

        private void MarkRuntimeMilestone(SeaRuntimeMilestone milestone)
        {
            if (runtimeEvidence == null)
            {
                return;
            }

            SetMilestone(runtimeEvidence, milestone);
            runtimeEvidence.recordedAtUtc = DateTime.UtcNow.ToString("O");
            runtimeEvidence.runtimeErrors = validationRuntimeErrors;
            SeaEvidenceWriter.Write(runtimeEvidencePath, runtimeEvidence);
        }

        private static void SetMilestone(
            SeaRuntimeScenarioEvidence evidence,
            SeaRuntimeMilestone milestone)
        {
            switch (milestone)
            {
                case SeaRuntimeMilestone.Movement:
                    evidence.movementObserved = true;
                    return;
                case SeaRuntimeMilestone.Combat:
                    evidence.combatObserved = true;
                    return;
                case SeaRuntimeMilestone.Progression:
                    evidence.progressionObserved = true;
                    return;
                case SeaRuntimeMilestone.Tactical:
                    evidence.tacticalObserved = true;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(milestone), milestone, null);
            }
        }

        private static bool HasArgument(string name)
        {
            return SeaRuntimeArguments.Has(
                name,
                Environment.GetCommandLineArgs(),
                Application.absoluteURL);
        }

        private static string CommandLineValue(string name)
        {
            return SeaRuntimeArguments.Value(
                name,
                Environment.GetCommandLineArgs(),
                Application.absoluteURL);
        }

        private void ObserveValidationLog(string condition, string _, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                validationRuntimeErrors++;
            }

            if (condition.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (condition.IndexOf("asset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    condition.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    condition.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                validationMissingAssets++;
            }
        }
    }
}
