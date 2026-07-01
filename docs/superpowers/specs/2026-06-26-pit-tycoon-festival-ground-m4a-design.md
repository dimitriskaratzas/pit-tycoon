# Pit Tycoon — Festival Ground (Milestone 4, sub-project A) Design

**Date:** 2026-06-26
**Status:** Approved (brainstorm) — ready for implementation plan
**Milestone:** "The Festival Ground" — sub-project **A** (foundation) of four. Builds on the Real-UI milestone (HUD + intermission shop + upgrade ghost-preview, PRs #11/#12).

## Goal

Stop the game feeling like a wireframe by turning the single fixed stage into an **open-air festival ground that you build out, Civ-style**. Spending in the intermission unlocks distinct **new structures** that rise at authored spots across an enlarged field — each carrying a gameplay effect — instead of merely scaling existing objects in place. M4a lays the **data-driven, reskinnable foundation** for that ground and proves the full vertical with 2–3 build spots wired end-to-end.

## Milestone context (the four sub-projects)

This design covers **M4a only**. The full milestone was decomposed during brainstorming:

- **M4a — Ground & build-spot foundation** *(this spec)*: enlarged open ground, data-driven `VenueLayout`, `BuildSpotController`, a `BuildSystem`, build-spots as a new shop category, 2–3 spots wired end-to-end with effects, a wide survey camera "home" pose.
- **M4b — Free-look survey camera**: bounded orbit/pan/zoom driver layered on `CameraRig`; authored fly-to wins; off during live sets.
- **M4c — Structure roster + economy**: the full set of structures and their effects, including *new* effect types (passive cash, cash multiplier, per-ability bonus) which may need Domain/economy work + tests.
- **M4d — Reskin/theme pass** *(later)*: a second world (indoor / underwater / space) to validate the swappable foundation, plus the real art-fidelity pass.

Build order: **M4a → M4b → M4c → M4d**. Each ships something playable on its own.

## Decisions (locked during brainstorm)

1. **Spine:** spatial expansion (an open ground that visibly fills with distinct structures), not an art-fidelity pass and not a theme-architecture-first build.
2. **Growth model:** **authored build spots** — the ground has pre-designed spots; the player chooses *what* to build and *in what order*, not *where*. (Player-placed tiles / true-Civ placement is explicitly rejected for this game — it would shift the core from performance toward building.)
3. **Camera:** **two resting poses** — a wide **survey pose** for the intermission (see the whole ground) and a close **live pose** for the set (watch the crowd/VFX, = today's captured pose). The camera flies to the survey pose on `SetEnded` and to the live pose on `SetStarted`, plus authored fly-to per spot (reused from ghost-preview). Bounded **free-look** (orbit/pan/zoom) is wanted but is **M4b**; M4a ships only the two poses + per-spot fly-to.
4. **Structure effects:** **every structure carries an economic effect.** M4a's proof structures reuse **existing effect primitives only** (hype-rate boost, crowd-capacity boost). New effect types are deferred to M4c.
5. **Architecture:** **additive build-spot layer** (chosen over generalizing existing upgrades into spots, and over a hard-coded no-data-layer minimal version). Existing upgrades / `VenueController` / crowd / economy are untouched; build-spots are a parallel system. Rejected alternatives: (2) refactoring existing upgrades into spots — needless regression risk during a foundation milestone; (3) hard-coded reveals with no data layer — abandons the reskinnable-foundation goal and is throwaway work.
6. **Themeable by construction:** the layout is pure data (`VenueLayout` asset + prefabs). M4a ships one open-air theme; reskinning later = a new layout asset + prefab set, no code changes.
7. **Build-spots are one-shot, not leveled:** a spot is either un-built or built (permanent). This is why they get their own `BuildSystem` rather than folding into the level-based `UpgradeSystem`.

## Scope

**In scope (M4a):**
- Enlarge the ground plane into a wide open field; keep the main stage cluster at the north end with the pit in front.
- A data-driven **`VenueLayout`** ScriptableObject describing the ground + a list of build spots.
- A **`BuildSpotController`** MonoBehaviour that lays out spots, tracks built/un-built state, raises a structure on build, and shows a translucent ghost on preview.
- A small **`BuildSystem`** MonoBehaviour (one-shot purchase) that checks cash, applies the spot's effect via existing primitives, raises the structure, and publishes a `StructureBuilt` event.
- A new **"Build"** category in `ShopView`, reusing `ShopRowWidget` and the existing select → preview → Buy/Cancel flow.
- **Preview + camera** reuse: selecting a Build row flies the camera to the spot's waypoint and ghosts the structure; Buy raises it and re-frames per existing behavior; Cancel keeps the camera put.
- A wide **survey "home" pose** for the intermission camera.
- **2–3 authored build spots** wired end-to-end (Second Stage, Camping/overflow field, optional Entrance Gate).
- An editor builder **`Pit Tycoon → Build Festival Ground`** that constructs the enlarged ground, instantiates spot placeholders, sets the survey-home pose, and wires all refs (idempotent).
- Greybox structure prefabs for the 2–3 spots (modelled in Blender like M2b, or primitives if faster) — visual fidelity is later (M4d).
- `SETUP.md` section.

**Out of scope (M4a):**
- Bounded free-look camera input (M4b).
- The full structure roster and any *new* effect types — passive cash, multipliers, per-ability bonuses (M4c).
- A second theme / underwater / space / indoor, and the art-fidelity pass (M4d).
- Player-chosen placement of structures.
- Any change to the existing 4 upgrades, the Domain economy, or the hype→cash loop math.

## Architecture

All new code lives in the Unity layer and consumes existing system public APIs + the `EventBus`. No Domain logic is added.

### Components

**`VenueLayout`** (new ScriptableObject, `Assets/PitTycoon/Domain/` is **not** appropriate — it references prefabs, so it lives in the Unity layer, e.g. `Assets/PitTycoon/Unity/`)
- A reference to the ground/environment (prefab or scene object description) and a `BuildSpot[]`.
- `BuildSpot` (serializable struct/class): `id` (string), `label` (string), display icon/color, **structure prefab** (`GameObject`), **local pose** (`Vector3 position`, `Vector3 eulerAngles`), **camera waypoint** (`Vector3 position`, `Vector3 eulerAngles`), **cost** (int), and an **effect descriptor** (an enum `BuildEffectKind { HypeRate, Capacity }` + a magnitude `float`/`int`).
- Authored as an asset; M4a ships one open-air layout. Tunable in the Inspector.

**`BuildSpotController`** (new MonoBehaviour, `Assets/PitTycoon/Unity/`, sibling to `VenueController`)
- Holds a `VenueLayout` ref and the **built-state set** (which spot ids are built).
- `Build(string id)` — instantiates the spot's structure prefab at its pose under the controller; marks the spot built. Idempotent (no-op if already built).
- `PreviewSpot(string id)` / `ClearPreview()` — instantiate a **translucent ghost** clone of the structure prefab at the empty spot (reusing the shared ghost material), torn down on clear. Same teardown discipline as `VenueController`/`CrowdController` (clear before re-preview; clear on destroy).
- Exposes `IsBuilt(id)` and the layout's un-built spots for the shop.
- Owns geometry only — **no economy, no effect application.**

**`BuildSystem`** (new MonoBehaviour, `Assets/PitTycoon/Unity/`, mirrors `UpgradeSystem` but one-shot)
- Holds refs to `EconomySystem`, `BuildSpotController`, `HypeSystem` (for `RaiseRate`), the crowd capacity sink (for `AddCapacity`), and the `EventBus`.
- `bool TryBuild(BuildSpot spot)` — if the spot is un-built and affordable: spend via `EconomySystem`, apply the effect (`HypeRate → HypeSystem.RaiseRate`, `Capacity → crowd AddCapacity`), call `BuildSpotController.Build(spot.id)`, publish `StructureBuilt`. Returns success.
- `bool CanAfford(BuildSpot spot)` for the shop's Buy-enable state.
- Confirm the exact existing signatures during implementation (`EconomySystem` spend API, `HypeSystem.RaiseRate`, the crowd capacity-raise API used by the Grounds upgrade). Reuse them; do not add new effect primitives in M4a.

**Preview/camera orchestration**
- Extend the existing `UpgradePreviewController` (or add a sibling) so a Build selection: flies the camera to `spot.cameraWaypoint` and calls `BuildSpotController.PreviewSpot(spot.id)`; Buy raises the structure + clears the ghost (camera stays); Cancel clears the ghost (camera stays); ⌂ Overview returns to survey home + clears. This mirrors the upgrade ghost-preview UX exactly.

### Ghost visuals

Reuse the existing shared **ghost material** (`Assets/PitTycoon/Art/Materials/GhostMat.mat`) for build-spot ghost clones. No new material.

### Camera

The intermission and the live set want **different framings**, so M4a defines two authored resting poses (in addition to per-spot waypoints):

- **Survey pose** — high, pulled back to the south, tilted down to frame the whole field. The intermission resting pose; ⌂ Overview and `ReturnHome` (during intermission) target it.
- **Live pose** — close to the stage/crowd for watching the set and VFX. This is effectively **today's camera pose** from sub-project B (the one the camera already sits at during play). The set resting pose.
- **Lifecycle:** on `SetEnded` the camera flies to the **survey** pose; on `SetStarted` it flies to the **live** pose (replacing sub-project B's snap-home-on-set-start, which assumed a single pose). The fly is gentle (reuse `CameraRig.FlyTo`); `ForceClear` still guarantees no ghost survives a set boundary.
- **Where the poses live:** both poses are authored data (on the preview controller and/or `VenueLayout`) so they're Inspector-tunable; the editor builder seeds sensible defaults. `CameraRig` keeps its mechanics (tween + capture); the *driver* (preview controller / bootstrap reacting to set-lifecycle events) decides which pose to fly to.
- Per-spot **camera waypoints** live in the `VenueLayout` (one per build spot), consumed by the preview controller exactly like the upgrade waypoints.

### Shop integration (`ShopView`)

- A third **"Build"** group, rendered like the existing Upgrades/Abilities groups (reuse `ShopRowWidget` + the select → preview → Buy/Cancel flow + the Group field).
- The Build group lists **un-built** spots (label, cost, afford state). On `StructureBuilt`, the row leaves the list (rebuild).
- Buy → `BuildSystem.TryBuild(spot)`; on success the preview controller's confirm path clears the ghost (the real structure now exists) and the row refreshes. Camera stays.
- Cancel → clears the ghost; camera stays; row collapses.

### Wiring

- **`GameBootstrap`** — add serialized refs for the `VenueLayout`, `BuildSpotController`, and `BuildSystem`; initialize them after the venue/economy systems exist; pass what `ShopView` needs through `HudController.Initialize(...)` (extend the existing forwarding, consistent with how `UpgradePreviewController` was threaded in sub-project B).
- **Editor builder** — new menu **`Pit Tycoon → Build Festival Ground`** (`FestivalGroundSetup.cs`, `Assets/PitTycoon/Unity/Editor/`): enlarges the ground plane, instantiates the build-spot placeholders from the layout, positions the Main Camera at the survey-home pose, adds/initializes `BuildSpotController` + `BuildSystem`, and wires camera/venue/crowd/economy/hype/shop refs via `SerializedObject`. Idempotent and consistent with `FestivalSceneSetup` / `PreviewSetup` / `HudSetup`. Runs **after** Build HUD + Build Upgrade Preview.

## The proof structures (M4a)

Reuse existing effect primitives only:

| Spot | Placement | Effect | On-screen change |
|------|-----------|--------|------------------|
| **Second Stage** | stage-left, angled toward the field | **+hype rate** | a second stage rises; hype builds faster |
| **Camping / overflow field** | back of the grounds | **+crowd capacity** | tents appear; more crowd figures fill in |
| **Entrance gate/arch** *(optional/stretch)* | a field edge | modest **+hype rate** ("arrivals hype") | a landmark gate |

Exact positions are tuned in the Inspector. Shipping **2 strong spots** (Second Stage + Camping) fully proves the vertical; the gate is a stretch.

## Data flow

```
[intermission] SetEnded -> HudController shows ShopView; camera flies to SURVEY pose
ShopView Build row click(spot)
  -> row expands (Buy/Cancel)
  -> PreviewController: CameraRig.FlyTo(spot.cameraWaypoint)
     -> BuildSpotController.PreviewSpot(spot.id)        (ghost structure shown)
Buy -> BuildSystem.TryBuild(spot)
     -> EconomySystem spend (cash check)
     -> on success: apply effect (HypeSystem.RaiseRate | crowd AddCapacity)
     -> BuildSpotController.Build(spot.id)              (real structure raised)
     -> PreviewController clears ghost; camera stays
     -> publish StructureBuilt -> ShopView rebuild (spot leaves Build list)
Cancel -> PreviewController.Cancel()                    (clear ghost, camera stays); row collapses
Switch row(spot2) -> Begin*(spot2)                      (auto-clears prior ghost, flies to spot2)
Return Home -> PreviewController.ReturnHome()           (survey home, ghost cleared)
Start Next Set -> ShopView ForceClear -> set.StartNextSet() -> SetStarted
  -> HudController: preview.ForceClear(); show live HUD; camera flies to LIVE pose
  -> built structures PERSIST (they remain raised; effects already applied)
```

## Error handling & gotchas

- **Builds are permanent and persist across sets** — `BuildSpotController` keeps raised structures; effects persist because they were applied to `HypeSystem`/crowd at build time. A set never reverts a build.
- **Previews exist only in intermission** — `Start Next Set` / `SetStarted` `ForceClear()` first (reuse the sub-project-B path), so a set never begins mid-ghost.
- **Live switching / teardown** — every `Begin*` clears the prior ghost; ghost teardown on panel `Hide()` and `OnDestroy` (reuse existing discipline). No orphaned ghost clones.
- **Already-built / unaffordable** — built spots leave the Build list; unaffordable spots show Buy greyed but preview + camera still work (window-shopping).
- **No double-spend** — `BuildSystem.TryBuild` re-checks built-state and affordability before committing.
- **One-shot, not leveled** — do not route builds through `UpgradeSystem`'s level machinery; only reuse the effect *primitives* it calls.
- Views/controllers null-guard their refs and log once if unwired (consistent with existing systems).

## Testing & verification

- **No Domain logic added.** Run `dotnet test PitTycoon.Domain.slnx` to confirm the **73** tests stay green (regression guard only). If any pure helper is introduced (e.g. build-cost math), TDD it in Domain with its own tests.
- **Unity side** is correctness-by-review + a manual checkpoint.
- **Manual checkpoint (in-Editor, by the dev):**
  1. `Pit Tycoon → Build Festival Ground` (after Build HUD + Build Upgrade Preview).
  2. Play a set, reach intermission. Camera sits at the wide survey pose; the open ground is visible; shop docked right with a **Build** category.
  3. Click a Build spot: camera flies to it; a **ghost** of the structure appears at the empty spot.
  4. **Buy** raises the real structure (ghost → real); the effect is visible (faster hype next set / denser crowd); the spot leaves the Build list; camera stays.
  5. **Cancel** reverts the ghost; camera stays put.
  6. Selecting another Build spot flies straight there; **⌂ Overview** returns to the survey pose.
  7. **Start Next Set** drops the camera to the stage/crowd and starts cleanly (no lingering ghost); **built structures remain**.
  8. F1 dev overlay and M1/M2/M3 behavior (abilities, VFX, coins, venue scaling, crowd fill, the existing upgrade ghost-preview) all intact.
- **Feel pass:** tune the survey-home pose, the per-spot camera waypoints, structure poses, costs, and effect magnitudes in the Inspector from what the dev sees in play.

## File structure

**New (Unity runtime):**
- `Assets/PitTycoon/Unity/VenueLayout.cs` — the layout ScriptableObject + `BuildSpot` type + `BuildEffectKind`.
- `Assets/PitTycoon/Unity/BuildSpotController.cs` — spot layout, built-state, `Build`/`PreviewSpot`/`ClearPreview`.
- `Assets/PitTycoon/Unity/BuildSystem.cs` — one-shot purchase: cash check, effect apply, raise, `StructureBuilt`.

**New (Editor):**
- `Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs` — `Pit Tycoon → Build Festival Ground` builder.

**New (assets, created/authored):**
- One `VenueLayout` asset under `Assets/Settings/` (open-air theme).
- Greybox structure prefabs (Second Stage, Camping, optional Gate) under `Assets/PitTycoon/Art/Prefabs/` (Blender like M2b, or primitives).

**Modified:**
- `Assets/PitTycoon/Unity/UI/ShopView.cs` — third "Build" category; route Buy/Cancel/select to `BuildSystem` + the preview controller.
- `Assets/PitTycoon/Unity/UpgradePreviewController.cs` — Build-spot preview entry points (fly to spot waypoint, ghost via `BuildSpotController`), or a sibling controller if cleaner.
- `Assets/PitTycoon/Unity/UI/HudController.cs` — forward the new refs to `ShopView`; on `SetEnded` fly the camera to the survey pose and on `SetStarted` fly it to the live pose (it already handles those events for showing/hiding the shop).
- `Assets/PitTycoon/Unity/GameBootstrap.cs` — `VenueLayout` + `BuildSpotController` + `BuildSystem` refs + init.
- `Assets/PitTycoon/Domain/Events.cs` (or wherever events live) — add `StructureBuilt` event.
- `SETUP.md` — Build Festival Ground step + verification checklist.

## Notes for the implementer

- Commit messages: imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- **Additive only** — do not touch the working 4 upgrades, `VenueController`, crowd organic-pit code, or the Domain economy. Build-spots are a parallel system.
- **No new Domain logic and no new effect primitives in M4a** — reuse `HypeSystem.RaiseRate` and the crowd `AddCapacity` path the Grounds upgrade already uses. New effect types are M4c.
- Confirm against source before use: `EconomySystem` spend API, `HypeSystem.RaiseRate`, the crowd capacity-raise API, `ShopView` group rendering + `ShopRowWidget`, `UpgradePreviewController` ghost/camera flow, `CameraRig` home capture, `FestivalSceneSetup`/`PreviewSetup` builder patterns. Don't add to these APIs unless a gap is found.
- Ghost clones should be cheap (no colliders, no extra scripts) and parented so teardown is a single `Destroy` — same as the existing ghost code.
- Keep `BuildSpotController` focused on geometry/state and `BuildSystem` on the purchase transaction; don't let either reach into the other's internals or into the crowd/hype internals beyond the public effect primitives.
- The structure prefabs are greybox for M4a; the real art pass is M4d. Make them readable in silhouette (a second stage clearly reads as a stage, tents as tents) so the "it fills up" feeling lands even in greybox.
