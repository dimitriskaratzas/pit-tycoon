# Pit Tycoon — Upgrade Ghost-Preview (sub-project B) Design

**Date:** 2026-06-26
**Status:** Approved (brainstorm) — ready for implementation plan
**Milestone:** "Real UI" — sub-project **B** of two. Builds on sub-project **A** (UGUI HUD + intermission shop, PR #11).

## Goal

Turn the intermission shop's immediate buy-on-click into a **select → preview → confirm** flow: selecting an upgrade flies the camera to the relevant venue spot and shows a **ghost** of the change (bigger stage / bigger PA / brighter lights / new crowd rows); **Buy** pays, **Cancel** reverts. Ability rows preview by firing a one-shot demo of their VFX. This is a presentation/camera layer over the working economy — **no new Domain logic**.

## Scope

**In scope (B):**
- A `CameraRig` that flies the Main Camera between an "overview home" and per-upgrade framing waypoints.
- An `UpgradePreviewController` state machine that orchestrates preview/confirm/cancel and delegates the ghost to each system.
- Preview hooks on `VenueController` (stage/PA/lighting), `CrowdController` (crowd rows), and `AbilitySystem` (ability VFX demo).
- A shared translucent **ghost material**.
- `ShopView` two-step flow (row expands to Buy/Cancel; live preview switching) + a **Return Home** ("⌂ Overview") button.
- Editor builder (`Pit Tycoon → Build Upgrade Preview`) that constructs and wires it all.

**Out of scope:**
- Any change to the hype → cash → upgrade economy or the Domain layer.
- New abilities/upgrades or new venue geometry.
- Halftone/manga polish on the ghost (a plain translucent material is fine for this milestone).
- Free-look / player-controlled camera during the shop (camera is driven only by selection / Return Home / set lifecycle).

## Decisions (locked during brainstorm)

1. **Architecture:** central `UpgradePreviewController` + per-system preview hooks (each system owns *how* it ghosts itself). Chosen over a god-object controller (would duplicate `VenueController` math and reach into `CrowdController` internals) and over a no-true-ghost reuse of `Apply` (preview would look identical to "bought").
2. **Grounds preview:** ghost the new back rows — translucent placeholder members in the would-be new capacity slots.
3. **Preview switching:** live — clicking another row cancels the current preview and flies to the new one.
4. **Ability rows:** included — preview fires a one-shot VFX demo, Confirm unlocks.
5. **Confirm/Cancel placement:** inside the shop row (the selected row expands to Buy $cost / Cancel), keeping the docked panel anchored and the scene preview unobstructed.
6. **Camera-return behavior:** **Cancel keeps the camera put** (only reverts the ghost). The camera moves *only* on a new selection or an explicit **Return Home** button. `Start Next Set` / `SetStarted` auto-return home. Gentle ease on all flights. (Civ-like: minimize involuntary camera motion.)
7. **Waypoints:** authored serialized poses (per `UpgradeKind` + ability-demo), pre-filled with scene-derived defaults by the editor builder, tunable in the Inspector. "Home" captured live from the camera at startup.

## Architecture

All new code lives in the Unity layer; it consumes existing system public APIs and the `EventBus`. No system rewrites — only additive preview hooks.

### Components

**`CameraRig`** (new MonoBehaviour, `Assets/PitTycoon/Unity/`)
- Captures the Main Camera's start transform as **home** on `Awake`.
- `FlyTo(Vector3 pos, Quaternion rot, float dur)` and `ReturnHome(float dur)` tween position/rotation each frame with a smoothstep ease; re-targetable mid-flight (sets a new goal from the current pose).
- Knows nothing about upgrades. Single responsibility: move the camera.
- Serialized: default fly duration, ease curve/strength.

**`UpgradePreviewController`** (new MonoBehaviour, `Assets/PitTycoon/Unity/`)
- State machine: `Idle → Previewing(target) → (commit | cancel) → Idle`. Tracks the current `UpgradeDefinition`/`AbilityDefinition` and previewed level.
- Holds refs: `CameraRig`, `VenueController`, `CrowdController`, `AbilitySystem`, `UpgradeSystem`, and a `PreviewWaypoint[]` (pose per `UpgradeKind` + one ability-demo pose).
- API:
  - `BeginUpgradePreview(UpgradeDefinition def, int nextLevel)` — fly to `def.Kind`'s waypoint; ask the owning system to show its ghost for `nextLevel`.
  - `BeginAbilityPreview(AbilityDefinition def)` — fly to the ability-demo waypoint; `AbilitySystem.PlayDemo(def)`.
  - `Confirm()` — purchase already happened in `ShopView` via the existing API; this clears the ghost (the real change is now applied by `TryPurchase`), keeps the camera put, and (for upgrades) re-arms the preview at the new `nextLevel` so the ghost shows the *next* step.
  - `Cancel()` — clear the ghost, **camera stays put**.
  - `ReturnHome()` — `CameraRig.ReturnHome`; also clears any active ghost.
  - `ForceClear()` — cancel + return home (used on set start). Idempotent.
- Owns no ghost geometry; it delegates to the systems below.

**Preview hooks on existing systems** (each keeps owning *how* it ghosts):
- `VenueController.Preview(UpgradeKind kind, int level)` / `ClearPreview()`
  - **Stage / PA:** spawn a translucent **ghost clone** at the target scale (`_base * f(level)`), real object untouched. `ClearPreview` destroys the clone(s).
  - **Lighting:** ramp the real accent lights to the target level's intensity (the brightness *is* the visible change; reversible). `ClearPreview` re-applies `Apply(Lighting, currentLevel)` — absolute, so exact.
- `CrowdController.PreviewCapacity(int delta)` / `ClearPreview()`
  - Instantiate translucent ghost members in the slots rows past current `Capacity` would occupy (same layout math as `Build`), **without mutating `_fill`**. `ClearPreview` destroys them.
- `AbilitySystem.PlayDemo(AbilityDefinition def)`
  - Fire the ability's VFX once with **no** cooldown/ownership/gameplay effect — a visual taster only.

### Ghost visuals

One shared **ghost material** asset (URP transparent, tinted cyan-white, low alpha, keeps the comic outline), created by the editor builder and referenced by `VenueController` and `CrowdController` for their ghost clones. Lighting and ability demos use the real lights / real VFX (no clone).

### Camera rig & waypoints

`PreviewWaypoint` is a small serializable struct `{ UpgradeKind kind; Vector3 position; Vector3 eulerAngles; }`. The controller holds an array plus one ability-demo pose. The editor builder pre-fills defaults derived from known scene positions:
- **Stage** — pulled back, head-on to the stage (z≈9).
- **PA** — angled toward the right PA stack (x≈6, z≈9).
- **Lighting** — tilted up toward the truss lights (y≈4, z≈9).
- **Grounds** — behind the pit, looking at the new back rows.
- **Ability demo** — wide shot framing stage + crowd.

All tunable in the Inspector. Home is captured live (survives camera repositioning by other setup scripts).

### Shop two-step flow (`ShopView` + `ShopRowWidget`)

- Row click no longer purchases. It selects the row (expands to reveal **Buy $cost** / **Cancel**) and calls `BeginUpgradePreview` / `BeginAbilityPreview`.
- **Buy** → existing `UpgradeSystem.TryPurchase(def)` / `AbilitySystem.TryUnlock(def)` (unchanged). On success: the real change is applied by those calls; `PreviewController.Confirm()` clears the ghost and re-arms at the next level; the row refreshes (level/cost/afford). Camera stays. Buy is disabled when unaffordable (preview still works).
- **Cancel** → `PreviewController.Cancel()` (revert ghost, camera stays); row collapses.
- **Switch rows** mid-preview → the new `Begin*` auto-cancels the current ghost and flies to the new target.
- **Return Home** ("⌂ Overview", top of the shop panel) → `PreviewController.ReturnHome()`; collapses any expanded row.
- `ShopRowWidget` gains `BuyButton` + `CancelButton` (shown only while that row is selected); built by `HudSetup`.

### Wiring

- **`GameBootstrap`** — add `[SerializeField] CameraRig cameraRig;` and `[SerializeField] UpgradePreviewController preview;` (added to the null-guard). Initialize them after the venue systems exist; pass `preview` to `HudController.Initialize(...)`.
- **`HudController`** — forward the `UpgradePreviewController` to `ShopView.Initialize(...)`; on `SetStarted`, call `preview.ForceClear()` before showing the live HUD (no set begins mid-ghost).
- **`ShopView`** — holds the `UpgradePreviewController`; `OnStartNextSet` calls `preview.ForceClear()` before `set.StartNextSet()`.
- **Editor builder** — new menu **`Pit Tycoon → Build Upgrade Preview`** (`PreviewSetup.cs`, `Assets/PitTycoon/Unity/Editor/`): adds `CameraRig` (to the Main Camera) + `UpgradePreviewController` (on `Systems`), creates the ghost-material asset, fills default waypoints, and wires camera/venue/crowd/abilities/upgrades/shop refs via `SerializedObject`. Idempotent (re-run rebuilds cleanly), consistent with `FestivalSceneSetup` / `HudSetup`.

## Data flow

```
[intermission] SetEnded -> HudController shows ShopView (docked right), camera at home
ShopView row click(def)
  -> row expands (Buy/Cancel)
  -> UpgradePreviewController.BeginUpgradePreview(def, levelOf+1)
     -> CameraRig.FlyTo(waypoint[def.Kind])
     -> VenueController.Preview / CrowdController.PreviewCapacity  (ghost shown)
Buy -> UpgradeSystem.TryPurchase(def)         (real change + UpgradePurchased)
     -> PreviewController.Confirm()           (clear ghost, re-arm next level, camera stays)
     -> row refreshes
Cancel -> PreviewController.Cancel()          (revert ghost, camera stays); row collapses
Switch row(def2) -> BeginUpgradePreview(def2) (auto-cancels prior ghost, flies to def2)
Return Home -> PreviewController.ReturnHome() (camera home, ghost cleared)
Start Next Set -> ShopView.ForceClear -> set.StartNextSet() -> SetStarted
  -> HudController: preview.ForceClear(); show live HUD (camera home)
```

## Error handling & gotchas

- **Previews exist only in intermission.** `Start Next Set` and any `SetStarted` call `ForceClear()` first, so a set never begins mid-ghost and the camera is always home for live play.
- **Live switching** must not leak ghost objects — every `Begin*` clears the prior preview before showing the next.
- **Ghost teardown** on panel `Hide()` and `OnDestroy` (no orphaned clones).
- **Camera interruption** — `FlyTo` always retargets from the current pose, so a mid-flight switch is smooth.
- **Lighting revert** is exact because `VenueController.Apply` sets absolute values from the captured base.
- **Confirm overlap** — after `TryPurchase` the real geometry exists; the controller must clear its ghost clone so you don't see ghost + real overlapping, then re-arm at the next level.
- **Unaffordable** — Buy disabled/greyed; preview and camera still work (window-shopping).
- Views/controllers null-guard their refs and log once if unwired (consistent with existing systems).

## Testing & verification

- **No Domain logic added.** Run `dotnet test PitTycoon.Domain.slnx` to confirm the **73** tests stay green (regression guard only).
- **Unity side** is correctness-by-review + a manual checkpoint.
- **Manual checkpoint (in-Editor, by the dev):**
  1. `Pit Tycoon → Build Upgrade Preview`.
  2. Play a set, reach intermission. Camera sits at the overview; shop docked right.
  3. Click each upgrade: camera flies to the right spot; the ghost shows the *next* step (bigger stage / bigger PA / brighter lights / new back-row figures).
  4. **Buy** commits (ghost → real), the row bumps level/cost, the preview re-arms for the next level, camera stays.
  5. **Cancel** reverts the ghost; camera stays put.
  6. Selecting another row flies straight there; **⌂ Overview** returns home.
  7. An ability row fires a one-shot VFX demo; Buy unlocks it.
  8. **Start Next Set** returns the camera home and starts cleanly (no lingering ghost); live HUD intact.
  9. F1 dev overlay and M1/M2/M3 behavior (abilities, VFX, coins, venue scaling, crowd fill) intact.
- **Feel pass:** tune camera duration/ease, ghost alpha, and the five waypoint poses in the Inspector from what the dev sees in play.

## File structure

**New (Unity runtime):**
- `Assets/PitTycoon/Unity/CameraRig.cs`
- `Assets/PitTycoon/Unity/UpgradePreviewController.cs`

**New (Editor):**
- `Assets/PitTycoon/Unity/Editor/PreviewSetup.cs`

**New (asset, created by the builder):**
- ghost material under `Assets/PitTycoon/Art/Materials/`

**Modified:**
- `Assets/PitTycoon/Unity/VenueController.cs` — `Preview(kind, level)` / `ClearPreview()` + ghost-material ref.
- `Assets/PitTycoon/Unity/CrowdController.cs` — `PreviewCapacity(delta)` / `ClearPreview()` + ghost-material ref.
- `Assets/PitTycoon/Unity/AbilitySystem.cs` — `PlayDemo(def)`.
- `Assets/PitTycoon/Unity/UI/ShopView.cs` — two-step flow + Return Home; takes `UpgradePreviewController`.
- `Assets/PitTycoon/Unity/UI/ShopRowWidget.cs` — Buy/Cancel buttons.
- `Assets/PitTycoon/Unity/UI/HudController.cs` — forward preview controller; `ForceClear` on set start.
- `Assets/PitTycoon/Unity/GameBootstrap.cs` — `cameraRig` + `preview` refs + init.
- `Assets/PitTycoon/Unity/Editor/HudSetup.cs` — Buy/Cancel UI on the shop-row template + the Return Home button.
- `SETUP.md` — Build Upgrade Preview step + verification checklist.

## Notes for the implementer

- Commit messages: imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- Keep the controller focused on the state machine + camera; let each system own its ghost. Don't let `UpgradePreviewController` reach into `CrowdController`/`VenueController` internals — call the new hooks only.
- The shop must stay docked right with **no scene dimming** — the whole point is the camera flying through the visible scene.
- Confirm against source before use: `UpgradeSystem.LevelOf/CurrentCost/TryPurchase`, `AbilitySystem.Abilities/DefinitionOf/TryUnlock` and its VFX path, `VenueController` base-capture + `Apply`, `CrowdController.Build` layout math. Don't add to these APIs unless a gap is found.
- Ghost clones should be cheap (no colliders, no extra scripts) and parented so teardown is a single `Destroy`.
