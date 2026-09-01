using UnityEngine;

namespace Sea.Client
{
    public sealed class SeaShipPresentation : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock properties;
        private Transform fullVisual;
        private GameObject distantSilhouette;
        private Transform healthBar;
        private Renderer healthRenderer;
        private Renderer silhouetteRenderer;

        public SeaShipFeedback Feedback { get; private set; }

        public ulong EntityId { get; private set; }

        public void Configure(
            Transform visual,
            SeaShipFeedback feedback,
            Transform health,
            GameObject silhouette)
        {
            properties = new MaterialPropertyBlock();
            fullVisual = visual;
            Feedback = feedback;
            healthBar = health;
            healthRenderer = health.GetComponent<Renderer>();
            distantSilhouette = silhouette;
            silhouetteRenderer = silhouette.GetComponent<Renderer>();
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
            bool isPlayerFaction)
        {
            Feedback.SetMotion(speed, maximumSpeed);
            var showFull = level is SeaPresentationLevel.Near or SeaPresentationLevel.Medium;
            fullVisual.gameObject.SetActive(showFull);
            distantSilhouette.SetActive(level == SeaPresentationLevel.Distant);
            healthBar.gameObject.SetActive(level == SeaPresentationLevel.Near);
            healthBar.localScale = new Vector3(
                4f * Mathf.Clamp01(maximumHull == 0 ? 0f : (float)hull / maximumHull),
                0.12f,
                0.12f);

            var color = isPlayerFaction
                ? new Color(0.25f, 0.72f, 1f, 1f)
                : new Color(0.28f, 0.95f, 0.45f, 1f);
            ApplyColor(healthRenderer, color);
            ApplyColor(silhouetteRenderer, isPlayerFaction
                ? new Color(0.15f, 0.55f, 0.85f, 1f)
                : new Color(0.30f, 0.34f, 0.36f, 1f));
        }

        public void ResetForPool()
        {
            EntityId = 0;
            Feedback.ResetPresentation();
            fullVisual.gameObject.SetActive(true);
            distantSilhouette.SetActive(false);
            healthBar.gameObject.SetActive(false);
            healthRenderer.SetPropertyBlock(null);
            silhouetteRenderer.SetPropertyBlock(null);
            gameObject.SetActive(false);
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
