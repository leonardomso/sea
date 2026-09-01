using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaShipVisualFactory
    {
        private static Material fallbackMaterial;

        public static GameObject Create(
            GameObject modelPrefab,
            string name,
            float targetFootprint,
            Material authoredMaterial = null,
            float modelYawOffsetDegrees = 0f)
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
            var importedAxisRotation = visual.transform.localRotation;
            visual.transform.localRotation =
                Quaternion.Euler(0f, modelYawOffsetDegrees, 0f) * importedAxisRotation;
            if (authoredMaterial != null)
            {
                ApplyMaterial(visual, authoredMaterial);
            }
            else
            {
                ApplyFallbackMaterialIfNeeded(visual);
            }

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

        public static GameObject CreateMediumLod(
            string name,
            float targetFootprint,
            Material material = null)
        {
            ValidateFootprint(targetFootprint);
            var root = new GameObject(name);
            CreatePrimitive(
                root.transform,
                "Medium Hull",
                new Vector3(targetFootprint * 0.34f, targetFootprint * 0.12f, targetFootprint),
                new Vector3(0f, targetFootprint * 0.06f, 0f),
                material);
            CreatePrimitive(
                root.transform,
                "Medium Deck",
                new Vector3(targetFootprint * 0.42f, targetFootprint * 0.10f, targetFootprint * 0.48f),
                new Vector3(0f, targetFootprint * 0.16f, -targetFootprint * 0.04f),
                material);
            CreatePrimitive(
                root.transform,
                "Medium Sail",
                new Vector3(targetFootprint * 0.58f, targetFootprint * 0.34f, targetFootprint * 0.035f),
                new Vector3(0f, targetFootprint * 0.37f, 0f),
                material);
            return root;
        }

        public static GameObject CreateDistantLod(
            string name,
            float targetFootprint,
            Material material = null)
        {
            ValidateFootprint(targetFootprint);
            var root = new GameObject(name);
            CreatePrimitive(
                root.transform,
                "Distant Hull",
                new Vector3(targetFootprint * 0.28f, targetFootprint * 0.09f, targetFootprint),
                new Vector3(0f, targetFootprint * 0.045f, 0f),
                material);
            return root;
        }

        private static void ApplyMaterial(GameObject visual, Material material)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void CreatePrimitive(
            Transform parent,
            string name,
            Vector3 scale,
            Vector3 position,
            Material material)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localScale = scale;
            primitive.transform.localPosition = position;
            if (material != null)
            {
                primitive.GetComponent<Renderer>().sharedMaterial = material;
            }

            var collider = primitive.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(collider);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void ValidateFootprint(float targetFootprint)
        {
            if (!float.IsFinite(targetFootprint) || targetFootprint <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(targetFootprint));
            }
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

            ApplyMaterial(visual, fallbackMaterial);
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
