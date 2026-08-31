Shader "Sea/Chart Fog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.015, 0.05, 0.065, 0.96)
        _PlayerPosition ("Player Position", Vector) = (0, 0, 0, 0)
        _VisionRadius ("Vision Radius", Float) = 44
        _FadeWidth ("Fade Width", Float) = 12
    }

    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

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
                float3 worldPosition : TEXCOORD0;
            };

            fixed4 _FogColor;
            float4 _PlayerPosition;
            float _VisionRadius;
            float _FadeWidth;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float distanceFromPlayer = distance(
                    input.worldPosition.xz,
                    _PlayerPosition.xy);
                float fog = smoothstep(
                    _VisionRadius,
                    _VisionRadius + max(_FadeWidth, 0.001),
                    distanceFromPlayer);
                return fixed4(_FogColor.rgb, _FogColor.a * fog);
            }
            ENDCG
        }
    }
}
