using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaFrameRateController : MonoBehaviour
    {
        private void Awake()
        {
            Apply(Application.isFocused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Apply(hasFocus);
        }

        private static void Apply(bool hasFocus)
        {
            // Both are set together: a target without its sync, or the other way about,
            // is the judder this exists to remove. See SeaFrameRatePolicy for why.
            QualitySettings.vSyncCount = SeaFrameRatePolicy.VerticalSyncForFocus(hasFocus);
            Application.targetFrameRate = SeaFrameRatePolicy.TargetForFocus(hasFocus);
        }
    }
}
