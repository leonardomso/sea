using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaShipFeedback : MonoBehaviour
    {
        private Transform visual;
        private TrailRenderer[] wakes;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float phase;
        private float normalizedSpeed;
        private float volleyRecoil;

        public static bool ShouldEmitWake(float speed, float maximumSpeed) =>
            maximumSpeed > 0f && speed / maximumSpeed >= 0.04f;

        public void Configure(
            Transform modelVisual,
            Material wakeMaterial,
            Material shadowMaterial,
            float animationPhase)
        {
            visual = modelVisual;
            baseLocalPosition = visual.localPosition;
            baseLocalRotation = visual.localRotation;
            phase = animationPhase;
            if (wakes != null)
            {
                ResetPresentation();
                return;
            }

            wakes = new[]
            {
                CreateWake("Port Wake", -1.15f, wakeMaterial),
                CreateWake("Starboard Wake", 1.15f, wakeMaterial),
            };
            CreateWaterlineShadow(shadowMaterial);
        }

        public void SetAnimationPhase(float animationPhase) => phase = animationPhase;

        public void ResetPresentation()
        {
            normalizedSpeed = 0f;
            volleyRecoil = 0f;
            if (visual != null)
            {
                visual.localPosition = baseLocalPosition;
                visual.localRotation = baseLocalRotation;
            }

            if (wakes == null)
            {
                return;
            }

            foreach (var wake in wakes)
            {
                wake.emitting = false;
                wake.Clear();
            }
        }

        public void SetMotion(float speed, float maximumSpeed)
        {
            normalizedSpeed = maximumSpeed <= 0f ? 0f : Mathf.Clamp01(speed / maximumSpeed);
            var emit = ShouldEmitWake(speed, maximumSpeed);
            if (wakes == null)
            {
                return;
            }

            foreach (var wake in wakes)
            {
                wake.emitting = emit;
            }
        }

        /// <summary>
        /// The magazine bears in every direction, so the recoil kicks away from wherever the
        /// guns actually spoke: <paramref name="lateralBias"/> is the muzzle's local sideways
        /// component, +1 to starboard and -1 to port.
        /// </summary>
        public void PlayVolley(float lateralBias)
        {
            volleyRecoil = -Mathf.Clamp(lateralBias, -1f, 1f);
        }

        private void LateUpdate()
        {
            if (visual == null)
            {
                return;
            }

            var time = Time.time + phase;
            var heave = Mathf.Sin(time * 1.35f) * (0.06f + normalizedSpeed * 0.025f);
            var roll = Mathf.Sin(time * 1.05f) * (0.55f + normalizedSpeed * 0.45f);
            var pitch = Mathf.Sin(time * 1.62f + 0.8f) * (0.35f + normalizedSpeed * 0.25f);
            visual.localPosition = baseLocalPosition + Vector3.up * heave +
                Vector3.right * (volleyRecoil * 0.22f);
            visual.localRotation = baseLocalRotation * Quaternion.Euler(
                pitch,
                0f,
                roll + volleyRecoil * 2.6f);
            volleyRecoil = Mathf.MoveTowards(volleyRecoil, 0f, Time.deltaTime * 4.8f);
        }

        private TrailRenderer CreateWake(string name, float localX, Material material)
        {
            var wakeObject = new GameObject(name);
            wakeObject.transform.SetParent(transform, false);
            var localWaterHeight = SeaWorldView.WaterSurfaceHeight - SeaWorldView.ShipRootHeight;
            wakeObject.transform.localPosition = new Vector3(localX, localWaterHeight + 0.015f, -4.1f);
            var wake = wakeObject.AddComponent<TrailRenderer>();
            wake.sharedMaterial = material;
            wake.time = 2.2f;
            wake.minVertexDistance = 0.18f;
            wake.startWidth = 0.72f;
            wake.endWidth = 0f;
            wake.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            wake.alignment = LineAlignment.View;
            wake.textureMode = LineTextureMode.Stretch;
            wake.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wake.receiveShadows = false;
            wake.emitting = false;
            return wake;
        }

        private void CreateWaterlineShadow(Material material)
        {
            var shadow = SeaPrimitive.Create(PrimitiveType.Cylinder, "Waterline Contact", material);
            shadow.transform.SetParent(transform, false);
            var localWaterHeight = SeaWorldView.WaterSurfaceHeight - SeaWorldView.ShipRootHeight;
            shadow.transform.localPosition = new Vector3(0f, localWaterHeight + 0.008f, 0f);
            shadow.transform.localScale = new Vector3(3.2f, 0.008f, 7.8f);
        }
    }
}
