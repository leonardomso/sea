Shader "Sea/Chart Water"
{
    Properties
    {
        _DeepColor ("Deep Atlantic", Color) = (0.018, 0.16, 0.21, 1)
        _SurfaceColor ("Sea Glass", Color) = (0.04, 0.31, 0.36, 1)
        _FoamColor ("Foam", Color) = (0.68, 0.83, 0.78, 1)
        _WaveScale ("Wave Scale", Float) = 0.11
        _WaveSpeed ("Wave Speed", Float) = 0.22
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
            };

            fixed4 _DeepColor;
            fixed4 _SurfaceColor;
            fixed4 _FoamColor;
            float _WaveScale;
            float _WaveSpeed;

            v2f vert(appdata input)
            {
                v2f output;
                float4 world = mul(unity_ObjectToWorld, input.vertex);
                output.worldPosition = world.xz;
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float time = _Time.y * _WaveSpeed;
                float2 position = input.worldPosition * _WaveScale;
                float longSwell = sin(position.x * 0.72 + position.y * 0.36 + time);
                float crossSwell = sin(position.x * -0.31 + position.y * 1.08 - time * 1.37);
                float fineWake = sin(position.x * 2.15 + position.y * 1.72 + time * 2.1);
                float water = saturate(0.5 + longSwell * 0.18 + crossSwell * 0.13 + fineWake * 0.035);
                float foam = smoothstep(0.77, 0.94, water) * 0.18;
                fixed4 color = lerp(_DeepColor, _SurfaceColor, water);
                return lerp(color, _FoamColor, foam);
            }
            ENDCG
        }
    }
}
