using UnityEngine;

namespace Sea.Client
{
    public static class SeaTacticalPresentationRules
    {
        public static float ChannelProgress(
            ulong startedAtTick,
            ulong completesAtTick,
            ulong currentTick)
        {
            if (completesAtTick <= startedAtTick)
            {
                return 1f;
            }

            if (currentTick <= startedAtTick)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                (float)(currentTick - startedAtTick) /
                (completesAtTick - startedAtTick));
        }
    }
}
