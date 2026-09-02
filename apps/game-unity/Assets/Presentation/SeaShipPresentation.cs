using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaShipPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock properties;
        private Transform nearVisual;
        private GameObject mediumVisual;
        private GameObject distantSilhouette;
        private Renderer[] nearRenderers;
        private Renderer[] mediumRenderers;
        private Transform healthBar;
        private Renderer healthRenderer;
        private Renderer silhouetteRenderer;

        public SeaShipFeedback Feedback { get; private set; }

        public ulong EntityId { get; private set; }

        public void Configure(
            Transform visual,
            GameObject medium,
            SeaShipFeedback feedback,
            Transform health,
            GameObject silhouette)
        {
            properties = new MaterialPropertyBlock();
            nearVisual = visual;
            mediumVisual = medium;
            nearRenderers = visual.GetComponentsInChildren<Renderer>(true);
            mediumRenderers = medium.GetComponentsInChildren<Renderer>(true);
            Feedback = feedback;
            healthBar = health;
            healthRenderer = health.GetComponent<Renderer>();
            distantSilhouette = silhouette;
            silhouetteRenderer = silhouette.GetComponentInChildren<Renderer>(true);
            mediumVisual.SetActive(false);
            distantSilhouette.SetActive(false);
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
            SeaPresentationLevel level,
            byte factionCode,
            byte archetypeCode)
        {
            Feedback.SetMotion(speed, maximumSpeed);
            nearVisual.gameObject.SetActive(level == SeaPresentationLevel.Near);
            mediumVisual.SetActive(level == SeaPresentationLevel.Medium);
            distantSilhouette.SetActive(level == SeaPresentationLevel.Distant);
            healthBar.gameObject.SetActive(level == SeaPresentationLevel.Near);
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
            ApplyColor(nearRenderers, variant);
            ApplyColor(mediumRenderers, variant);
            ApplyColor(silhouetteRenderer, variant);
        }

        public void ResetForPool()
        {
            EntityId = 0;
            Feedback.ResetPresentation();
            nearVisual.gameObject.SetActive(true);
            mediumVisual.SetActive(false);
            distantSilhouette.SetActive(false);
            healthBar.gameObject.SetActive(false);
            healthRenderer.SetPropertyBlock(null);
            silhouetteRenderer.SetPropertyBlock(null);
            ClearColors(nearRenderers);
            ClearColors(mediumRenderers);
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
