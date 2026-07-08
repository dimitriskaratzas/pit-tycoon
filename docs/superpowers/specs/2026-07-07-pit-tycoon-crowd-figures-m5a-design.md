# Pit Tycoon — Rigged Crowd Figures (Milestone 5, sub-project A) Design

**Date:** 2026-07-07
**Milestone:** "The Fidelity Pass" — sub-project **M5a** of four. M5 order locked during brainstorming: **M5a crowd figures → M5b main-stage refresh → M5c beat-reactive parts → M5d structure surface detail.** Doing the stage refresh before beat-reactivity means the beat hooks land on final geometry.

## Summary

The pit is currently a pool of identical `CrowdFigure.fbx` clones; variety is ±12% scale jitter and all motion is a procedural whole-body sine bob plus beat pops. M5a replaces this with **rigged, animated figures**: 4 body variants skinned to one shared armature, 3 animation clips blended by music intensity, and per-member outfit tints. This is the project's first armature + Mecanim work.

## Design decisions (locked during brainstorming)

1. **Variety: bodies + colors.** 4 body-shape variants × ~6 outfit tints assigned per member.
2. **Motion: rigged animations** (not procedural limbs, not bob-only). Chosen for quality; perf is fine at pit scale (~100–200 figures) with Animator culling.
3. **Clips: 3, hype-blended.** `Sway` → `Groove` → `HypeJump` blended by the analyzer's smoothed 0..1 intensity, with per-member playback offset so the crowd never moves in lockstep.
4. **Architecture: classic Mecanim** (option A). One shared rig, SkinnedMeshRenderer + Animator per member, one shared AnimatorController with a 1D blend tree. GPU vertex-animation baking (B) and LOD hybrid (C) rejected as complexity we don't need at this crowd size.

## Blender assets

New working file `ArtSource/crowd-figures.blend` (repo root, NEVER inside `Assets/` — Unity would import it):

- **Armature:** one simple rig, ~10 bones — hips, spine, head, upper+lower arm ×2, leg ×2. Stylised chibi proportions; no fingers, no toes, no IK in the export.
- **Bodies:** 4 variants skinned to that same armature, each ~1–2k tris, keeping the current figure's comic/chibi silhouette language:
  - `CrowdBase` — the everyman (near-current proportions)
  - `CrowdChunky` — wider, heavier build
  - `CrowdLanky` — tall and thin
  - `CrowdShort` — small and round
- **Actions (3), all loopable:**
  - `Sway` — weight shift side to side, arms low (low energy)
  - `Groove` — knee bounce, arms moving at waist/chest (mid energy)
  - `HypeJump` — jumping with arms up (full energy)
- **Export:** 4 FBXs to `Assets/PitTycoon/Art/Models/Crowd/<Name>.fbx`, same axis/scale conventions as the M2b/M4d exports. Each FBX carries the shared rig + all 3 clips. Verify one import end-to-end (scale, clip names, loop) before batch-exporting the rest.

## Unity animation

- **One shared `AnimatorController`** asset at `Assets/Settings/CrowdAnimator.controller`, created by the editor builder: a single state containing a **1D blend tree** on float parameter **`Energy`** (0..1) — Sway at 0, Groove at 0.5, HypeJump at 1. Loop time on all clips.
- **Per-member desync:** on spawn each member's Animator starts at a random normalized time and gets ±10% random speed.
- **Culling:** `Animator.cullingMode = CullCompletely` so off-screen members cost nothing.

## CrowdController changes (smallest possible)

Keeps: the pool, fill/scale-in logic, FillFraction/ICrowdMeter, beat pops (the pop scales the transform — it stacks fine on top of skinned animation).

Changes:
- **Remove the whole-body sine bob** — the clips own body motion now.
- **`memberPrefab` (single) → `memberPrefabs` (array).** Each pooled member picks a random variant. A one-element array behaves exactly like today, so existing scenes degrade gracefully until re-wired.
- **Per-frame:** write the analyzer's smoothed intensity to each member's Animator `Energy` float (cache the Animator refs at pool build; skip members that have none — greybox capsule fallback stays valid).
- **On spawn:** assign a random outfit tint from a serialized ~6-color palette via `MaterialPropertyBlock` (same per-renderer mechanism as the M2a crowd tint hook — no material instancing explosion).

No Domain changes; the 98 Domain tests stay green by construction.

## Builder + scene wiring

- **New editor builder `CrowdPrefabs`** (sibling of `StructurePrefabs`, same idempotent load-or-build pattern): creates the AnimatorController + blend tree, wraps each crowd FBX in a prefab at `Assets/PitTycoon/Art/Prefabs/Crowd/<Name>.prefab` with the ComicLit crowd material and the shared controller assigned, and returns the prefab set. Falls back gracefully (logs + skips) for FBXs not yet exported.
- **`FestivalSceneSetup`** (existing crowd wiring point): fills CrowdController's `memberPrefabs` array with the 4 variant prefabs and seeds the outfit palette.
- **SETUP.md:** new M5a section — build steps, verification checklist, tuning knobs.

## Batches & checkpoints

1. **Rig + `CrowdBase` + 3 actions** — export one FBX, full pipeline check in the Editor (import scale, clip names, blend tree, loop, cel shading + outline on a skinned mesh) before anything else.
2. **Remaining 3 bodies** — skin to the same rig, batch-export.
3. **Final checkpoint** verifies: low hype sways, mid grooves, high jumps; blend transitions read smoothly as a set builds; beat pops still visible on top; no lockstep (offsets working); tints vary; frame rate holds at max capacity; M1–M4 regression intact.

## Testing & verification

- `dotnet test PitTycoon.Domain.slnx` — unchanged, stays at 98 passing.
- Motion/blend quality is inherently visual: verified at the Editor checkpoints above.

## Out of scope

- Beat-synchronised animation (M5c may revisit; beat pops via transform remain).
- Crowd LODs, GPU instanced animation.
- Named characters, facial features, accessories.
- Main-stage assets (M5b), structure detail (M5d).

## Tuning knobs

- Outfit palette colors (serialized on CrowdController, live in Inspector).
- Blend thresholds/clip speeds (AnimatorController, live).
- Per-member speed jitter range, beat-pop strength (CrowdController, existing).
