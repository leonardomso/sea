using System;
using UnityEngine;

namespace Sea.Client
{
    /// <summary>
    /// Renderer-only primitives built from Unity's built-in meshes. GameObject.CreatePrimitive
    /// always attaches a collider, which the browser build strips together with the physics
    /// module, so every call there logged an error before the collider was destroyed again.
    /// </summary>
    public static class SeaPrimitive
    {
        public static GameObject Create(PrimitiveType type, string name, Material material)
        {
            var primitive = new GameObject(name);
            primitive.AddComponent<MeshFilter>().sharedMesh = BuiltinMesh(type);
            primitive.AddComponent<MeshRenderer>().sharedMaterial = material;
            return primitive;
        }

        public static Mesh BuiltinMesh(PrimitiveType type)
        {
            var path = type switch
            {
                PrimitiveType.Sphere => "New-Sphere.fbx",
                PrimitiveType.Capsule => "New-Capsule.fbx",
                PrimitiveType.Cylinder => "New-Cylinder.fbx",
                PrimitiveType.Cube => "Cube.fbx",
                PrimitiveType.Plane => "New-Plane.fbx",
                PrimitiveType.Quad => "Quad.fbx",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
            var mesh = Resources.GetBuiltinResource<Mesh>(path);
            if (mesh == null)
            {
                throw new InvalidOperationException($"Built-in mesh '{path}' is unavailable.");
            }

            return mesh;
        }
    }
}
