using System;
using UnityEngine;

namespace Sea.Client
{
    public static class SeaMaterialFactory
    {
        private static readonly string[] ShaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "UI/Default",
        };

        public static Material Create(Color color)
        {
            foreach (var shaderName in ShaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    return new Material(shader) { color = color };
                }
            }

            throw new InvalidOperationException("No runtime-compatible shader is available for the sea world.");
        }
    }
}
