Shader "Gigachad/FireballBillboard"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
        _Shape ("Shape (0 Disc, 1 Ring, 2 Spark)", Range(0, 2)) = 0
        _Softness ("Softness", Range(0.005, 0.4)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FireballAdditive"
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Intensity;
                half _Shape;
                half _Softness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centered);

                float disc = 1.0 - smoothstep(
                    0.34,
                    1.0,
                    distanceFromCenter);

                float ringOuter = 1.0 - smoothstep(
                    0.83,
                    0.83 + _Softness,
                    distanceFromCenter);
                float ringInner = smoothstep(
                    0.48 - _Softness,
                    0.48,
                    distanceFromCenter);
                float ring = ringOuter * ringInner;

                float sparkDistance = length(float2(
                    centered.x * 0.30,
                    centered.y));
                float spark = 1.0 - smoothstep(
                    0.2,
                    1.0,
                    sparkDistance);

                float discOrRing = lerp(disc, ring, saturate(_Shape));
                float mask = lerp(
                    discOrRing,
                    spark,
                    saturate(_Shape - 1.0));
                mask *= input.color.a;

                half3 color = input.color.rgb * (_Intensity * mask);
                return half4(color, mask);
            }
            ENDHLSL
        }
    }
}
