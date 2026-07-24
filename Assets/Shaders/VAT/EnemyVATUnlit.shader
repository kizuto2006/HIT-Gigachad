Shader "Gigachad/VAT/Enemy Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        [HideInInspector] _Metallic("Metallic", Range(0, 1)) = 0
        [HideInInspector] _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [NoScaleOffset] _VATPositionTex("VAT Position", 2D) = "black" {}
        [HideInInspector][NoScaleOffset] _VATNormalTex("VAT Normal", 2D) = "bump" {}
        _VATFrameCount("VAT Frame Count", Float) = 1
        _VATDuration("VAT Duration", Float) = 1
        [HideInInspector] _VATPhaseOffset("VAT Phase Offset", Float) = 0
        [HideInInspector] _VATPlaybackSpeed("VAT Playback Speed", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VATPositionTex);
            SAMPLER(sampler_VATPositionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _VATFrameCount;
                float _VATDuration;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(VATPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPhaseOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPlaybackSpeed)
            UNITY_INSTANCING_BUFFER_END(VATPerInstance)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 vatUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float speed = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPlaybackSpeed);
                float phase = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPhaseOffset);
                float frameCount = max(_VATFrameCount, 1.0);
                float duration = max(_VATDuration, 0.0001);
                float frame = frac((_Time.y * speed) / duration + phase) * frameCount;
                float frame0 = floor(frame);
                float frame1 = frame0 + 1.0;
                frame1 = frame1 >= frameCount ? 0.0 : frame1;
                float2 uv0 = float2(input.vatUV.x, (frame0 + 0.5) / frameCount);
                float2 uv1 = float2(input.vatUV.x, (frame1 + 0.5) / frameCount);
                float3 positionOS = lerp(
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv0, 0).xyz,
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv1, 0).xyz,
                    frac(frame));

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
