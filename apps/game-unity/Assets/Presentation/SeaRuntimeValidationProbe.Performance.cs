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
        private float nextPresentationReportTime;

        private void ObservePresentationPerformance()
        {
            var requiredShipCount = Application.platform == RuntimePlatform.WebGLPlayer
                ? 100
                : 250;
            Application.targetFrameRate = 1_000;
            ReportPresentationProgress(requiredShipCount);
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

            var allocatedBefore = presentationWarmupFrames >= 180 ? AllocatedBytes() : 0;
            worldView.RunSyntheticPerformanceFrame();
            if (presentationWarmupFrames < 180)
            {
                presentationWarmupFrames++;
                presentationPoolCountAfterWarmup = worldView.SyntheticShipPoolCreatedCount;
                return;
            }

            RecordPresentationFrame(allocatedBefore, requiredShipCount);
        }

        // A probe that cannot fill its fleet reseeds it forever and says nothing while it does,
        // which is indistinguishable from a hung player. This says which of the two it is.
        private void ReportPresentationProgress(int requiredShipCount)
        {
            if (Time.unscaledTime < nextPresentationReportTime)
            {
                return;
            }

            nextPresentationReportTime = Time.unscaledTime + 5f;
            Debug.Log(
                $"Sea presentation progress: required={requiredShipCount} " +
                $"visible={(worldView == null ? -1 : worldView.VisibleShipPresentationCount)} " +
                $"seeded={presentationFleetSeeded} warmup={presentationWarmupFrames} " +
                $"measured={presentationMeasuredFrames}",
                this);
        }

        // The browser runtime has no per-thread allocation counter: the icall behind
        // GC.GetAllocatedBytesForCurrentThread is not implemented there and throws, which cost
        // the WebGL probe its idle-bytes evidence entirely. It asks the collector how much of
        // the managed heap is in use instead — a coarser number that catches a frame leaving
        // bytes behind but not one that allocates and collects between two samples.
        private static long AllocatedBytes() =>
            Application.platform == RuntimePlatform.WebGLPlayer
                ? GC.GetTotalMemory(forceFullCollection: false)
                : GC.GetAllocatedBytesForCurrentThread();

        private void RecordPresentationFrame(long allocatedBefore, int requiredShipCount)
        {
            presentationFrameTimes[presentationMeasuredFrames] = Time.unscaledDeltaTime * 1_000f;
            presentationAllocatedBytes[presentationMeasuredFrames] =
                AllocatedBytes() - allocatedBefore;
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
