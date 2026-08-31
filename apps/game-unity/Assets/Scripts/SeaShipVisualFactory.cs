using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaShipVisualFactory
    {
        private static Material fallbackMaterial;

        public static GameObject Create(GameObject modelPrefab, string name, float targetFootprint)
        {
            if (modelPrefab == null)
            {
                throw new ArgumentNullException(nameof(modelPrefab));
            }

            if (targetFootprint <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetFootprint));
            }

            var root = new GameObject(name);
            var visual = UnityEngine.Object.Instantiate(modelPrefab, root.transform, false);
            visual.name = "Visual";
            ApplyFallbackMaterialIfNeeded(visual);

            var initialBounds = CalculateRendererBounds(root);
            var initialFootprint = Mathf.Max(initialBounds.size.x, initialBounds.size.z);
            if (initialFootprint <= Mathf.Epsilon)
            {
                UnityEngine.Object.Destroy(root);
                throw new InvalidOperationException("The ship model has no measurable horizontal footprint.");
            }

            visual.transform.localScale *= targetFootprint / initialFootprint;
            var scaledBounds = CalculateRendererBounds(root);
            visual.transform.position -= new Vector3(
                scaledBounds.center.x,
                scaledBounds.min.y,
                scaledBounds.center.z);

            return root;
        }

        private static void ApplyFallbackMaterialIfNeeded(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (HasAuthoredColor(material))
                    {
                        return;
                    }
                }
            }

            if (fallbackMaterial == null)
            {
                fallbackMaterial = SeaMaterialFactory.Create(new Color(0.28f, 0.12f, 0.035f, 1f));
                fallbackMaterial.name = "Starter Ship Fallback";
            }

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = fallbackMaterial;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static bool HasAuthoredColor(Material material)
        {
            if (material == null)
            {
                return false;
            }

            var color = material.color;
            var channelRange = Mathf.Max(color.r, color.g, color.b) - Mathf.Min(color.r, color.g, color.b);
            return material.mainTexture != null || channelRange > 0.05f;
        }

        public static Bounds CalculateRendererBounds(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("The ship model does not contain renderable geometry.");
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }
    }
}
