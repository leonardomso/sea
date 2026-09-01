using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaWorldGeometryFactory
    {
        public static GameObject CreateIsland(
            string name,
            Vector3 position,
            float radius,
            Material sand,
            Material rock,
            Material land)
        {
            ValidateRadius(radius);
            var root = new GameObject(name);
            root.transform.position = position;
            CreateLayer(root.transform, "Sand Shore", radius * 2.15f, 0.22f, 0f, sand);
            CreateLayer(root.transform, "Rock Shelf", radius * 1.82f, 0.58f, 0.24f, rock);
            CreateLayer(root.transform, "Island Crown", radius * 1.48f, 0.72f, 0.78f, land);
            for (var index = 0; index < 9; index++)
            {
                var angle = index * 2.399963f;
                var distance = radius * (0.16f + index % 4 * 0.09f);
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = $"Canopy {index + 1}";
                canopy.transform.SetParent(root.transform, false);
                canopy.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * distance,
                    1.7f + index % 3 * 0.24f,
                    Mathf.Sin(angle) * distance);
                var canopyScale = radius * (0.16f + index % 2 * 0.035f);
                canopy.transform.localScale = new Vector3(
                    canopyScale * 1.35f,
                    canopyScale * 0.58f,
                    canopyScale);
                canopy.transform.localRotation = Quaternion.Euler(0f, index * 41f, 0f);
                PreparePrimitive(canopy, land);
            }

            return root;
        }

        public static GameObject CreateReef(
            string name,
            Vector3 position,
            float radius,
            Material shallows,
            Material rock)
        {
            ValidateRadius(radius);
            var root = new GameObject(name);
            root.transform.position = position;
            CreateLayer(root.transform, "Reef Shallows", radius * 2.3f, 0.08f, -0.08f, shallows);
            for (var index = 0; index < 5; index++)
            {
                var angle = index * Mathf.PI * 0.4f + 0.35f;
                var distance = radius * (0.22f + index % 2 * 0.2f);
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = $"Reef Rock {index + 1}";
                stone.transform.SetParent(root.transform, false);
                stone.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0.08f,
                    Mathf.Sin(angle) * distance);
                var stoneScale = radius * (0.24f + index % 3 * 0.045f);
                stone.transform.localScale = new Vector3(stoneScale, stoneScale * 0.32f, stoneScale);
                PreparePrimitive(stone, rock);
            }

            return root;
        }

        public static GameObject CreateHarbor(
            string name,
            Vector3 position,
            float radius,
            Material shallows,
            Material dock)
        {
            ValidateRadius(radius);
            var root = new GameObject(name);
            root.transform.position = position;
            CreateLayer(root.transform, "Harbor Waters", radius * 2f, 0.06f, -0.1f, shallows);
            for (var index = -1; index <= 1; index++)
            {
                var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pier.name = $"Pier {index + 2}";
                pier.transform.SetParent(root.transform, false);
                pier.transform.localPosition = new Vector3(index * 2.1f, 0.2f, 0f);
                pier.transform.localScale = new Vector3(1.2f, 0.35f, radius * 1.55f);
                PreparePrimitive(pier, dock);
            }

            return root;
        }

        public static GameObject CreateShoal(
            string name,
            Vector3 position,
            float radius,
            Material shallows)
        {
            ValidateRadius(radius);
            var root = new GameObject(name);
            root.transform.position = position;
            CreateLayer(root.transform, "Shoal Water", radius * 2f, 0.025f, -0.14f, shallows);
            for (var index = 0; index < 4; index++)
            {
                var ripple = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ripple.name = $"Shoal Ripple {index + 1}";
                ripple.transform.SetParent(root.transform, false);
                ripple.transform.localPosition = new Vector3(
                    Mathf.Cos(index * 1.7f) * radius * 0.35f,
                    -0.1f,
                    Mathf.Sin(index * 1.7f) * radius * 0.35f);
                ripple.transform.localScale = new Vector3(
                    radius * (0.32f + index * 0.04f),
                    0.012f,
                    radius * 0.09f);
                PreparePrimitive(ripple, shallows);
            }

            return root;
        }

        public static GameObject CreateStorm(
            string name,
            Vector3 position,
            float radius,
            Material cloudMaterial)
        {
            ValidateRadius(radius);
            var root = new GameObject(name);
            root.transform.position = position;
            for (var index = 0; index < 7; index++)
            {
                var angle = index * 2.399963f;
                var distance = radius * (0.12f + index % 3 * 0.12f);
                var cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cloud.name = $"Storm Cloud {index + 1}";
                cloud.transform.SetParent(root.transform, false);
                cloud.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * distance,
                    4.5f + index % 2 * 1.1f,
                    Mathf.Sin(angle) * distance);
                var size = radius * (0.32f + index % 3 * 0.045f);
                cloud.transform.localScale = new Vector3(size * 1.55f, size * 0.62f, size);
                PreparePrimitive(cloud, cloudMaterial);
            }

            return root;
        }

        private static void CreateLayer(
            Transform parent,
            string name,
            float diameter,
            float height,
            float y,
            Material material)
        {
            var layer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            layer.name = name;
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = new Vector3(0f, y, 0f);
            layer.transform.localScale = new Vector3(diameter, height, diameter);
            PreparePrimitive(layer, material);
        }

        private static void PreparePrimitive(GameObject primitive, Material material)
        {
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(collider);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }
        }

        private static void ValidateRadius(float radius)
        {
            if (!float.IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
        }
    }
}
