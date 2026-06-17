Shader "PitTycoon/ComicLit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.5,0.5,0.5,1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _RampTex("Lighting Ramp", 2D) = "white" {}
        _ShadowTint("Shadow Tint", Color) = (0.32,0.30,0.40,1)
        _RimColor("Rim Color", Color) = (1,1,1,0)
        _RimPower("Rim Power", Range(0.5,8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowTint;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half RampShade(half ndl)
            {
                half t = saturate(ndl * 0.5 + 0.5);
                return SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(t, 0.5)).r;
            }

            half4 frag(Varyings IN):SV_Target
            {
                float3 N = normalize(IN.normalWS);
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = dot(N, mainLight.direction);
                half ramp = RampShade(ndl) * mainLight.shadowAttenuation;
                half3 lit = lerp(_ShadowTint.rgb, mainLight.color, ramp);

                half3 add = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, IN.positionWS);
                    half andl = dot(N, l.direction);
                    half ar = RampShade(andl) * l.distanceAttenuation;
                    add += l.color * ar;
                LIGHT_LOOP_END
                #endif

                half3 ambient = SampleSH(N);
                half3 color = baseCol.rgb * (lit + add + ambient);

                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half rim = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);
                color += _RimColor.rgb * (rim * _RimColor.a);

                return half4(color, baseCol.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionHCS:SV_POSITION; };
            V ShadowVert(A IN)
            {
                V OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                float3 ns = TransformObjectToWorldNormal(IN.normalOS);
                float4 hcs = TransformWorldToHClip(ApplyShadowBias(ws, ns, _LightDirection));
                #if UNITY_REVERSED_Z
                hcs.z = min(hcs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                hcs.z = max(hcs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionHCS = hcs;
                return OUT;
            }
            half4 ShadowFrag(V IN):SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V DNVert(A IN)
            {
                V OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 DNFrag(V IN):SV_Target { return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0); }
            ENDHLSL
        }
    }
}
