# Pit Tycoon — Lighting & Atmosphere (Milestone 5, sub-project E) Design

**Date:** 2026-08-11
**Milestone:** "The Fidelity Pass" — sub-project **M5e**. M5a crowd figures and M5b main-stage refresh shipped (PRs #18/#19). M5c beat-reactive parts is specced and planned but **not yet implemented**; M5e runs **before** it. Revised order: **M5e → M5c → M5d.** Beat-reactivity then lands on final lighting, and structure surface detail (M5d) gets art-directed under the real look rather than under a daylight sun.

## Summary

The festival currently renders under a daylight directional sun against a flat solid-color camera clear, with three point lights for accent and no atmosphere of any kind. Scene fog is disabled, and would do nothing if enabled because the project's own shaders never sample it. M5e turns the venue into a night festival: a gradient sky with stars and a moon that darkens from dusk to full night across sets, working fog, spot-lit accents, and stylised light-beam cones over the pit whose brightness and sweep track the hype meter.

No new models. No Domain changes. The visual payload is two new shaders, a fog addition to two existing ones, one controller, and wiring in the two existing editor builders.

## Design decisions (locked during brainstorming)

1. **Dusk → night across sets.** Set 1 plays at golden hour; the sky darkens each set until full night. The sky becomes a progress bar for the whole run, reinforcing the tycoon curve. Rejected: always-night (less progression) and dusk→night *within* one set (a moving target that fights the hype curve for attention).
2. **Sky is a gradient with stars and a moon** — a custom comic skybox shader, not a flat color and not a photographic HDRI. Cheap, fully art-directable, matches the cel/graphic-novel language. A distant treeline/skyline silhouette was considered and cut: it must read from every free-look angle, which is modelling work this pass does not need.
3. **Beams are cone meshes**, not screen-space light shafts. URP has no built-in volumetric fog; a stylised additive cone is cheaper, controllable, reads as comic-book light, and does not fight the existing halftone/outline full-screen passes. Rejected: fog + spots alone (no shaft in the air, empty sky over the stage) and a custom radial-blur render feature (a full-screen effect to write and debug, stacking onto two existing ones).
4. **Sky advances per set; hype drives the rig within a set.** Time of day is set-indexed, so it reads as progression. Beam brightness and sweep speed track `IHypeMeter.HypeFraction`, so the hype loop is visible on screen per the project rule. Accent-light intensity is deliberately excluded — `VenueController` owns it for the Lighting upgrade (see Runtime, below). Beat-level punch stays M5c's job. Rejected: hype-driven sky, which would make time of day flicker and destroy the dusk→night arc.
5. **The day curve is a serialized `AnimationCurve`, not a Domain class.** It is a lerp over set index; an Inspector curve is more art-directable than any C# equivalent, and it is tunable without a recompile. The Domain layer and its 98 tests are untouched by this milestone.

## Prerequisite: fog support in the project shaders

`Assets/PitTycoon/Art/Shaders/ComicLit.shader` and `Outline.shader` contain **no fog support** — no `multi_compile_fog` pragma, no fog coordinate, no `MixFog` call. Every object in the game (ground, crowd, structures) renders with ComicLit, so enabling `RenderSettings.fog` today changes nothing on screen.

The two shaders need different treatments, because they are different kinds of shader:

**`ComicLit.shader`** is a per-object forward shader, so it takes the standard URP fog path in its `ForwardLit` pass:

- `#pragma multi_compile_fog`.
- A `half fogFactor` varying on the next free `TEXCOORD` semantic, filled with `ComputeFogFactor(p.positionCS.z)` in the vertex stage. It must be `half`, not `float`: URP defines exactly two overloads, `MixFog(half3, half)` and `MixFog(float3, float)`, and the shader's `half3` colour with a `float` factor matches neither.
- `color = MixFog(color, IN.fogFactor)` before the fragment stage returns. `MixFogColor` internally checks whether any fog keyword is enabled and returns the color untouched when fog is off, so this is safe with fog disabled.

**`Outline.shader`** is a **full-screen blit pass** — it has no vertex stage of its own and no per-object geometry, so `MixFog` cannot apply. Fogging the ink color would also be wrong: the goal is for distant *edges to weaken*, not for distant ink to turn grey. Instead it gains two explicit float properties, `_FadeStart` and `_FadeEnd`, and attenuates the computed edge by linear eye depth:

```hlsl
float eye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
edge *= 1.0 - smoothstep(_FadeStart, _FadeEnd, eye);
```

This is deliberately explicit rather than reading `unity_FogParams`: URP's `ComputeFogIntensity` returns `0.0` when no fog keyword is active, which in a blit pass would silently erase every outline in the scene whenever fog is off.

The attenuation uses the **nearest** of the four depth taps the pass already samples, not the center pixel. At a silhouette against the sky the center pixel is the far plane, so a center-depth fade would erase exactly the outlines that matter most. The nearest tap keeps a close silhouette crisp against a distant background while still fading genuinely far geometry.

`_FadeStart`/`_FadeEnd` are set **once by the editor builder**, not driven per frame. `OutlineMat.mat` is referenced by the renderer's Full Screen Pass feature, so a runtime write would mutate the asset — the same trap as the skybox, without a clean runtime-copy escape. A static range roughly matched to the night fog is sufficient; the fade only has to read as depth, not track fog density exactly.

Outline is included deliberately: without it, silhouette edges stay at full contrast into the far distance while their fill fades, which reads as broken rather than stylised.

This is a prerequisite for everything else in the milestone and lands first.

## New shaders

**`Assets/PitTycoon/Art/Shaders/ComicSky.shader`** — a skybox shader. Properties:

- `_HorizonColor`, `_ZenithColor` — vertical gradient, interpolated on view-direction Y.
- `_StarStrength` (0..1) — procedural stars via a hash on the view direction, multiplied by this. 0 at dusk, 1 at full night.
- `_MoonDir` (direction vector), `_MoonSize`, `_MoonColor` — one stylised disc with a soft glow falloff.

All dusk→night motion is four property writes; the shader itself has no notion of time or sets.

**`Assets/PitTycoon/Art/Shaders/LightBeam.shader`** — unlit additive cone.

- `Blend One One`, `ZWrite Off`, `Cull Off` — `Cull Off` so the free-look camera can pass through a beam without it vanishing.
- Soft rim fade (falls off toward the cone's silhouette edge) and a fade along the cone's length so the beam dissolves rather than ending in a hard disc.
- `_Color`, `_Intensity`.
- Unlit and fog-free by design: a light shaft should not be fogged by the haze it represents.

## Runtime

**`Assets/PitTycoon/Unity/AtmosphereController.cs`** — one MonoBehaviour on the Systems object, mirroring `BeatVfxController`'s shape (`Initialize(EventBus, IHypeMeter)`, subscribes on the bus, tolerates missing references).

Serialized: `dayCurve` (AnimationCurve), `setsToNight` (int, default 4), dusk and night values for each driven property, `transitionSeconds` (default ~2), hype→beam response ranges.

- **On `SetStarted`:** read `e.SetNumber`, which `SetController` publishes 1-based (set 1 is the first of a run), and evaluate `phase = dayCurve.Evaluate(clamp01((e.SetNumber - 1) / (float)setsToNight))` — so set 1 is phase 0 = full dusk and set `setsToNight + 1` onward is phase 1 = full night. Then animate toward the phase-appropriate values over `transitionSeconds` so dusk visibly falls instead of popping between sets. Driven: skybox `_HorizonColor` / `_ZenithColor` / `_StarStrength`, `RenderSettings.fogColor` and `fogDensity`, the directional light's color and intensity, and `RenderSettings.ambientLight`. Ambient is switched to `AmbientMode.Flat` by the builder so a single color drives it deterministically — under the default Skybox ambient mode the value would come from the custom sky's spherical harmonics and need a `DynamicGI.UpdateEnvironment()` every set.
- **Per frame:** read `IHypeMeter.HypeFraction` and drive beam `_Intensity` and beam sweep speed between their serialized low and high values. A dim, slow, sleepy rig at low hype; a blazing sweeping light show at full.

**Accent-light intensity is deliberately NOT hype-driven.** `VenueController.ApplyLighting` already owns `accentLights[i].intensity`, writing `base + lightStep * level` when the Lighting upgrade is purchased. A per-frame write from `AtmosphereController` would overwrite it on the next frame and make a paid upgrade invisible. One writer per property: accent intensity stays upgrade-owned, and the hype read is carried entirely by the beams — which take their color from those same lights, so the rig still reads as one responding system.

**Skybox material trap (called out because it bites first-time Unity devs):** writing to `RenderSettings.skybox`'s material at runtime mutates the **asset**, and the change persists after exiting Play mode — the scene's sky would be permanently left at whatever the last set looked like. `AtmosphereController` instantiates a runtime copy of the material in `Awake` (`new Material(skyMaterial)`) and assigns that to `RenderSettings.skybox`, so the asset is never touched.

**`Assets/PitTycoon/Unity/LightBeam.cs`** — one per cone. Sweeps its transform on a sine (serialized arc degrees and base speed, per-beam phase offset so the three never sweep in unison), takes its color from the parent `Light` so beam and lit pool always agree, and writes `_Intensity` through a MaterialPropertyBlock (no material instancing).

`GameBootstrap` wires `AtmosphereController` the same way it wires `BeatVfxController`, and treats it as optional so scenes that have not been re-run through the builders keep running.

## Editor wiring — existing builders, no new menus

Following the M5c convention of extending the builders rather than adding menu items.

**`ComicLookSetup`** (`Pit Tycoon → Apply Comic Look`):

- Create `ComicSky.mat` from the new shader and assign it to `RenderSettings.skybox`.
- Set the main camera's `clearFlags` to `Skybox` (currently `SolidColor`).
- Enable fog: `RenderSettings.fog = true`, `fogMode = ExponentialSquared`, seeded with the dusk color and density.
- Convert the three accent lights from `LightType.Point` to `LightType.Spot`, with spot angle and range aimed down at the pit.
- Dim the directional light from its current daylight intensity to a cool moonlight fill.

**`FestivalSceneSetup`**:

- Generate the beam cone mesh procedurally in C# and save it as an asset — no Blender round-trip for a cone.
- Build `Assets/PitTycoon/Art/Prefabs/LightBeam.prefab` with the mesh, the LightBeam material, and the `LightBeam` component.
- Spawn three beams under the existing light mount, each aligned to one accent spot.
- Add `AtmosphereController` to Systems and wire its references via `SerializedObject`, the same mechanism already used for `BeatVfxController`.

Everything degrades gracefully in the established idiom: missing objects warn and skip.

**`SETUP.md`** gains an M5e section: build steps, verification checklist, tuning knobs.

## M5c handoff

`AtmosphereController` owns the **base** intensity of the beams. M5c's `BeatPulse` adds its envelope on top and re-captures its base when the envelope is idle — the rule already written into the M5c plan for exactly this kind of co-ownership. No conflict, and the beams give `BeatPulse` the largest reactive surface in the scene.

Property ownership after both milestones ship, so nothing is written twice: `VenueController` owns accent-light intensity (upgrade level), `AtmosphereController` owns the directional light, sky, fog, and beam base intensity (set index + hype), `BeatPulse` owns short-lived additive punches on top (beats).

## Testing & verification

- `dotnet test PitTycoon.Domain.slnx` — unchanged, stays at 98 passing. This milestone adds no Domain code.
- The work is inherently visual; correctness is an Editor checkpoint list:
  1. Fog visibly affects crowd and structures at distance, and outlines fade with their fills.
  2. Set 1 reads as golden-hour dusk; the sky darkens each set and reaches full night by set `setsToNight + 1`; stars fade in as it darkens; the transition animates rather than popping.
  3. Beams are visible from the default camera and from free-look angles, including flying through one.
  4. Beams brighten and sweep faster as hype rises within a set, and settle back at set start.
  5. The Lighting upgrade still visibly brightens the accent lights and holds — proof `AtmosphereController` is not overwriting `VenueController`.
  6. Exiting Play mode leaves the scene's sky asset unmodified (the runtime-copy check).
  7. M1–M5b regression intact: abilities, upgrades, build spots, ghost previews, and the existing beat VFX all still behave.

## Out of scope

- Crowd layout and per-member variation (separate pass: position jitter, material slots, accessories).
- Ground textures and surface detail; structure surface detail (M5d).
- Real volumetric fog, weather, or a day/night skybox with moving celestial bodies beyond the static moon.
- Beat-driven color snaps and beat-reactive geometry (M5c).
- Crowd shadows and shadow-quality work.

## Tuning knobs

- `dayCurve`, `setsToNight`, `transitionSeconds`, and every dusk/night color and intensity pair (Inspector, live).
- Per-beam sweep arc, base speed, and phase offset (Inspector, live).
- Hype→intensity response ranges for beams (Inspector, live).
- Fog density and color pairs; spot angle and range on the accent lights.
- Bloom, color grading, and vignette in the existing `ComicLook.asset` volume profile, which the new brighter night look will likely want re-balanced.
