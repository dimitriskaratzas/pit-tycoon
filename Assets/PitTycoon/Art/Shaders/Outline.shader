Shader "PitTycoon/Outline"
{
    Properties
    {
        _Thickness("Line Thickness (px)", Range(0.5,4)) = 1.4
        _DepthSensitivity("Depth Sensitivity", Float) = 0.4
        _NormalSensitivity("Normal Sensitivity", Float) = 2.0
        _InkColor("Ink Color", Color) = (0.07,0.06,0.09,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Thickness;
            float _DepthSensitivity;
            float _NormalSensitivity;
            float4 _InkColor;

            half4 Frag(Varyings input):SV_Target
            {
                float2 uv = input.texcoord;
                float2 o = (1.0 / _ScreenParams.xy) * _Thickness;

                float d0 = LinearEyeDepth(SampleSceneDepth(uv + float2(-o.x,-o.y)), _ZBufferParams);
                float d1 = LinearEyeDepth(SampleSceneDepth(uv + float2( o.x,-o.y)), _ZBufferParams);
                float d2 = LinearEyeDepth(SampleSceneDepth(uv + float2(-o.x, o.y)), _ZBufferParams);
                float d3 = LinearEyeDepth(SampleSceneDepth(uv + float2( o.x, o.y)), _ZBufferParams);
                float dEdge = abs(d0 - d3) + abs(d1 - d2);

                float3 n0 = SampleSceneNormals(uv + float2(-o.x,-o.y));
                float3 n1 = SampleSceneNormals(uv + float2( o.x,-o.y));
                float3 n2 = SampleSceneNormals(uv + float2(-o.x, o.y));
                float3 n3 = SampleSceneNormals(uv + float2( o.x, o.y));
                float nEdge = length(n0 - n3) + length(n1 - n2);

                float edge = saturate(dEdge * _DepthSensitivity + nEdge * _NormalSensitivity);
                float3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 outc = lerp(col, _InkColor.rgb, edge * _InkColor.a);
                return half4(outc, 1);
            }
            ENDHLSL
        }
    }
}
