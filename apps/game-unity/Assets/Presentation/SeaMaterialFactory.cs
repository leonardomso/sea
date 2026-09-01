using System;
using UnityEngine;
using UnityEngine.Rendering;

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

        public static Material CreateTransparent(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("No transparent shader is available for the sea world.");
            }

            var material = new Material(shader) { color = color };
            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        public static Material CreateChartWater()
        {
            var shader = Shader.Find("Sea/Chart Water");
            if (shader == null)
            {
                return Create(new Color(0.025f, 0.22f, 0.28f, 1f));
            }

            return new Material(shader) { name = "Living Chart Water" };
        }
    }
}
