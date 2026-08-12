Shader "PitTycoon/ComicSky"
{
    Properties
    {
        _HorizonColor("Horizon Color", Color) = (0.98,0.55,0.28,1)
        _ZenithColor("Zenith Color", Color) = (0.24,0.26,0.48,1)
        _GradientPower("Gradient Power", Range(0.2,4)) = 1.2
        _StarStrength("Star Strength", Range(0,1)) = 0
        _StarDensity("Star Density", Range(50,600)) = 220
        _MoonDir("Moon Direction", Vector) = (0.35,0.42,0.84,0)
        _MoonSize("Moon Size", Range(0.9,0.999)) = 0.985
        _MoonColor("Moon Color", Color) = (1,0.97,0.88,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Loose uniforms, not a UnityPerMaterial CBUFFER: skyboxes are drawn by the engine's
            // legacy skybox path rather than through the SRP Batcher (Unity's own skybox shaders
            // do the same), and there is no SRP Batcher benefit to preserve here.
            float4 _HorizonColor;
            float4 _ZenithColor;
            float _GradientPower;
            float _StarStrength;
            float _StarDensity;
            float4 _MoonDir;
            float _MoonSize;
            float4 _MoonColor;

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 dir:TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Unity draws the skybox on a mesh centred on the camera, so object-space
                // position IS the view direction.
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target
            {
                float3 d = normalize(IN.dir);

                float t = pow(saturate(d.y), _GradientPower);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // Stars: hash a coarse cell grid over the view direction, keep the sparse hits.
                // Faded out entirely at dusk by _StarStrength, and never below the horizon.
                float3 cell = floor(d * _StarDensity);
                float rnd = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float star = step(0.9985, rnd) * saturate(d.y * 3.0) * _StarStrength;
                col += star;

                // One stylised moon disc with a soft halo.
                float m = dot(d, normalize(_MoonDir.xyz));
                float disc = smoothstep(_MoonSize, _MoonSize + 0.004, m);
                float glow = pow(saturate(m), 220.0) * 0.35;
                col = lerp(col, _MoonColor.rgb, saturate(disc + glow) * _MoonColor.a);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
