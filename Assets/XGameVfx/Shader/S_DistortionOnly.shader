Shader "Custom/Distortion Only"
{
    Properties
    {
        [Header(Distortion Shape)]
        _MaskTex ("Distortion Mask (Shape/Alpha)", 2D) = "white" {}
        _OpacityBoost ("Mask Opacity Boost", Range(0.1, 10.0)) = 1.0
        
        [Header(Distortion Pattern)]
        _NoiseTex ("Distortion Pattern (Noise/Normal)", 2D) = "gray" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.1
        
        [Header(Animation Settings)]
        _MaskSpeed ("Mask Speed (X, Y)", Vector) = (0, 0, 0, 0)
        _NoiseSpeed ("Distortion Noise Speed (X, Y)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        // Xếp hàng render ở mục Transparent+100 để đảm bảo nó bóp méo tất cả các effect phía sau nó
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        LOD 200
        Cull Off
        ZWrite Off

        GrabPass { "_DistortionGrab" }

        CGPROGRAM
        // Sử dụng hệ thống Unlit để không bị ảnh hưởng bởi bóng tối
        #pragma surface surf NoLighting alpha:blend
        #pragma target 3.0

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten)
        {
            return fixed4(s.Albedo, s.Alpha);
        }

        sampler2D _MaskTex;
        sampler2D _NoiseTex;
        sampler2D _DistortionGrab;

        struct Input
        {
            float2 uv_MaskTex;
            float2 uv_NoiseTex;
            float4 color : COLOR; // Nhận màu và độ mờ từ Particle System hoặc Sprite Renderer
            float4 screenPos;
        };

        float _OpacityBoost;
        float _DistortionStrength;
        float4 _MaskSpeed;
        float4 _NoiseSpeed;

        void surf (Input IN, inout SurfaceOutput o)
        {
            // 1. Đọc Mask (có thể trượt nếu nhập số vào Mask Speed)
            float2 maskUV = IN.uv_MaskTex + _MaskSpeed.xy * _Time.y;
            fixed4 maskTex = tex2D(_MaskTex, maskUV);
            float maskLuminance = dot(maskTex.rgb, float3(0.299, 0.587, 0.114));
            float mask = saturate(max(maskTex.a, maskLuminance) * _OpacityBoost);

            // Kết hợp với Alpha từ hệ thống Particle (để có thể fade mờ theo thời gian)
            mask *= IN.color.a;

            // 2. Tính toán Họa tiết bóp méo di chuyển
            float2 noiseUV = IN.uv_NoiseTex + _NoiseSpeed.xy * _Time.y;
            float4 noiseTex = tex2D(_NoiseTex, noiseUV);
            
            // Giải mã ảnh noise (từ 0..1) sang vector bóp méo (từ -1..1)
            float2 distortion = (noiseTex.rg - 0.5) * 2.0;

            // 3. Tính tọa độ Khúc xạ nền (Refraction)
            float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
            
            // Càng ra gần rìa (mask tiến về 0) thì độ méo càng giảm về 0, giúp viền tàng hình mượt mà
            screenUV += distortion * _DistortionStrength * mask; 
            
            fixed4 bgColor = tex2D(_DistortionGrab, screenUV);

            // 4. Xuất màu ra màn hình
            o.Albedo = 0;
            // Chỉ trả về màu nền đã bị bóp méo (thuần túy khúc xạ, không thêm màu sắc gì khác)
            o.Emission = bgColor.rgb; 
            
            // Ép Alpha = 1.0 vì bản thân màu nền đã bao gồm cả các vùng không bị méo (rìa ngoài mask).
            o.Alpha = 1.0; 
        }
        ENDCG
    }
    FallBack "Transparent/VertexLit"
}
