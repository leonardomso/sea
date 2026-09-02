using System;

namespace Sea.Client
{
    [Serializable]
    public sealed class SeaClientPerformanceEvidence
    {
        public int schemaVersion = 1;
        public string platform;
        public string recordedAtUtc;
        public int visibleShips;
        public float frameP95Milliseconds;
        public float frameP99Milliseconds;
        public long idleBytesPerFrame;
        public bool poolsStable;
        public int runtimeErrors;
        public int missingAssets;

        public bool MeetsBudget(int requiredVisibleShips)
        {
            return schemaVersion == 1 &&
                visibleShips >= requiredVisibleShips &&
                frameP95Milliseconds <= 16.7f &&
                frameP99Milliseconds <= 25f &&
                idleBytesPerFrame == 0 &&
                poolsStable &&
                runtimeErrors == 0 &&
                missingAssets == 0;
        }
    }
}
