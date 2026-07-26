Shader "Gigachad/VAT/Enemy Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _OutlineColor("Outline Color", Color) = (0.025, 0.02, 0.015, 1)
        _OutlineWidth("Outline Width (Pixels)", Range(0, 4)) = 1.5
        [NoScaleOffset] _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [NoScaleOffset] _VATPositionTex("VAT Position", 2D) = "black" {}
        [NoScaleOffset] _VATNormalTex("VAT Normal", 2D) = "bump" {}
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
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VATPositionTex);
            SAMPLER(sampler_VATPositionTex);
            TEXTURE2D(_VATNormalTex);
            SAMPLER(sampler_VATNormalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Metallic;
                half _Smoothness;
                float _VATFrameCount;
                float _VATDuration;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(VATPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPhaseOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPlaybackSpeed)
            UNITY_INSTANCING_BUFFER_END(VATPerInstance)

            struct OutlineAttributes
            {
                float3 positionOS : POSITION;
                float2 vatUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            void SampleOutlineVAT(float vertexU, out float3 positionOS, out float3 normalOS)
            {
                float speed = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPlaybackSpeed);
                float phase = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPhaseOffset);
                float frameCount = max(_VATFrameCount, 1.0);
                float duration = max(_VATDuration, 0.0001);
                float frame = frac((_Time.y * speed) / duration + phase) * frameCount;
                float frame0 = floor(frame);
                float frame1 = frame0 + 1.0;
                frame1 = frame1 >= frameCount ? 0.0 : frame1;
                float blend = frac(frame);
                float2 uv0 = float2(vertexU, (frame0 + 0.5) / frameCount);
                float2 uv1 = float2(vertexU, (frame1 + 0.5) / frameCount);
                positionOS = lerp(
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv0, 0).xyz,
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv1, 0).xyz,
                    blend);
                normalOS = normalize(lerp(
                    SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv0, 0).xyz,
                    SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv1, 0).xyz,
                    blend));
            }

            OutlineVaryings OutlineVert(OutlineAttributes input)
            {
                OutlineVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionOS;
                float3 normalOS;
                SampleOutlineVAT(input.vatUV.x, positionOS, normalOS);

                float4 positionCS = TransformObjectToHClip(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
                float2 outlineDirection = normalVS.xy;
                outlineDirection /= max(length(outlineDirection), 0.0001);
                float2 pixelOffset = outlineDirection * (2.0 * _OutlineWidth / _ScreenParams.xy);
                positionCS.xy += pixelOffset * positionCS.w;
                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(OutlineVaryings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), _OutlineColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_VATPositionTex);
            SAMPLER(sampler_VATPositionTex);
            TEXTURE2D(_VATNormalTex);
            SAMPLER(sampler_VATNormalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Metallic;
                half _Smoothness;
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
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 vatUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            void SampleVAT(float vertexU, out float3 positionOS, out float3 normalOS)
            {
                float speed = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPlaybackSpeed);
                float phase = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPhaseOffset);
                float frameCount = max(_VATFrameCount, 1.0);
                float duration = max(_VATDuration, 0.0001);
                float frame = frac((_Time.y * speed) / duration + phase) * frameCount;
                float frame0 = floor(frame);
                float frame1 = frame0 + 1.0;
                frame1 = frame1 >= frameCount ? 0.0 : frame1;
                float blend = frac(frame);

                float2 uv0 = float2(vertexU, (frame0 + 0.5) / frameCount);
                float2 uv1 = float2(vertexU, (frame1 + 0.5) / frameCount);
                float3 position0 = SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv0, 0).xyz;
                float3 position1 = SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv1, 0).xyz;
                float3 normal0 = SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv0, 0).xyz;
                float3 normal1 = SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv1, 0).xyz;

                positionOS = lerp(position0, position1, blend);
                normalOS = normalize(lerp(normal0, normal1, blend));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS;
                float3 normalOS;
                SampleVAT(input.vatUV.x, positionOS, normalOS);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 metallicGloss = SAMPLE_TEXTURE2D(
                    _MetallicGlossMap,
                    sampler_MetallicGlossMap,
                    input.uv);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallicGloss.r * _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = metallicGloss.a * _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.alpha = albedo.a;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_VATPositionTex);
            SAMPLER(sampler_VATPositionTex);
            TEXTURE2D(_VATNormalTex);
            SAMPLER(sampler_VATNormalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _OutlineColor;
                float _OutlineWidth;
                half _Metallic;
                half _Smoothness;
                float _VATFrameCount;
                float _VATDuration;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(VATPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPhaseOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _VATPlaybackSpeed)
            UNITY_INSTANCING_BUFFER_END(VATPerInstance)

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float3 positionOS : POSITION;
                float2 vatUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            void SampleShadowVAT(float vertexU, out float3 positionOS, out float3 normalOS)
            {
                float speed = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPlaybackSpeed);
                float phase = UNITY_ACCESS_INSTANCED_PROP(VATPerInstance, _VATPhaseOffset);
                float frameCount = max(_VATFrameCount, 1.0);
                float duration = max(_VATDuration, 0.0001);
                float frame = frac((_Time.y * speed) / duration + phase) * frameCount;
                float frame0 = floor(frame);
                float frame1 = frame0 + 1.0;
                frame1 = frame1 >= frameCount ? 0.0 : frame1;
                float blend = frac(frame);
                float2 uv0 = float2(vertexU, (frame0 + 0.5) / frameCount);
                float2 uv1 = float2(vertexU, (frame1 + 0.5) / frameCount);
                positionOS = lerp(
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv0, 0).xyz,
                    SAMPLE_TEXTURE2D_LOD(_VATPositionTex, sampler_VATPositionTex, uv1, 0).xyz,
                    blend);
                normalOS = normalize(lerp(
                    SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv0, 0).xyz,
                    SAMPLE_TEXTURE2D_LOD(_VATNormalTex, sampler_VATNormalTex, uv1, 0).xyz,
                    blend));
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS;
                float3 normalOS;
                SampleShadowVAT(input.vatUV.x, positionOS, normalOS);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
