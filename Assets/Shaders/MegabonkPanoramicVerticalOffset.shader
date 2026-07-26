Shader "Gigachad/Skybox/Panoramic Vertical Offset"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        _VerticalOffset ("Vertical Offset", Range(-0.25, 0.25)) = 0
        [NoScaleOffset] _MainTex ("Panoramic Texture", 2D) = "grey" {}
        [HideInInspector] _Mapping ("Mapping", Float) = 1
        [HideInInspector] _ImageType ("Image Type", Float) = 0
        [HideInInspector] _MirrorOnBack ("Mirror On Back", Float) = 0
        [HideInInspector] _Layout ("Layout", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _VerticalOffset;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float sineValue;
                float cosineValue;
                sincos(radians, sineValue, cosineValue);
                float2x2 rotation = float2x2(cosineValue, -sineValue, sineValue, cosineValue);
                return float3(mul(rotation, vertex.xz), vertex.y).xzy;
            }

            float2 DirectionToPanoramaUV(float3 direction)
            {
                float3 normalizedDirection = normalize(direction);
                float latitude = acos(normalizedDirection.y);
                float longitude = atan2(normalizedDirection.z, normalizedDirection.x);
                float2 spherical = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - spherical;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = RotateAroundYInDegrees(input.vertex.xyz, _Rotation);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = DirectionToPanoramaUV(input.direction);
                uv.y = saturate(uv.y - _VerticalOffset);

                half3 color = tex2D(_MainTex, uv).rgb;
                color *= _Tint.rgb * unity_ColorSpaceDouble.rgb;
                color *= _Exposure;
                return half4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
