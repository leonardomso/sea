using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaShipPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock properties;
        private Transform visual;
        private Renderer[] renderers;
        private Transform healthBar;
        private Renderer healthRenderer;

        public SeaShipFeedback Feedback { get; private set; }

        public ulong EntityId { get; private set; }

        public void Configure(Transform shipVisual, SeaShipFeedback feedback, Transform health)
        {
            properties = new MaterialPropertyBlock();
            visual = shipVisual;
            renderers = shipVisual.GetComponentsInChildren<Renderer>(true);
            Feedback = feedback;
            healthBar = health;
            healthRenderer = health.GetComponent<Renderer>();
        }

        public void Bind(ulong entityId, string presentationName)
        {
            EntityId = entityId;
            name = presentationName;
            Feedback.SetAnimationPhase(entityId * 0.37f);
            gameObject.SetActive(true);
        }

        public void Apply(
            uint hull,
            uint maximumHull,
            float speed,
            float maximumSpeed,
            byte factionCode,
            byte archetypeCode)
        {
            Feedback.SetMotion(speed, maximumSpeed);
            visual.gameObject.SetActive(true);
            healthBar.gameObject.SetActive(true);
            healthBar.localScale = new Vector3(
                4f * Mathf.Clamp01(maximumHull == 0 ? 0f : (float)hull / maximumHull),
                0.12f,
                0.12f);

            var isPlayerFaction = factionCode == 1;
            var color = isPlayerFaction
                ? new Color(0.25f, 0.72f, 1f, 1f)
                : new Color(0.28f, 0.95f, 0.45f, 1f);
            ApplyColor(healthRenderer, color);
            var variant = SeaShipVariantPolicy.Tint(factionCode, archetypeCode);
            ApplyColor(renderers, variant);
        }

        public void ResetForPool()
        {
            EntityId = 0;
            Feedback.ResetPresentation();
            visual.gameObject.SetActive(true);
            healthBar.gameObject.SetActive(false);
            healthRenderer.SetPropertyBlock(null);
            ClearColors(renderers);
            gameObject.SetActive(false);
        }

        private void ApplyColor(Renderer[] renderers, Color color)
        {
            foreach (var renderer in renderers)
            {
                ApplyColor(renderer, color);
            }
        }

        private static void ClearColors(Renderer[] renderers)
        {
            foreach (var renderer in renderers)
            {
                renderer.SetPropertyBlock(null);
            }
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            properties.Clear();
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            renderer.SetPropertyBlock(properties);
        }
    }
}
