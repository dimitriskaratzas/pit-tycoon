# Pit Tycoon — Real Structure Models (Milestone 4, sub-project D) Design

**Date:** 2026-07-03
**Milestone:** "The Festival Ground" — sub-project **M4d** (visuals pass) of four. Builds on M4a (ground + build spots, PR #13), M4b (free-look camera, PR #14), and M4c (11-structure roster + economy, PR #15), all merged.

## Summary

M4c finished the festival ground's *mechanics*: 11 purchasable structures with real economic effects. They are all primitive-cube greyboxes. M4d replaces them with **real Blender-modelled 3D geometry** — mid-detail, cel-shaded, kit-based — driven live through the Blender MCP. The swap is pure content: new FBX models wrapped in prefabs that the existing build-spot data points at. No Domain changes, no gameplay code beyond one small idle-motion MonoBehaviour.

This is the first pass of an ongoing fidelity ramp: the user expects to push for more detail later. The kit approach is chosen partly for that — improving a kit part upgrades every structure using it.

## Design decisions (locked during brainstorming)

1. **Scope: the 11 build-spot structures only.** The M2b assets (CrowdFigure, main-stage Stage/Truss/Banner/PASpeaker FBXs) stay as-is; refreshing them is a later pass.
2. **Fidelity: mid detail** — ~2–8k tris per structure, with small props and accents (bottles on the bar, straps on the speaker wall) so the build-preview close-ups hold up.
3. **Materials: shared palette** — ~7 ComicLit cel material instances (no textures, no shader changes). Colors tunable in the Inspector; per-part assignment via named material slots.
4. **Motion: simple idle motion on 1–2 structures** (banner sway, strobe head pan) via one small MonoBehaviour. No rigging, no beat-reactivity (a possible follow-up milestone).
5. **Approach: modular kit → assembly.** Build a ~9-part shared library first, assemble the 11 structures from kit parts + a few unique pieces. Chosen for consistency, reuse (the roster shares heavy DNA), and upgrade leverage later.

## The kit (parts library)

One Blender working file: `Assets/PitTycoon/Art/Source/festival-structures.blend` (committed; source of truth for future edits). Parts are chunky and bevel-edged so the ComicLit cel ramp + outline post read them cleanly. ~200–800 tris each:

| Part | Notes |
|---|---|
| Speaker cabinet | Recessed grille face, corner protectors |
| Scaffold pole + clamp | Straight segment; clamp joint variant |
| Truss segment | Triangular lattice, tileable |
| Canopy sheet | Slightly sagging cloth plane with rim |
| Counter block | Bar-height, front panel inset |
| Pitched tent | Low-poly with door flap; 2–3 scale variants |
| Railing | Post + twin-rail, tileable |
| Strobe/light head | Boxy head + yoke, aimable |
| Sofa block | Rounded seat + back |

## The 11 structures

Assembled from kit parts + unique pieces; each keeps roughly its greybox footprint so the authored spot poses and preview camera fly-tos in `OpenAirLayout.asset` remain valid without retuning.

| Structure | Kit parts | Unique pieces |
|---|---|---|
| Amp Stack | cabinet ×6 (array) | — |
| Speaker Wall | cabinet ×12, poles | rigging straps |
| Strobe Rig | poles, truss, light head ×4 | — |
| Sponsor Banner | poles | curved banner cloth |
| Entrance Gate | poles, truss | arch sign panel |
| Bar | counter, canopy, poles | bottle shelf cluster |
| Food Court | counter, canopy ×3 | menu boards |
| VIP Lounge | railing, sofa ×2 | platform, rope posts |
| Grandstand | railing | stepped tiers, seat rows |
| Camping Field | tent ×9 (varied) | campfire ring |
| Second Stage | truss, cabinet ×2, canopy | deck, backwall |

## Materials

`Assets/PitTycoon/Art/Materials/Palette/` — ComicLit cel material instances:

- **Wood** (counters, decks, tiers)
- **MetalDark** (cabinets, poles, truss)
- **MetalLight** (clamps, rails, yokes)
- **CanvasA** / **CanvasB** (canopies, tents — two-tone variety)
- **AccentWarm** (banner cloth, sign panels, sofa)
- **StrobeGlow** (emissive-tinted light-head lenses)

In Blender each part's material slots use these exact names; the editor prefab builder maps slot name → Unity material at build time. Recoloring the entire festival = editing 7 materials in the Inspector.

## Export & integration

- Per-structure FBX exported to `Assets/PitTycoon/Art/Models/Structures/<Name>.fbx` (same axis/scale conventions as M2b exports; verify one import before batch-exporting).
- New editor builder `StructurePrefabs` (sibling of `StructureGreyboxPrefabs`, same idempotent load-or-build pattern): wraps each FBX in a prefab at `Assets/PitTycoon/Art/Prefabs/Structures/<Name>.prefab` and assigns palette materials by slot name.
- `FestivalGroundSetup` points the 11 spots' prefab refs at the new prefabs. Greybox prefabs stay in the repo as fallback.
- No Domain changes. No changes to `BuildSystem`/`BuildSpotController`/`ShopView` — the swap is data.

## Idle motion

One MonoBehaviour, `IdleMotion` (in `Assets/PitTycoon/Unity/`): sways/rotates a serialized child transform with serialized amplitude, speed, and axis. Used on:

- **Sponsor Banner** — gentle cloth sway.
- **Strobe Rig** — slow head pan.

Added by the `StructurePrefabs` builder to those two prefabs. Uses `Time.deltaTime`; purely cosmetic, no EventBus, no Domain.

## Batches & checkpoints

Modelling proceeds in batches with an Editor look-check between each (survey-distance view + preview close-up, checking silhouettes, palette, and outline/halftone post on the new geometry):

1. **Kit** — the 9 parts.
2. **Batch 1 (audio gear):** Amp Stack, Speaker Wall, Strobe Rig — heaviest kit reuse, fastest wins.
3. **Batch 2 (hospitality):** Bar, Food Court, VIP Lounge.
4. **Batch 3 (remainder):** Sponsor Banner, Entrance Gate, Grandstand, Camping Field, Second Stage.

## Testing & verification

- `dotnet test PitTycoon.Domain.slnx` stays at 96 passing by construction (nothing touches Domain).
- Manual Editor checkpoint per batch, final checkpoint verifies:
  - All 11 structures rise on purchase with real models (correct spot, correct footprint).
  - Ghost preview + camera fly-to close-ups hold up at mid detail.
  - Banner sways, strobe heads pan.
  - Comic post-processing (outline, halftone) reads well on the new geometry.
  - M1–M4c regression intact (hype/economy/abilities/effects unchanged).

## Out of scope (likely follow-ups)

- Crowd figure upgrade / body variants.
- Main-stage (M2b asset) refresh.
- Beat-reactive structure parts (amp thump, strobe flash on `BeatDetected`).
- Painted texture atlas / surface detail beyond the palette.
- Any economy or gameplay changes.

## Tuning knobs

- Palette material colors (Inspector, live).
- `IdleMotion` amplitude/speed per structure (Inspector).
- Per-spot poses/costs unchanged from M4c (`OpenAirLayout.asset`).
