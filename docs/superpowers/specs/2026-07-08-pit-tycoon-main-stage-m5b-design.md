# Pit Tycoon — Main-Stage Refresh (Milestone 5, sub-project B) Design

**Date:** 2026-07-08
**Milestone:** "The Fidelity Pass" — sub-project **M5b** of four (M5a crowd figures shipped, PR #18; M5c beat-reactive parts and M5d structure surface detail follow). Doing the stage before beat-reactivity so M5c's hooks land on final geometry.

## Summary

The main stage is still the M2b greybox-era set: five flat-material objects (Stage, Truss, Banner, PA ×2) placed at z≈9 by `FestivalSceneSetup`. M5b replaces them with **one full stage rig** — a single `MainStage.fbx` assembled in Blender at M4d kit quality — while keeping the existing venue-upgrade mechanics (stage/PA scaling, ghost previews, accent lights) working unchanged.

## Design decisions (locked during brainstorming)

1. **Scope: full stage rig** at the same spot — deck + roof canopy on truss legs, LED-style backwall, PA wings, sub stacks, banner. Not a drop-in mesh swap, not a multi-level catwalk build.
2. **Asset architecture: one `MainStage.fbx`** with named child parts surviving import (option A). Composition lives in Blender; one placement call; child hierarchy provides M5c hooks.
3. **Upgrades: whole-rig scaling** (existing mechanic kept). Each Stage level scales the full rig; PA upgrades scale the wings; ghost previews unchanged. "Levels add visible parts" noted as a possible future milestone.

## Blender asset

`ArtSource/festival-structures.blend`, new `MainStage` collection reusing the M4d kit parts (truss segments, speaker cabinets, poles, canopy):

- **Deck** — stage platform with front skirt panel, ~14u wide.
- **RoofTruss** — 4 truss-leg towers + roof truss grid carrying a slightly sagging canopy.
- **Backwall** — angled LED-style panel behind the performer position (StrobeGlow accents).
- **PAWingL / PAWingR** — 3-cabinet vertical speaker arrays flanking the deck.
- **SubStackL / SubStackR** — 2-cabinet sub stacks at the deck's front corners.
- **BannerCloth** — curved banner on the roof front edge.

~6–9k tris total. Material slots use the exact M4d palette names (Wood, MetalDark, MetalLight, AccentWarm, StrobeGlow, CanvasA) so the existing palette assets recolor it live.

**M5c hook contract:** the FBX child objects are named exactly `Deck`, `RoofTruss`, `Backwall`, `PAWingL`, `PAWingR`, `SubStackL`, `SubStackR`, `BannerCloth` — beat-reactivity (M5c) will find parts by these names; renaming them later is a breaking change.

Export: `Assets/PitTycoon/Art/Models/MainStage.fbx`, same conventions as M4d/M5a exports.

## Unity integration

- **`StructurePrefabs.EnsureMainStage()`** (new builder entry, same idempotent pattern): wraps the FBX in `Assets/PitTycoon/Art/Prefabs/MainStage.prefab`, palette materials by slot name, `IdleMotion` on `BannerCloth` (cloth sway, like the M4d SponsorBanner).
- **`FestivalSceneSetup`** changes:
  - Deletes the five legacy objects (Stage, Truss, Banner, PA Left, PA Right) if present; places the MainStage prefab at the same spot (z≈9, facing the crowd).
  - Re-parents the M2a accent lights onto the `RoofTruss` child (same behavior as today's truss reparenting).
  - Re-wires `VenueController`: `stage` → MainStage root, `paLeft`/`paRight` → `PAWingL`/`PAWingR`. Scaling and ghost previews then work with zero VenueController code changes.
- **No Domain changes, no gameplay code changes.** The only C# edits are the two editor builders.

## Testing & verification

- `dotnet test PitTycoon.Domain.slnx` — unchanged (98 passing by construction).
- Editor checkpoint verifies: old objects gone, rig placed and palette-colored; accent lights sit on the roof truss; banner sways; Stage/PA upgrades scale the right transforms with correct ghost previews; comic post reads well; M1–M5a regression (crowd dances in front of the new stage at correct relative scale).

## Out of scope (follow-ups)

- Beat-reactive parts (M5c — the named-child contract above is its landing pad).
- Upgrade tiers that add visible parts (possible future milestone; strong tycoon feedback).
- Structure surface detail (M5d), performer/DJ character, stage VFX changes.

## Tuning knobs

- Palette materials (shared with M4d, live in Inspector).
- `IdleMotion` amplitude/speed on BannerCloth.
- `VenueController` stageStep/paStep scaling factors (existing, unchanged).
