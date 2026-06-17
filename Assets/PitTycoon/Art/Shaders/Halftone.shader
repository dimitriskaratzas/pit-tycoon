Shader "PitTycoon/Halftone"
{
    Properties
    {
        _DotScale("Dot Scale (px)", Float) = 5
        _Threshold("Shadow Threshold", Range(0,1)) = 0.5
        _Softness("Edge Softness", Range(0.001,0.5)) = 0.06
        _InkColor("Ink Color", Color) = (0.08,0.06,0.10,0.85)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "Halftone"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _DotScale;
            float _Threshold;
            float _Softness;
            float4 _InkColor;

            half4 Frag(Varyings input):SV_Target
            {
                float2 uv = input.texcoord;
                float3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float d = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                bool isSky = d <= 0.0;
                #else
                bool isSky = d >= 1.0;
                #endif

                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float shade = saturate((_Threshold - lum) / max(_Threshold, 1e-3));

                float2 sp = uv * _ScreenParams.xy / max(_DotScale, 1.0);
                float2 cell = frac(sp) - 0.5;
                float dist = length(cell);
                float radius = shade * 0.5;
                float dotMask = 1.0 - smoothstep(radius - _Softness, radius + _Softness, dist);

                float ink = isSky ? 0.0 : dotMask * shade;
                float3 outc = lerp(col, _InkColor.rgb, ink * _InkColor.a);
                return half4(outc, 1);
            }
            ENDHLSL
        }
    }
}
