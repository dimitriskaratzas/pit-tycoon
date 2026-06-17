# Pit Tycoon — Milestone 2a Design (Cel-Shading Pipeline over the Greybox)

**Date:** 2026-06-18
**Status:** Approved (design), pending spec review
**Scope:** M2a only — the rendering pipeline, validated on the existing M1 greybox primitives. Asset modelling (M2b) and the manga beat-VFX vocabulary (M2c) are explicitly out of scope.
**Source brief:** `pit-tycoon-claude-code-brief.md` (Art direction section)
**Predecessor:** M1 set loop (shipped, PR #3 merged).

## 1. Goal

Make the working M1 greybox scene *look like a mature graphic-novel comic* without changing gameplay. When M2a is done, you press Play on `Greybox.unity` and the same loop runs — but rendered with stepped cel lighting, heavy inked outlines, halftone-as-shadow, and glowing accent lights.

**Non-negotiable rule:** M2a must not regress the M1 loop. The set → hype → cash → bigger-crowd chain still plays exactly as before; only the rendering changes.

## 2. Art direction (locked)

**Mature graphic-novel comic, "color-pop" variant.** Borderlands is the *lineage* (cel-shaded but adult/gritty), not the destination — we are not copying it.

What "mature" means concretely (the three ingredients layered on top of cel shading):

1. **Inked, gritty linework** — heavy, slightly irregular outlines, not clean thin toon lines.
2. **Halftone / hatched shadows instead of smooth gradients** — the halftone pass *is* the shadow-rendering, which is where the graphic-novel grit lives.
3. **Weathered, desaturated base palette with bold accent pops** — concrete/rust/dust/denim base; saturation reserved for stage lights, signage, ability VFX. The "color-pop" variant lets the accent palette spread a bit wider than a strictly muted look so the pit feels *alive, not grim*.

**Reference palette** (base + accents, from the approved mock):

| Role | Hex |
|------|-----|
| Dusk sky | `#3E3A52` |
| Greyed mauve | `#6B5E6B` |
| Dust ochre haze | `#A8927A` |
| Pit floor | `#4A4038` |
| Ink / near-black | `#14111A` |
| Accent — amber | `#FFB13C` |
| Accent — magenta | `#E0457B` |
| Accent — cyan | `#36C9E0` |

These are authoring targets for materials/ramp/Volume, not hard-coded constants.

## 3. Environment & constraints

- **Unity 6 LTS (6000.0.x)**, **URP 17.4.0** (already installed).
- **PC-first.** Render features attach to `Assets/Settings/PC_Renderer.asset`. The Mobile tier (`Mobile_Renderer.asset`) is out of scope; we don't guarantee the passes are mobile-cheap.
- **HLSL, not Shader Graph.** Decision rationale: Shader Graph and Renderer Features are authored in Unity's GUI and don't version as reviewable text. Hand-written `.shader` files are authorable by Claude, committable, and code-reviewable — and the brief explicitly allows "custom HLSL shader." We wire the fullscreen HLSL shaders through URP's built-in **Full Screen Pass Renderer Feature** (it accepts a plain `Shader`, not only Shader Graph).
- **Delivery model = M1's.** Claude authors shader files + a ramp texture + materials + a Volume profile + an editor setup script, and lists every manual Editor step in `SETUP.md`. Claude **cannot** operate the Editor (attach features, assign materials, set Volume overrides interactively) — those are ordered `SETUP.md` steps, automated via the editor script wherever reasonable.
- Engineer is C#-fluent, first-time Unity. Explain URP render-pipeline concepts (render features, fullscreen passes, depth/normals textures, Volume overrides); don't explain C# basics.

## 4. The pipeline — four pieces, in render order

### 4.1 Cel / ramp lit shader (HLSL, URP Lit-style)
A custom `.shader` that lights opaque geometry with URP's main light + additional lights + shadows, then **posterizes `N·L` through a ramp texture** (a few hard steps) for the stepped comic look.
- Includes URP's `Lighting.hlsl` / `Core.hlsl`; uses `GetMainLight()` + `GetAdditionalLight()` and shadow attenuation.
- Per-material params: base color, ramp texture, optional rim term, optional spec step.
- Applied to the **authored** greybox materials (ground, crowd, stage primitives). The runtime-spawned VFX (whirlpool, coins) are created in code with default materials; restyling those is deferred to M2c (alongside the beat-VFX work) so M2a needs no gameplay-code change. They may render unshaded in M2a — acceptable.

### 4.2 Halftone pass (fullscreen HLSL)
Full Screen Pass Renderer Feature → custom fullscreen shader. Samples scene color, computes luminance, compares against a **screen-space dot pattern** so dots concentrate in **shadows/midtones** = halftone-as-shadow.
- Params: dot scale, luminance threshold, ink color, optional sky/UI mask.
- Injection point: after opaques (so it reads lit color), before the outline pass.

### 4.3 Outline pass (fullscreen HLSL)
Full Screen Pass Renderer Feature → custom fullscreen shader doing **Sobel edge-detect on `_CameraDepthTexture` + `_CameraNormalsTexture`**, drawing inked lines **on top of** the halftone.
- The feature's Requirements must request **Normal** (and Depth) so `_CameraNormalsTexture` is generated (DepthNormals prepass).
- Params: line thickness, ink color, depth sensitivity, normal sensitivity.
- Injection point: after the halftone pass (ink sits over the shadow dots).

### 4.4 Accent look (Volume + lights)
- Colored stage lights (amber/magenta/cyan spot/point) authored into the scene for the accent pops.
- A `ComicLook` Volume profile: Color Adjustments (push saturation/contrast for "color-pop"), Tonemapping, subtle **Bloom** so accents glow, light Vignette.
- This is the dial for "alive, not grim."

**Pass order summary:** opaque (cel ramp) → halftone (shadow dots) → outline (ink) → post (bloom/color via Volume).

## 5. Deliverables

- `Assets/PitTycoon/Art/Shaders/` — `ComicLit.shader`, `Halftone.shader`, `Outline.shader` (+ shared HLSL includes as needed).
- A ramp texture asset + the `ComicLook` Volume profile + materials for the existing greybox objects.
- Editor setup script (extends the M1 setup pattern): attaches the two Full Screen Pass features to `PC_Renderer`, creates/assigns materials and the ramp, assigns the Volume.
- `SETUP.md` section: ordered manual Editor steps for anything the script can't do, plus a tuning guide (ramp steps, dot scale/threshold, line thickness, bloom/saturation).
- **Likely zero new gameplay C#.** Runtime beat-reactive behaviour (pulsing bloom, shockwaves) is M2c. M2a is static authoring.

## 6. Verification (no TDD here — be honest about it)

Shaders/rendering are not unit-testable like the Domain layer, so M2a has **no automated tests**. "Done" is verified by:

1. **Visual match** — the greybox scene renders in the mature-comic, color-pop look (stepped lighting + inked outlines + halftone shadows + glowing accents), judged against the approved art mock.
2. **No gameplay regression** — the full M1 loop (set → ability → bank → intermission → bigger set) still plays.
3. **Performance** — holds a reasonable frame rate at the pulled-back camera (no hard FPS target for M2a; just "not janky").

Verification is a **manual in-editor checkpoint** (same model as the M1 bring-up steps): Claude delivers the files + `SETUP.md`, the user runs the setup, presses Play, and reports against a checklist.

## 7. Out of scope (explicit)

- **M2b** — Blender asset modelling/import (stage, truss, crowd figures, props) and replacing the greybox geometry.
- **M2c** — manga beat-VFX vocabulary (impact frames, speed lines, halftone shockwaves, onomatopoeia) and beat-reactive shader hooks.
- **Mobile tier** rendering.
- Any gameplay/economy/ability change.

## 8. Risks & open questions

- **DepthNormals availability** — the outline pass needs `_CameraNormalsTexture`; if the Full Screen Pass feature's Normal requirement doesn't populate it as expected in URP 17, fall back to a DepthNormals-prepass renderer feature. Validate early.
- **Pass-ordering correctness** — halftone vs outline vs transparents ordering may need iteration; the design assumes opaque-only treatment for M2a (the whirlpool/coin VFX are simple and can be opaque or accept the post as-is).
- **Halftone over sky/UI** — screen-space halftone may dot the sky or IMGUI HUD; may need a luminance floor or depth mask. Acceptable to tune in `SETUP.md`.
- **"Color-pop" calibration** — exact saturation spread is a visual dial; locked direction is "wider than the muted mock, still grit-based." Final value set during the verification checkpoint.
