using System;
using UnityEngine;

namespace Sea.Client
{
    public sealed partial class SeaRuntimeValidationProbe
    {
        private readonly float[] presentationFrameTimes = new float[300];
        private readonly long[] presentationAllocatedBytes = new long[300];
        private bool presentationPerformanceEnabledForThisRun;
        private bool presentationFleetSeeded;
        private int presentationWarmupFrames;
        private int presentationMeasuredFrames;
        private int presentationPoolCountAfterWarmup;
        private string presentationEvidencePath;

        private void ObservePresentationPerformance()
        {
            var requiredShipCount = Application.platform == RuntimePlatform.WebGLPlayer
                ? 100
                : 250;
            Application.targetFrameRate = 1_000;
            if (worldView == null)
            {
                return;
            }

            if (!presentationFleetSeeded ||
                SeaRuntimeValidationRules.ShouldRestoreSyntheticFleet(
                    worldView.VisibleShipPresentationCount,
                    requiredShipCount))
            {
                worldView.SeedSyntheticPerformanceFleet(requiredShipCount);
                presentationFleetSeeded = true;
                presentationWarmupFrames = 0;
                presentationMeasuredFrames = 0;
                return;
            }

            var allocatedBefore = presentationWarmupFrames >= 180
                ? GC.GetAllocatedBytesForCurrentThread()
                : 0;
            worldView.RunSyntheticPerformanceFrame();
            if (presentationWarmupFrames < 180)
            {
                presentationWarmupFrames++;
                presentationPoolCountAfterWarmup = worldView.SyntheticShipPoolCreatedCount;
                return;
            }

            RecordPresentationFrame(allocatedBefore, requiredShipCount);
        }

        private void RecordPresentationFrame(long allocatedBefore, int requiredShipCount)
        {
            presentationFrameTimes[presentationMeasuredFrames] = Time.unscaledDeltaTime * 1_000f;
            presentationAllocatedBytes[presentationMeasuredFrames] =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            presentationMeasuredFrames++;
            if (presentationMeasuredFrames < presentationFrameTimes.Length)
            {
                return;
            }

            WritePresentationVerdict(requiredShipCount);
        }

        private void WritePresentationVerdict(int requiredShipCount)
        {
            Array.Sort(presentationFrameTimes);
            Array.Sort(presentationAllocatedBytes);
            var evidence = CreatePresentationEvidence();
            SeaEvidenceWriter.Write(presentationEvidencePath, evidence);
            var passed = evidence.MeetsBudget(requiredShipCount);
            Debug.Log(
                $"Sea presentation performance: visible={evidence.visibleShips}, " +
                $"frame-p95-ms={evidence.frameP95Milliseconds:F3}, " +
                $"frame-p99-ms={evidence.frameP99Milliseconds:F3}, " +
                $"idle-bytes={evidence.idleBytesPerFrame}, " +
                $"pools-stable={evidence.poolsStable}, passed={passed}.",
                this);
            presentationPerformanceEnabledForThisRun = false;
            Application.Quit(passed ? 0 : 3);
        }

        private SeaClientPerformanceEvidence CreatePresentationEvidence()
        {
            return new SeaClientPerformanceEvidence
            {
                platform = Application.platform.ToString(),
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                visibleShips = worldView.VisibleShipPresentationCount,
                frameP95Milliseconds = Percentile(presentationFrameTimes, 0.95f),
                frameP99Milliseconds = Percentile(presentationFrameTimes, 0.99f),
                idleBytesPerFrame =
                    presentationAllocatedBytes[presentationAllocatedBytes.Length - 1],
                poolsStable = worldView.SyntheticShipPoolCreatedCount ==
                    presentationPoolCountAfterWarmup,
                runtimeErrors = validationRuntimeErrors,
                missingAssets = validationMissingAssets,
            };
        }

        private static float Percentile(float[] sortedSamples, float percentile)
        {
            var index = Mathf.CeilToInt(sortedSamples.Length * percentile) - 1;
            return sortedSamples[Mathf.Clamp(index, 0, sortedSamples.Length - 1)];
        }
    }
}
