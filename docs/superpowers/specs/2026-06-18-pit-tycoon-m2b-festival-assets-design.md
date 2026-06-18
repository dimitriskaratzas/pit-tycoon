# Pit Tycoon — Milestone 2b Design (Festival Assets)

**Date:** 2026-06-18
**Status:** Approved (design), pending spec review
**Scope:** M2b only — replace the greybox primitives with real low-poly geometry that reads at the pulled-back camera, shaded by the existing M2a comic pipeline. No new gameplay systems beyond the crowd-instancing change.
**Source brief:** `pit-tycoon-claude-code-brief.md` (Art direction — "spend visual budget on silhouettes").
**Predecessor:** M2a cel-shading pipeline (shipped, PR #4 merged).

## 1. Goal

Swap the greybox primitives (capsule crowd, plane ground, no stage) for real festival geometry — a stage, truss rig, PA stacks, backdrop banner, and a crowd figure — modeled in Blender and dropped into the M2a comic pipeline so they inherit ComicLit shading. When M2b is done, the same M1 loop plays through a fixed camera, but the scene reads as an actual gig instead of capsules.

**Non-negotiable rule:** M2b must not regress the M1 loop or the M2a look. The crowd still reacts to audio, the ability/hype/economy/upgrade chain is unchanged, and the comic shading still applies.

## 2. Where M2b sits in the arc (trajectory note)

Captured because it framed the design discussion:

- The game is already a real **3D** scene with a perspective camera; M2b makes the *content* real geometry. The flat layout mock used in brainstorming is a communication diagram, not the engine fidelity.
- **Fidelity keeps climbing** across milestones (M2b geometry → M2c manga VFX → later props/textures/better figures/environment), but always within the locked **gritty graphic-novel comic** direction — not photoreal.
- A **movable camera + walkable venue with stalls/vendors** is the believable end state and nothing here blocks it, but it is **explicitly a later milestone** (a camera-control system + venue content), NOT M2b. M2b delivers a great-looking **fixed-camera** scene.

## 3. Environment & constraints

- **Unity 6 LTS**, **URP 17.4.0**, comic pipeline from M2a (`ComicLit` + Halftone/Outline render features + `ComicLook` Volume).
- **Blender MCP is connected** and drives modeling directly (confirmed live). Work happens in a dedicated `PitTycoon_M2b` collection so the user's existing default objects (Cube/Camera/Light) are left untouched — never destructively modify their scene.
- Claude can: drive Blender via MCP, write FBX into the project, author C#/editor scripts, write `SETUP.md`. Claude **cannot** operate the Unity Editor (import settings confirmation, prefab/scene checkpoints) — those are ordered `SETUP.md` steps + an editor script.
- Engineer is C#-fluent, first-time Unity/Blender. Explain Blender→Unity import gotchas (scale, axis, no animation).

## 4. Asset set (Core festival — all low-poly, silhouette-first)

| Asset | Description | Notes |
|-------|-------------|-------|
| Crowd figure | Arms-up humanoid silhouette (head + torso/legs + raised arms) | One mesh, a few hundred tris; the instanced crowd unit |
| Stage | Raised riser/platform | Single mesh |
| Truss rig | Two towers + top beam | The three M2a accent lights mount on it |
| PA stacks ×2 | Speaker towers flanking the stage | Mirror-placed |
| Backdrop banner | Flat panel behind the stage | Accent-edged |

Detail budget goes to **silhouette**, not surface — the camera is pulled back. Two materials cover everything: a dark-ink structure material (stage/truss/PA/banner) and the crowd material, both `ComicLit`.

## 5. Blender → Unity pipeline

1. Model each asset in Blender (MCP, `PitTycoon_M2b` collection), Z-up, real-world-ish scale.
2. Export **FBX** into `Assets/PitTycoon/Art/Models/` with Unity-friendly scale/orientation applied at export (so imports need no rotation fix).
3. Unity auto-imports; an editor script `FestivalSceneSetup` (sibling to `ComicLookSetup`):
   - creates prefabs from the imported meshes,
   - assigns the ComicLit structure/crowd materials,
   - places stage / truss / PA / banner behind the crowd grid,
   - reparents the three accent lights (`Accent Amber/Magenta/Cyan`) onto the truss,
   - points `CrowdController` at the crowd-figure prefab (serialized field),
   - saves the scene.
4. User runs a bring-up checkpoint.

*Rejected alternatives:* dropping a `.blend` straight into `Assets/` (couples to the Blender install, slower import); fully manual import/wiring (more user work). FBX + editor script mirrors the M2a delivery model.

## 6. Crowd instancing + reaction (the one gameplay-code change)

`CrowdController` is the only gameplay code touched:

- `Build()` instantiates the **crowd-figure prefab** (`Instantiate(memberPrefab, ...)`) instead of `GameObject.CreatePrimitive(PrimitiveType.Capsule)`. A serialized `GameObject memberPrefab` is added; if unset, it falls back to the capsule (so behaviour degrades gracefully and the field can be wired by the editor script).
- Reaction changes from **vertical scale-stretch → a vertical hop**: `localPosition.y` bounces with intensity (and a bigger hop on beat-pop), while the figure keeps its proportions (no `localScale.y` stretching of a humanoid). Small per-instance rotation/scale variance adds visual life.
- Draw cost: one shared material → SRP Batcher handles it, as it already did for ~1200 capsules in M1. A sane member cap stays via the existing `Grow()` clamps.

## 7. Verification

No automated tests (assets/rendering). Manual in-editor checkpoint:

1. Figures replace the capsules and read as a crowd at the camera distance.
2. Stage / truss / PA / banner are present and comic-shaded; accent lights sit on the truss.
3. **M1 loop intact** — crowd hops with intensity, pops on beat, grows on upgrade; ability/hype/economy/intermission all unchanged.
4. Performance is not janky at full crowd size.
5. Domain suite still 50 green (it is untouched, but run it as the regression guard).

## 8. Out of scope (explicit)

- Animated/skeletal crowd (the figure is a static mesh that hops).
- Multiple crowd-figure variants, front-of-stage barriers (that was the "Core + dressing" option).
- Environment dressing (fencing, tents, sky geometry).
- Movable camera, walkable venue, stalls/vendors — a later milestone.
- All M2c manga beat-VFX and the restyled whirlpool/coins.

## 9. Risks & open questions

- **FBX scale/axis** — Blender Z-up vs Unity Y-up; apply transforms + correct export settings so prefabs import upright at sensible scale. Validate on the first asset before modeling the rest.
- **Instance count perf** — hundreds of figure meshes; rely on SRP Batcher, keep the figure low-poly, cap crowd size if needed.
- **Crowd hop tuning** — the bounce height/decay needs to read as "jumping crowd," not "floating"; an Inspector dial refined at the checkpoint.
- **Material on imported meshes** — FBX imports create their own material slots; the editor script must override them with the ComicLit materials.
- **Silhouette legibility** — confirm the arms-up figure reads at distance before committing to it (cheap to iterate in Blender).
