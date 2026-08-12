Shader "PitTycoon/LightBeam"
{
    Properties
    {
        _Color("Color", Color) = (1,0.69,0.24,1)
        _Intensity("Intensity", Range(0,4)) = 1
        _EdgeSoftness("Edge Softness", Range(0.5,6)) = 2.5
        _LengthFade("Length Fade", Range(0.1,4)) = 1.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Beam"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _EdgeSoftness;
                float _LengthFade;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float2 uv:TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Inverse fresnel: brightest through the body of the cone facing us, dissolving
                // at the silhouette so the shaft reads as volume rather than as a solid surface.
                half edge = pow(saturate(abs(dot(N, V))), _EdgeSoftness);

                // uv.y is 0 at the emitter and 1 at the open end (set by the generated mesh),
                // so the beam dissolves into the air instead of ending in a hard rim.
                half lengthFade = pow(saturate(1.0 - IN.uv.y), _LengthFade);

                half a = edge * lengthFade * _Intensity;
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
