# M5e Lighting & Atmosphere Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the festival into a night venue — a gradient sky with stars and a moon that darkens from dusk to full night across sets, working fog, spot-lit accents, and stylised light-beam cones over the pit whose brightness and sweep track hype.

**Architecture:** Fog support is added to the project's own shaders first (nothing else works without it). A new skybox shader and an additive cone shader carry the look. One `AtmosphereController` on the Systems object owns time-of-day: it subscribes to `SetStarted` on the EventBus, evaluates a serialized `AnimationCurve` over set number, and animates sky/fog/sun/ambient toward the new phase; per frame it reads `IHypeMeter.HypeFraction` and drives the beams. A small `LightBeam` component sweeps each cone and writes its intensity through a MaterialPropertyBlock. All scene changes are made by the two existing editor builders — no new menu items.

**Tech Stack:** Unity 6 LTS (`6000.4.11f1`), URP, HLSL, plain C# MonoBehaviours, `SerializedObject` editor wiring.

## Global Constraints

- **You cannot operate the Unity Editor.** Every Editor action (pressing Play, checking a look, adding a render feature) is a step the developer performs. Write the code and the exact instructions; never mark a visual checkpoint verified yourself.
- **No Domain changes in this milestone.** `Assets/PitTycoon/Domain/` is untouched; `dotnet test PitTycoon.Domain.slnx` stays at **98 passing** and is the regression gate for every commit.
- Domain layer rule still applies to anything new: plain C#, no UnityEngine references. Nothing in this plan belongs there.
- **One writer per property.** `VenueController.ApplyLighting` owns `accentLights[i].intensity`. `AtmosphereController` must never write accent-light intensity — doing so makes the paid Lighting upgrade invisible.
- **Never mutate a material asset at runtime.** Writes to `RenderSettings.skybox`'s material persist after Play mode exits. The controller instantiates a runtime copy.
- Editor builders stay idempotent and degrade gracefully: missing objects warn and skip (established M5b idiom).
- URP version in use is `com.unity.render-pipelines.universal@d3aed158d698`. `MixFogColor` in this version is internally guarded and returns the color untouched when fog is disabled, so `MixFog` is safe to call unconditionally.
- Commits: imperative present tense, **no `Co-Authored-By` trailer**.
- Branch: `feat/lighting-atmo-m5e` (already created off `master`, spec already committed).

## File Structure

- Modify `Assets/PitTycoon/Art/Shaders/ComicLit.shader` — standard URP fog in the `ForwardLit` pass.
- Modify `Assets/PitTycoon/Art/Shaders/Outline.shader` — depth-based edge fade (blit pass; no vertex fog possible).
- Create `Assets/PitTycoon/Art/Shaders/ComicSky.shader` — gradient sky, stars, moon.
- Create `Assets/PitTycoon/Art/Shaders/LightBeam.shader` — additive cone.
- Create `Assets/PitTycoon/Unity/LightBeam.cs` — per-cone sweep + intensity.
- Create `Assets/PitTycoon/Unity/AtmosphereController.cs` — the one owner of time-of-day and beam response.
- Modify `Assets/PitTycoon/Unity/GameBootstrap.cs` — optional wiring for the controller.
- Modify `Assets/PitTycoon/Unity/Editor/ComicLookSetup.cs` — sky material, fog, spot conversion, night sun, outline fade values.
- Modify `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs` — cone mesh + beam prefab + beam placement + controller wiring.
- Modify `SETUP.md` — M5e section.

---

### Task 1: Fog support in the project shaders

Nothing else in the milestone is visible until this lands. `RenderSettings.fog` currently has zero effect because every object renders with `ComicLit`, which never samples fog.

**Files:**
- Modify: `Assets/PitTycoon/Art/Shaders/ComicLit.shader`
- Modify: `Assets/PitTycoon/Art/Shaders/Outline.shader`
- Modify: `Assets/PitTycoon/Unity/Editor/ComicLookSetup.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `OutlineMat.mat` gains float properties `_FadeStart` and `_FadeEnd`. `RenderSettings.fog` is left enabled with `FogMode.ExponentialSquared`; Task 4's `AtmosphereController` drives `RenderSettings.fogColor` and `RenderSettings.fogDensity` from there.

- [ ] **Step 1: Add the fog pragma and varying to ComicLit's ForwardLit pass**

In `ComicLit.shader`, add the pragma after the existing `_CLUSTER_LIGHT_LOOP` line (line 27):

```hlsl
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
```

Replace the `Varyings` struct (line 44) to add a fog factor on the next free TEXCOORD:

```hlsl
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; half fogFactor:TEXCOORD3; };
```

Type `fogFactor` as `half` (not `float`) so that the `MixFog(color, IN.fogFactor)` call in the fragment stage matches the `(half3, half)` overload exactly: URP defines exactly two overloads, `MixFog(half3, half)` and `MixFog(float3, float)`, and a `half3` colour with a `float` factor matches neither.

- [ ] **Step 2: Fill the fog factor in the vertex stage**

In `vert`, add the assignment before `return OUT;`:

```hlsl
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
```

- [ ] **Step 3: Mix fog in the fragment stage**

In `frag`, replace the final two lines:

```hlsl
                color += _RimColor.rgb * (rim * _RimColor.a);

                return half4(color, baseCol.a);
```

with:

```hlsl
                color += _RimColor.rgb * (rim * _RimColor.a);

                color = MixFog(color, IN.fogFactor);
                return half4(color, baseCol.a);
```

- [ ] **Step 4: Add fade properties to the Outline shader**

`Outline.shader` is a full-screen blit pass — it has no vertex stage of its own, so `MixFog` cannot be used. It fades edge *strength* by depth instead. Add to `Properties` (after `_InkColor`, line 8):

```hlsl
        _InkColor("Ink Color", Color) = (0.07,0.06,0.09,1)
        _FadeStart("Edge Fade Start (m)", Float) = 30
        _FadeEnd("Edge Fade End (m)", Float) = 70
```

and declare them alongside the existing uniforms (after line 28):

```hlsl
            float4 _InkColor;
            float _FadeStart;
            float _FadeEnd;
```

- [ ] **Step 5: Attenuate the edge by nearest sampled depth**

In `Frag`, replace the `edge` line (line 47):

```hlsl
                float edge = saturate(dEdge * _DepthSensitivity + nEdge * _NormalSensitivity);
```

with:

```hlsl
                float edge = saturate(dEdge * _DepthSensitivity + nEdge * _NormalSensitivity);

                // Fade distant edges so outlines recede with the fog. Uses the NEAREST of the four
                // depth taps already sampled above, not the center pixel: at a silhouette against the
                // sky the center pixel is the far plane, and a center-depth fade would erase exactly
                // the outlines that matter most.
                float nearest = min(min(d0, d1), min(d2, d3));
                edge *= 1.0 - smoothstep(_FadeStart, _FadeEnd, nearest);
```

- [ ] **Step 6: Enable fog and seed the outline fade in the builder**

In `ComicLookSetup.cs`, change the `OutlineMat` creation line (line 54) from:

```csharp
            CreateMaterial($"{MatDir}/OutlineMat.mat", outlineShader, null);
```

to:

```csharp
            CreateMaterial($"{MatDir}/OutlineMat.mat", outlineShader, m =>
            {
                m.SetFloat("_FadeStart", 30f);
                m.SetFloat("_FadeEnd", 70f);
            });
```

Then add fog setup to the scene-wiring block, immediately after the `var cam = Camera.main;` lines (lines 75-76):

```csharp
            // Fog: on and exponential-squared. AtmosphereController drives color and density
            // per set; these are the dusk (set 1) values so the scene reads right without it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.42f, 0.34f, 0.36f);
            RenderSettings.fogDensity = 0.010f;
```

- [ ] **Step 7: Verify the Domain suite is untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 98`

- [ ] **Step 8: Developer Editor checkpoint**

Hand these steps to the developer — do not mark them done yourself:

1. Let Unity recompile; confirm the Console has no shader errors for `PitTycoon/ComicLit` or `PitTycoon/Outline`.
2. Run `Pit Tycoon → Apply Comic Look (M2a)`.
3. Press Play. Expected: distant crowd members and structures visibly wash toward the fog color, and their outlines weaken with distance instead of staying crisp to the horizon.
4. Sanity check the guard: in Lighting settings, untick Fog. Outlines must still draw at full strength (the fade uses explicit distances, not fog keywords, precisely so this cannot black-hole the outline pass). Re-tick Fog.

- [ ] **Step 9: Commit**

```bash
git add Assets/PitTycoon/Art/Shaders/ComicLit.shader Assets/PitTycoon/Art/Shaders/Outline.shader Assets/PitTycoon/Unity/Editor/ComicLookSetup.cs
git commit -m "feat(art): add fog to ComicLit and distance fade to Outline"
```

---

### Task 2: Night sky and night lighting

**Files:**
- Create: `Assets/PitTycoon/Art/Shaders/ComicSky.shader`
- Modify: `Assets/PitTycoon/Unity/Editor/ComicLookSetup.cs`

**Interfaces:**
- Consumes: Task 1's fog settings in `ComicLookSetup`.
- Produces: shader `"PitTycoon/ComicSky"` with float/color properties `_HorizonColor`, `_ZenithColor`, `_GradientPower`, `_StarStrength`, `_StarDensity`, `_MoonDir`, `_MoonSize`, `_MoonColor`; material asset at `Assets/PitTycoon/Art/Materials/ComicSky.mat` assigned to `RenderSettings.skybox`. Task 4's `AtmosphereController` writes `_HorizonColor`, `_ZenithColor`, and `_StarStrength` on a runtime copy of that material.

- [ ] **Step 1: Write the sky shader**

Create `Assets/PitTycoon/Art/Shaders/ComicSky.shader`:

```hlsl
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
```

- [ ] **Step 2: Create the sky material and assign it in the builder**

In `ComicLookSetup.cs`, add the shader lookup next to the existing ones (after line 43):

```csharp
            var outlineShader = Shader.Find("PitTycoon/Outline");
            var skyShader = Shader.Find("PitTycoon/ComicSky");
```

Add the material creation after the `OutlineMat` line from Task 1:

```csharp
            Material skyMat = null;
            if (skyShader != null)
            {
                skyMat = CreateMaterial($"{MatDir}/ComicSky.mat", skyShader, m =>
                {
                    m.SetColor("_HorizonColor", DuskHorizon);
                    m.SetColor("_ZenithColor", DuskZenith);
                    m.SetFloat("_StarStrength", 0f);
                });
            }
            else Debug.LogWarning("ComicLookSetup: PitTycoon/ComicSky not found — sky left as camera clear color.");
```

Add the two palette colors alongside the existing ones (after line 30):

```csharp
        private static readonly Color DuskHorizon = new Color(0.98f, 0.55f, 0.28f);
        private static readonly Color DuskZenith = new Color(0.24f, 0.26f, 0.48f);
```

- [ ] **Step 3: Point the camera and ambient at the sky**

In `ComicLookSetup.cs`, replace the camera block (lines 75-76):

```csharp
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Sky; }
```

with:

```csharp
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = skyMat != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = Sky;
            }
            if (skyMat != null) RenderSettings.skybox = skyMat;

            // Flat ambient so one color drives it deterministically. Under the default Skybox
            // ambient mode the value is derived from the custom sky's spherical harmonics and
            // would need a DynamicGI.UpdateEnvironment() every time the sky changes.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.28f, 0.34f);
```

- [ ] **Step 4: Convert the accent lights to aimed spots**

In `ComicLookSetup.cs`, replace `EnsureAccentLight` (lines 159-166):

```csharp
        private static void EnsureAccentLight(string name, Color color, Vector3 pos)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = pos;
            var light = go.GetComponent<Light>(); if (light == null) light = go.AddComponent<Light>();
            light.type = LightType.Point; light.color = color; light.intensity = 3.5f; light.range = 22f;
        }
```

with:

```csharp
        /// <summary>Rig light: a spot aimed down at the pit, so the beam cones (M5e) have a
        /// direction to follow and the crowd gets pools of colour instead of flat point fill.</summary>
        private static void EnsureAccentLight(string name, Color color, Vector3 pos)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((PitCenter - pos).normalized, Vector3.up);
            var light = go.GetComponent<Light>(); if (light == null) light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = 8f;
            light.range = 34f;
            light.spotAngle = 46f;
            light.innerSpotAngle = 18f;
        }
```

and add the pit-centre constant next to the palette colors:

```csharp
        private static readonly Vector3 PitCenter = new Vector3(0f, 0.5f, 0f);
```

- [ ] **Step 5: Dim the sun to a moonlight fill**

In `ComicLookSetup.cs`, add to the scene-wiring block, just before `EnsureGlobalVolume(profile);` (line 78):

```csharp
            // The sun is now a cool fill, not a daylight key. AtmosphereController animates
            // these per set; these are the dusk values.
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                if (sun != null)
                {
                    sun.color = new Color(1f, 0.78f, 0.52f);
                    sun.intensity = 0.9f;
                }
            }
            else Debug.LogWarning("ComicLookSetup: 'Directional Light' not found — sun left unchanged.");
```

- [ ] **Step 6: Verify the Domain suite is untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 98`

- [ ] **Step 7: Developer Editor checkpoint**

1. Let Unity recompile; confirm no errors for `PitTycoon/ComicSky` in the Console.
2. Run `Pit Tycoon → Apply Comic Look (M2a)`.
3. Expected in Scene view: a warm orange-to-blue gradient sky (dusk), no stars yet (`_StarStrength` is 0), and a moon disc if the camera looks toward `_MoonDir`.
4. Select `Assets/PitTycoon/Art/Materials/ComicSky.mat` and drag `_StarStrength` to 1. Stars should appear above the horizon only, and none below. Set it back to 0.
5. The three accent lights should now be cones aimed at the pit, and the overall scene should read as evening rather than midday.

- [ ] **Step 8: Commit**

```bash
git add Assets/PitTycoon/Art/Shaders/ComicSky.shader Assets/PitTycoon/Unity/Editor/ComicLookSetup.cs
git commit -m "feat(art): add comic sky shader and night rig lighting"
```

---

### Task 3: Light-beam cones

**Files:**
- Create: `Assets/PitTycoon/Art/Shaders/LightBeam.shader`
- Create: `Assets/PitTycoon/Unity/LightBeam.cs`
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

**Interfaces:**
- Consumes: the aimed spot lights from Task 2.
- Produces: `public sealed class LightBeam : MonoBehaviour` in namespace `PitTycoon.Unity` with `public void SetIntensity(float intensity)` and `public void SetSweepScale(float scale)`. Task 4's `AtmosphereController` holds a `LightBeam[]` and calls both. Also produces the mesh asset `Assets/PitTycoon/Art/Models/BeamCone.asset` and the prefab `Assets/PitTycoon/Art/Prefabs/LightBeam.prefab`.

- [ ] **Step 1: Write the beam shader**

Create `Assets/PitTycoon/Art/Shaders/LightBeam.shader`:

```hlsl
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
```

- [ ] **Step 2: Write the LightBeam component**

Create `Assets/PitTycoon/Unity/LightBeam.cs`:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// One stylised light-shaft cone. Sweeps on a sine around its mount, takes its colour from
    /// the Light it hangs under so beam and lit pool always agree, and exposes an intensity that
    /// AtmosphereController drives from hype. All writes go through a MaterialPropertyBlock, so
    /// every beam shares one material with no instancing.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class LightBeam : MonoBehaviour
    {
        [Tooltip("Half-arc of the sweep, in degrees.")]
        [SerializeField] private float sweepDegrees = 20f;
        [Tooltip("Sweeps per second at scale 1.")]
        [SerializeField] private float sweepSpeed = 0.25f;
        [Tooltip("Offset into the sweep cycle (0..1) so beams never move in unison.")]
        [SerializeField] private float phaseOffset;
        [Tooltip("Axis the rig light swings about, in its own local space. Y yaws the beam across " +
                 "the pit; flip to (1,0,0) to nod it up and down instead.")]
        [SerializeField] private Vector3 sweepAxis = new Vector3(0f, 1f, 0f);

        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int IntensityProp = Shader.PropertyToID("_Intensity");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Transform _sweepTarget;
        private Light _owner;
        private float _baseLightIntensity = 1f;
        private Quaternion _baseRotation;
        private float _intensity = 1f;
        private float _speedScale = 1f;
        private float _sweepPhase;

        /// <summary>Sweep rate multiplier; 1 is the serialized base speed.</summary>
        public void SetSweepScale(float scale) => _speedScale = Mathf.Max(0f, scale);

        /// <summary>Beam brightness before the owning Light's own intensity is applied.</summary>
        public void SetIntensity(float intensity) => _intensity = Mathf.Max(0f, intensity);

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            // NOTE: this block is authoritative and is never re-read from the renderer.
            // Renderer.GetPropertyBlock(mpb) OVERWRITES mpb with the renderer's current block,
            // which would silently wipe the colour set below on the first Apply call.
            _mpb = new MaterialPropertyBlock();

            _owner = GetComponentInParent<Light>();
            if (_owner != null)
            {
                _mpb.SetColor(ColorProp, _owner.color);
                // Captured before any upgrade is applied, so the ratio below reads 1 at level 0.
                _baseLightIntensity = Mathf.Max(0.0001f, _owner.intensity);
            }

            // Sweep the LIGHT, not just the cone. The cone is a child, so it follows — and the
            // pool of light on the crowd travels with the shaft instead of sitting still while
            // the shaft slides off it.
            _sweepTarget = _owner != null ? _owner.transform : transform;
            _baseRotation = _sweepTarget.localRotation;
            Apply();
        }

        private void Update()
        {
            // Accumulate phase rather than scaling absolute time: AtmosphereController rewrites
            // _speedScale every frame from hype, and scaling Time.time would jump the sine's
            // argument by Time.time * delta-scale each time it changes — the cone would strobe.
            _sweepPhase += Time.deltaTime * sweepSpeed * _speedScale;
            float angle = Mathf.Sin((_sweepPhase + phaseOffset) * Mathf.PI * 2f) * sweepDegrees;
            _sweepTarget.localRotation = _baseRotation * Quaternion.AngleAxis(angle, sweepAxis);

            Apply();
        }

        private void Apply()
        {
            if (_renderer == null) return;
            // VenueController raises the Light's intensity per Lighting-upgrade level; the beam
            // tracks that ratio so the purchase is visible on the shaft, not only on the ground.
            // Read every frame rather than on purchase: nothing notifies us, and it is one float.
            float lightScale = _owner != null ? _owner.intensity / _baseLightIntensity : 1f;
            _mpb.SetFloat(IntensityProp, _intensity * lightScale);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
```

- [ ] **Step 3: Generate the cone mesh and beam prefab in the builder**

In `FestivalSceneSetup.cs`, add these methods before `EnsureFolder` (line 234):

```csharp
        private const string BeamMeshPath = "Assets/PitTycoon/Art/Models/BeamCone.asset";
        private const string BeamMatPath = "Assets/PitTycoon/Art/Materials/LightBeamMat.mat";
        private const string BeamPrefabPath = "Assets/PitTycoon/Art/Prefabs/LightBeam.prefab";

        /// <summary>Open-ended cone: apex at the origin, opening along -Y to radius/length.
        /// uv.y runs 0 at the apex to 1 at the open end, which the beam shader fades along.
        /// No caps — a capped cone reads as a solid object, not a shaft of light.</summary>
        private static Mesh BuildBeamCone(int segments, float radius, float length)
        {
            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(a) * radius, -length, Mathf.Sin(a) * radius);
                verts[i * 2] = Vector3.zero;      // apex, duplicated per segment for clean normals
                verts[i * 2 + 1] = ring;
                uvs[i * 2] = new Vector2(i / (float)segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segments, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1;
                int b1 = ((i + 1) % segments) * 2 + 1;
                int t = i * 3;
                // One triangle per segment: apex + this segment's ring vertex + the next one.
                // A second triangle would span two apex vertices that share the origin — zero area.
                tris[t] = a0; tris[t + 1] = a1; tris[t + 2] = b1;
            }

            var mesh = new Mesh { name = "BeamCone" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject EnsureBeamPrefab()
        {
            var beamShader = Shader.Find("PitTycoon/LightBeam");
            if (beamShader == null)
            {
                Debug.LogWarning("FestivalSceneSetup: PitTycoon/LightBeam shader missing — beams skipped.");
                return null;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
            if (mesh == null)
            {
                mesh = BuildBeamCone(24, 3.2f, 16f);
                AssetDatabase.CreateAsset(mesh, BeamMeshPath);
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(BeamMatPath);
            if (mat == null) { mat = new Material(beamShader); AssetDatabase.CreateAsset(mat, BeamMatPath); }
            mat.shader = beamShader;
            EditorUtility.SetDirty(mat);

            // Idempotent: an existing prefab is kept, not rebuilt. Re-running the builder must
            // not revert per-beam tuning (sweepAxis, sweepDegrees) the developer set on it.
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BeamPrefabPath);
            if (existing != null) return existing;

            var temp = new GameObject("LightBeam");
            temp.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            temp.AddComponent<LightBeam>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, BeamPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }
```

- [ ] **Step 4: Place one beam under each accent light**

In `FestivalSceneSetup.cs`, add this method next to the one above:

```csharp
        /// <summary>Hang a beam cone under each accent light, aligned to the light's aim.
        /// The prefab's cone opens along -Y, so a local -90 X rotation points it down the
        /// parent light's +Z (forward) axis. Phases are staggered so they never sweep in unison.</summary>
        private static LightBeam[] EnsureBeams()
        {
            var prefab = EnsureBeamPrefab();
            if (prefab == null) return new LightBeam[0];

            var beams = new System.Collections.Generic.List<LightBeam>();
            string[] lightNames = { "Accent Amber", "Accent Magenta", "Accent Cyan" };

            for (int i = 0; i < lightNames.Length; i++)
            {
                var lightGo = GameObject.Find(lightNames[i]);
                if (lightGo == null)
                {
                    Debug.LogWarning($"FestivalSceneSetup: '{lightNames[i]}' not found — beam skipped.");
                    continue;
                }

                var old = lightGo.transform.Find("LightBeam");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var beamGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                beamGo.name = "LightBeam";
                beamGo.transform.SetParent(lightGo.transform, false);
                beamGo.transform.localPosition = Vector3.zero;
                beamGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                var beam = beamGo.GetComponent<LightBeam>();
                var bso = new SerializedObject(beam);
                var phase = bso.FindProperty("phaseOffset");
                if (phase != null) phase.floatValue = i / (float)lightNames.Length;
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(beam);

                beams.Add(beam);
            }
            return beams.ToArray();
        }
```

Then call it from `BuildFestivalScene`, replacing the `WireBeatVfx(lit, stage);` line (line 109):

```csharp
            var beams = EnsureBeams();

            WireBeatVfx(lit, stage);
```

(`beams` is consumed by Task 4; until then the assignment is unused and the compiler will warn — that is expected and is resolved in the next task.)

- [ ] **Step 5: Re-aim the accent lights after they are reparented**

Found in review of Task 2, and it must land before the beams do. `ComicLookSetup.EnsureAccentLight` bakes each spot's rotation from its M2a authored position toward the pit. `FestivalSceneSetup.ReparentLight` then runs in the normal build order and **moves the light to a different position on the roof truss, overwriting only `transform.position`** — the rotation stays aimed from where the light used to be.

Measured for "Accent Amber": authored at `(-5, 6, 1)`, re-placed at `(-2.5, 4, 9)`, leaving the aim ~57° off target against a 23° spot half-angle. The cone misses the pit entirely, and the Task 3 beams inherit that aim because they align to their parent light.

While they were point lights this was invisible — position was all that mattered. Converting them to spots in Task 2 is what made rotation load-bearing.

In `FestivalSceneSetup.cs`, replace `ReparentLight`:

```csharp
        private static void ReparentLight(string name, Transform parent, Vector3 worldPos)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
        }
```

with:

```csharp
        /// <summary>Move a rig light onto the stage's light mount. Re-aims after moving: these
        /// are spots (M5e), so the rotation baked at the authored position in ComicLookSetup
        /// points from the wrong place once the light lands on the truss.</summary>
        private static void ReparentLight(string name, Transform parent, Vector3 worldPos)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.LookRotation((PitCenter - worldPos).normalized, Vector3.up);
        }
```

and add the pit-centre constant next to the other `FestivalSceneSetup` statics (it must match the value `ComicLookSetup` uses):

```csharp
        private static readonly Vector3 PitCenter = new Vector3(0f, 0.5f, 0f);
```

- [ ] **Step 6: Verify the Domain suite is untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 98`

- [ ] **Step 7: Developer Editor checkpoint**

1. Let Unity recompile; confirm no errors for `PitTycoon/LightBeam` or `LightBeam.cs`.
2. Run `Pit Tycoon → Build Festival Scene (M2b)`.
3. Expected: three glowing cones hanging from the accent lights, each tinted to its light's colour, angled down at the pit.
4. Confirm the *lit pools* land on the crowd too, not just the cones — that is the Step 5 re-aim working. If the pools sit behind or beside the pit, the rotation fix did not take.
5. Press Play. The cones should sweep slowly and out of phase with each other.
6. Fly the free-look camera through a cone. It must stay visible from inside (`Cull Off`) rather than vanishing.
7. Confirm the lit pool on the crowd sweeps *with* the shaft — `LightBeam` rotates the rig light itself, so both move together. If the sweep reads wrong (nodding up-and-down rather than yawing across the pit), set `sweepAxis` to `(1,0,0)` on the prefab; the axis is in the light's local space and is exposed for exactly this.

- [ ] **Step 8: Commit**

```bash
git add Assets/PitTycoon/Art/Shaders/LightBeam.shader Assets/PitTycoon/Unity/LightBeam.cs Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs
git commit -m "feat(art): add sweeping light-beam cones over the pit"
```

---

### Task 4: AtmosphereController

**Files:**
- Create: `Assets/PitTycoon/Unity/AtmosphereController.cs`
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

**Interfaces:**
- Consumes: `LightBeam.SetIntensity(float)` and `LightBeam.SetSweepScale(float)` from Task 3; the `ComicSky.mat` material from Task 2; `EventBus`, `SetStarted` (with `int SetNumber`, published 1-based), and `IHypeMeter.HypeFraction` from Domain.
- Produces: `public sealed class AtmosphereController : MonoBehaviour` with `public void Initialize(EventBus bus, IHypeMeter hype)`, wired by `GameBootstrap` as an optional reference.

- [ ] **Step 1: Write the controller**

Create `Assets/PitTycoon/Unity/AtmosphereController.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns time of day and the light rig's response to hype. The sky advances dusk -> night
    /// across sets (SetStarted.SetNumber is 1-based), animating over transitionSeconds so the
    /// change is visible rather than a pop. Within a set, hype drives the beam cones only.
    ///
    /// It deliberately does NOT touch accent-light intensity: VenueController owns that field
    /// for the Lighting upgrade, and a per-frame write here would make a paid upgrade invisible.
    /// </summary>
    public sealed class AtmosphereController : MonoBehaviour
    {
        [Header("Day phase")]
        [Tooltip("Maps set progress 0..1 to dusk->night phase 0..1.")]
        [SerializeField] private AnimationCurve dayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Set number at which full night is reached.")]
        [SerializeField] private int setsToNight = 4;
        [SerializeField] private float transitionSeconds = 2f;

        [Header("Sky")]
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Color duskHorizon = new Color(0.98f, 0.55f, 0.28f);
        [SerializeField] private Color duskZenith = new Color(0.24f, 0.26f, 0.48f);
        [SerializeField] private Color nightHorizon = new Color(0.14f, 0.11f, 0.26f);
        [SerializeField] private Color nightZenith = new Color(0.03f, 0.03f, 0.09f);

        [Header("Fog")]
        [SerializeField] private Color duskFog = new Color(0.42f, 0.34f, 0.36f);
        [SerializeField] private Color nightFog = new Color(0.07f, 0.06f, 0.12f);
        [SerializeField] private float duskFogDensity = 0.010f;
        [SerializeField] private float nightFogDensity = 0.020f;

        [Header("Ambient + sun")]
        [SerializeField] private Light sun;
        [SerializeField] private Color duskAmbient = new Color(0.30f, 0.28f, 0.34f);
        [SerializeField] private Color nightAmbient = new Color(0.10f, 0.10f, 0.16f);
        [SerializeField] private Color duskSunColor = new Color(1f, 0.78f, 0.52f);
        [SerializeField] private Color nightSunColor = new Color(0.50f, 0.58f, 0.85f);
        [SerializeField] private float duskSunIntensity = 0.9f;
        [SerializeField] private float nightSunIntensity = 0.25f;

        [Header("Beams (hype-driven)")]
        [SerializeField] private LightBeam[] beams;
        [SerializeField] private float beamIntensityLow = 0.35f;
        [SerializeField] private float beamIntensityHigh = 1.6f;
        [SerializeField] private float beamSweepLow = 0.5f;
        [SerializeField] private float beamSweepHigh = 2.4f;

        private static readonly int HorizonProp = Shader.PropertyToID("_HorizonColor");
        private static readonly int ZenithProp = Shader.PropertyToID("_ZenithColor");
        private static readonly int StarProp = Shader.PropertyToID("_StarStrength");

        private EventBus _bus;
        private IHypeMeter _hype;
        private Material _skyInstance;
        private float _phase;         // current, animated
        private float _phaseTarget;   // set by SetStarted

        public void Initialize(EventBus bus, IHypeMeter hype)
        {
            if (_bus != null) _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus = bus;
            if (_bus != null) _bus.Subscribe<SetStarted>(OnSetStarted);
            _hype = hype;
        }

        private void Awake()
        {
            // A runtime COPY: writing to the skybox material asset persists after Play mode
            // exits and would permanently leave the scene's sky at the last set's look.
            if (skyMaterial != null)
            {
                _skyInstance = new Material(skyMaterial);
                RenderSettings.skybox = _skyInstance;
            }
            ApplyPhase(0f);
        }

        private void OnDestroy()
        {
            if (_bus != null) _bus.Unsubscribe<SetStarted>(OnSetStarted);
            if (_skyInstance != null)
            {
                RenderSettings.skybox = skyMaterial;
                Destroy(_skyInstance);
            }
        }

        private void OnSetStarted(SetStarted e)
        {
            float progress = setsToNight > 0
                ? Mathf.Clamp01((e.SetNumber - 1) / (float)setsToNight)
                : 1f;
            _phaseTarget = Mathf.Clamp01(dayCurve.Evaluate(progress));
        }

        private void Update()
        {
            if (!Mathf.Approximately(_phase, _phaseTarget))
            {
                float step = transitionSeconds > 0f ? Time.deltaTime / transitionSeconds : 1f;
                _phase = Mathf.MoveTowards(_phase, _phaseTarget, step);
                ApplyPhase(_phase);
            }

            if (beams == null || beams.Length == 0) return;
            float h = _hype != null ? Mathf.Clamp01(_hype.HypeFraction) : 0f;
            float intensity = Mathf.Lerp(beamIntensityLow, beamIntensityHigh, h);
            float sweep = Mathf.Lerp(beamSweepLow, beamSweepHigh, h);
            for (int i = 0; i < beams.Length; i++)
            {
                if (beams[i] == null) continue;
                beams[i].SetIntensity(intensity);
                beams[i].SetSweepScale(sweep);
            }
        }

        private void ApplyPhase(float t)
        {
            if (_skyInstance != null)
            {
                _skyInstance.SetColor(HorizonProp, Color.Lerp(duskHorizon, nightHorizon, t));
                _skyInstance.SetColor(ZenithProp, Color.Lerp(duskZenith, nightZenith, t));
                _skyInstance.SetFloat(StarProp, t);
            }

            RenderSettings.fogColor = Color.Lerp(duskFog, nightFog, t);
            RenderSettings.fogDensity = Mathf.Lerp(duskFogDensity, nightFogDensity, t);
            RenderSettings.ambientLight = Color.Lerp(duskAmbient, nightAmbient, t);

            if (sun != null)
            {
                sun.color = Color.Lerp(duskSunColor, nightSunColor, t);
                sun.intensity = Mathf.Lerp(duskSunIntensity, nightSunIntensity, t);
            }
        }
    }
}
```

- [ ] **Step 2: Add the optional reference to GameBootstrap**

In `GameBootstrap.cs`, add the field after `freeLook` (line 30):

```csharp
        [SerializeField] private FreeLookController freeLook;
        [SerializeField] private AtmosphereController atmosphere;
```

Do **not** add it to the null-check on lines 38-40 — it is optional so scenes that have not been re-run through the builders keep working. Add the wiring call after `freeLook?.Initialize(Bus);` (line 56):

```csharp
            freeLook?.Initialize(Bus);
            atmosphere?.Initialize(Bus, hype);   // hype passed as IHypeMeter
```

- [ ] **Step 3: Wire the controller in the builder**

In `FestivalSceneSetup.cs`, add this method next to `WireBeatVfx`:

```csharp
        private static void WireAtmosphere(LightBeam[] beams)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            var ctrl = systems.GetComponent<AtmosphereController>();
            if (ctrl == null) ctrl = systems.AddComponent<AtmosphereController>();

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/ComicSky.mat");
            if (skyMat == null)
                Debug.LogWarning("FestivalSceneSetup: ComicSky.mat missing — run Apply Comic Look (M2a) first.");

            var sunGo = GameObject.Find("Directional Light");
            var sun = sunGo != null ? sunGo.GetComponent<Light>() : null;

            var aso = new SerializedObject(ctrl);
            SetRef(aso, "skyMaterial", skyMat);
            SetRef(aso, "sun", sun);
            // Only rewrite when we actually found beams: re-running this builder alone destroys
            // the MainStage rig (and the accent lights reparented onto it), so EnsureBeams can
            // legitimately return empty. Clearing the array there would silently drop the beams
            // with no obvious way back — the recovery is to re-run Apply Comic Look first.
            var arr = aso.FindProperty("beams");
            if (arr != null && beams.Length > 0)
            {
                arr.arraySize = beams.Length;
                for (int i = 0; i < beams.Length; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = beams[i];
            }
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);

            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot != null)
            {
                var bso = new SerializedObject(boot);
                SetRef(bso, "atmosphere", ctrl);
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(boot);
            }
        }
```

and call it from `BuildFestivalScene`, right after `WireVenue(stage, paLeft, paRight);` (line 110):

```csharp
            WireVenue(stage, paLeft, paRight);
            WireAtmosphere(beams);
```

This also consumes the `beams` local from Task 3, clearing the unused-variable warning.

- [ ] **Step 4: Verify the Domain suite is untouched**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 98`

- [ ] **Step 5: Developer Editor checkpoint**

1. Let Unity recompile; confirm no compile errors.
2. Run `Pit Tycoon → Build Festival Scene (M2b)`.
3. Select the `Systems` object and confirm `AtmosphereController` is present with `skyMaterial`, `sun`, and three `beams` assigned, and that `GameBootstrap`'s `atmosphere` field points at it.
4. Press Play. Set 1 should read as dusk. Play through to set 2, 3, 4: the sky must visibly darken over ~2 seconds at each set start, stars fade in, fog thickens and cools, and the sun drops toward a blue moonlight.
5. During a set, watch the beams as hype climbs: they should brighten and sweep faster, then settle back at the next set start.
6. Buy the **Lighting** upgrade. The accent lights must brighten and *stay* brighter — if they snap back, `AtmosphereController` is writing accent intensity and the one-writer rule has been broken.
7. Exit Play mode, then select `Assets/PitTycoon/Art/Materials/ComicSky.mat`. Its `_StarStrength` must still be **0** and its colours still the dusk values — proof the runtime copy worked and the asset was never mutated.

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/AtmosphereController.cs Assets/PitTycoon/Unity/GameBootstrap.cs Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs
git commit -m "feat: add atmosphere controller driving dusk-to-night and hype-reactive beams"
```

---

### Task 5: SETUP.md and full regression

**Files:**
- Modify: `SETUP.md`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: nothing consumed by later tasks — this is the closing task.

- [ ] **Step 1: Add the M5e section to SETUP.md**

Append a new section following the format of the existing per-milestone sections:

```markdown
## M5e — Lighting & Atmosphere

Turns the venue into a night festival: gradient sky with stars and a moon, working fog,
spot-lit accents, and sweeping light-beam cones that respond to hype.

### Build steps

1. `Pit Tycoon → Apply Comic Look (M2a)` — creates `ComicSky.mat`, assigns it as the skybox,
   switches the camera to Skybox clear, enables exponential-squared fog, converts the three
   accent lights to aimed spots, and dims the directional light to a cool fill.
2. `Pit Tycoon → Build Festival Scene (M2b)` — generates `BeamCone.asset` and
   `LightBeam.prefab`, hangs one beam under each accent light, and adds + wires
   `AtmosphereController` on `Systems`.

No manual Editor steps beyond running the two menu items. The Halftone/Outline render
features from M2a must already be on `PC_Renderer.asset` (unchanged by this milestone).

### Verification

- Distant crowd and structures wash toward the fog colour; their outlines weaken with distance.
- Set 1 reads as dusk. Each set start darkens the sky over ~2 seconds; stars fade in; by set 5
  (with the default `setsToNight = 4`) it is full night.
- Beams are visible from the default camera and from inside, and brighten + sweep faster as
  hype climbs.
- The Lighting upgrade still brightens the accent lights permanently.
- After exiting Play mode, `ComicSky.mat` still holds its dusk values (the controller edits a
  runtime copy, never the asset).

### Tuning knobs

- `AtmosphereController` on `Systems`: `dayCurve`, `setsToNight`, `transitionSeconds`, and every
  dusk/night colour and intensity pair. All live in Play mode.
- `beamIntensityLow/High` and `beamSweepLow/High` — the hype response.
- `LightBeam` on each beam: `sweepDegrees`, `sweepSpeed`, `phaseOffset`, `sweepAxis`
  (set `sweepAxis` to `(1,0,0)` for a vertical sweep).
- `ComicSky.mat`: `_GradientPower`, `_StarDensity`, `_MoonDir`, `_MoonSize`, `_MoonColor`.
- `LightBeamMat.mat`: `_EdgeSoftness`, `_LengthFade`.
- `OutlineMat.mat`: `_FadeStart`, `_FadeEnd` — the distance band over which outlines dissolve.
- `ComicLook.asset` volume profile: bloom, colour grading, and vignette will likely want
  re-balancing now that the scene is darker and the beams are additive.
```

- [ ] **Step 2: Run the Domain suite one final time**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 98`

- [ ] **Step 3: Developer full-regression checkpoint**

Full pass through the game, confirming M1-M5b still behave under the new lighting:

1. A set plays; the crowd fills, hype builds, abilities fire on-beat with their multipliers.
2. The intermission shop opens; upgrades and abilities are purchasable; ghost previews appear
   and are inert (no beam or pulse on a preview).
3. Build spots place structures; the crowd grows across sets.
4. Existing beat VFX — shockwaves, onomatopoeia, coin fly — still read clearly against the
   darker background. If they wash out, re-balance bloom threshold in `ComicLook.asset`.
5. Free-look camera behaves, including flying through a beam.
6. Frame rate holds at maximum crowd capacity with all three beams on screen.

- [ ] **Step 4: Commit**

```bash
git add SETUP.md
git commit -m "docs: M5e setup, verification, and tuning notes"
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| Fog in `ComicLit` via `multi_compile_fog` + `MixFog` | 1 |
| Outline depth fade with `_FadeStart`/`_FadeEnd`, nearest-tap | 1 |
| `_FadeStart`/`_FadeEnd` set once by the builder, not per frame | 1 (Step 6) |
| `ComicSky.shader` — gradient, stars, moon | 2 |
| Skybox assigned, camera `clearFlags = Skybox` | 2 |
| Fog enabled, exponential-squared | 1 (Step 6) |
| Accent lights Point → Spot, aimed at the pit | 2 |
| Sun dimmed to a cool fill | 2 |
| `AmbientMode.Flat` so ambient is one driven colour | 2 |
| `LightBeam.shader` — additive, `ZWrite Off`, `Cull Off`, rim + length fade | 3 |
| Cone mesh generated in C#, no Blender round-trip | 3 |
| `LightBeam.prefab` built and placed under each accent light | 3 |
| Per-beam sweep with phase offset | 3 |
| `AtmosphereController` — `SetStarted`, day curve, animated transition | 4 |
| Hype drives beam intensity + sweep speed | 4 |
| Accent intensity NOT written (VenueController owns it) | 4 (guard + checkpoint 6) |
| Runtime skybox material copy | 4 (guard + checkpoint 7) |
| `GameBootstrap` optional wiring | 4 |
| `SerializedObject` wiring in `FestivalSceneSetup` | 3, 4 |
| `SETUP.md` section | 5 |
| Domain untouched, 98 tests green | every task |

**Placeholders:** none — every code step carries the full text to write.

**Type consistency:** `LightBeam.SetIntensity(float)` and `LightBeam.SetSweepScale(float)` are defined in Task 3 Step 2 and called with those exact names in Task 4 Step 1. `AtmosphereController.Initialize(EventBus, IHypeMeter)` is defined in Task 4 Step 1 and called as `atmosphere?.Initialize(Bus, hype)` in Task 4 Step 2, where `hype` is a `HypeSystem` already passed as `IHypeMeter` elsewhere in `GameBootstrap`. Serialized field names used by `SetRef` (`skyMaterial`, `sun`, `beams`, `atmosphere`, `phaseOffset`) all match their declarations.

**Known non-blocking note:** Task 3 Step 4 introduces a `beams` local that is unused until Task 4 Step 3 consumes it. If the tasks are executed out of order, expect one unused-variable warning in between.
