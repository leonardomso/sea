using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaFrameRateController : MonoBehaviour
    {
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Apply(Application.isFocused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Apply(hasFocus);
        }

        private static void Apply(bool hasFocus)
        {
            Application.targetFrameRate = SeaFrameRatePolicy.TargetForFocus(hasFocus);
        }
    }
}
