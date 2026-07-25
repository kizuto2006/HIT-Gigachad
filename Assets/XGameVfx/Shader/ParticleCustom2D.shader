Shader "Custom/2D/ParticleCustom"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Texture (R)", 2D) = "white" {}
        [Header(Blend Mode and Sorting)]
        [Enum(Alpha Blend, 10, Additive, 1)] _BlendMode ("Blend Mode", Float) = 10
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        
        [Header(Main Texture UV Animation)]
        _MainSpeed ("Main Speed (X,Y)", Vector) = (0,0,0,0)

        [Header(Mask Texture UV Animation)]
        _MaskSpeed ("Mask Speed (X,Y)", Vector) = (0,0,0,0)

        [Header(Dissolve Texture UV Animation)]
        _DissolveSpeed ("Dissolve Speed (X,Y)", Vector) = (0,0,0,0)

        [Header(Effects)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveSoftness ("Dissolve Softness", Range(0.0001, 1)) = 0.1

        [Header(Settings)]
        _Rotation ("Texture Rotation (Degrees)", Range(-360, 360)) = 0
        _RotationSpeed ("Rotation Speed (Deg/Life)", Float) = 0
        _Pivot ("Tiling Pivot (X,Y)", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane" 
        }
        
        Blend SrcAlpha [_BlendMode]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        Cull [_Cull]
        Lighting Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_particles
            
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                // Unity packs streams: if UV(xy) and Custom1.xy(zw) are added, they map to TEXCOORD0
                float4 texcoord : TEXCOORD0; 
                // x: AgePercent, y: Random
                float4 custom1 : TEXCOORD1; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 mainUV : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                float customData : TEXCOORD2; // Custom data for dissolve
                float2 dissolveUV : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;

            float4 _MainSpeed;
            float4 _MaskSpeed;
            float4 _DissolveSpeed;

            float _DissolveAmount;
            float _DissolveSoftness;
            float _Rotation;
            float _RotationSpeed;
            float4 _Pivot;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                
                float agePercent = v.custom1.x; // 0 -> 1 over lifetime

                // Custom1.x (from Custom Data curve) is packed into TEXCOORD0.z based on user's stream list
                o.customData = v.texcoord.z;

                // Calculate Tiling (Base from Material Inspector)
                float2 mainTiling = _MainTex_ST.xy;
                float2 maskTiling = _MaskTex_ST.xy;
                float2 dissolveTiling = _DissolveTex_ST.xy;

                // Calculate Offset (Base + Speed over Lifetime)
                float2 mainOffset = _MainTex_ST.zw + (_MainSpeed.xy * agePercent);
                float2 maskOffset = _MaskTex_ST.zw + (_MaskSpeed.xy * agePercent);
                float2 dissolveOffset = _DissolveTex_ST.zw + (_DissolveSpeed.xy * agePercent);

                // Scale UV from the custom Pivot
                float2 centeredUV = v.texcoord.xy - _Pivot.xy;

                // Apply Rotation
                float rad = radians(_Rotation + _RotationSpeed * agePercent);
                float s, c;
                sincos(rad, s, c);
                float2x2 rotMatrix = float2x2(c, -s, s, c);
                centeredUV = mul(rotMatrix, centeredUV);

                o.mainUV = (centeredUV * mainTiling) + _Pivot.xy + mainOffset;
                o.maskUV = (centeredUV * maskTiling) + _Pivot.xy + maskOffset;
                o.dissolveUV = (centeredUV * dissolveTiling) + _Pivot.xy + dissolveOffset;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample Mask Texture
                fixed4 maskColor = tex2D(_MaskTex, i.maskUV);

                // Sample Dissolve Texture
                fixed4 dissolveColor = tex2D(_DissolveTex, i.dissolveUV);

                // Sample Main Texture
                fixed4 mainColor = tex2D(_MainTex, i.mainUV);

                // Masking: Multiply main color with mask color
                mainColor *= maskColor;

                // Smooth Dissolve
                float totalDissolve = saturate(_DissolveAmount + i.customData);
                float t_remapped = lerp(-_DissolveSoftness, 1.0, totalDissolve);
                float dissolveMask = smoothstep(t_remapped, t_remapped + _DissolveSoftness, dissolveColor.r);
                
                mainColor *= dissolveMask;

                // Final color (Texture Color * Particle System Color)
                fixed4 col = mainColor * i.color;
                
                return col;
            }
            ENDCG
        }
    }
}
