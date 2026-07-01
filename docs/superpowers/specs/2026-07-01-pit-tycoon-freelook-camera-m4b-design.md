# Pit Tycoon — Free-Look Survey Camera (Milestone 4, sub-project B) Design

**Date:** 2026-07-01
**Milestone:** "The Festival Ground" — sub-project **M4b** (free-look camera) of four. Builds directly on M4a (open ground + build spots, PR #13) and the `CameraRig` introduced in the earlier Real-UI ghost-preview work (PRs #11/#12).

## Summary

Now that M4a gives the player a whole ground to look at, let them actually **look around it**. M4b adds a **bounded, Civ-6-style free-look camera** during the intermission: pan across the ground, orbit the yaw, and zoom in/out. Authored camera moves (survey/live resting poses, per-spot and per-upgrade fly-tos) still **always win** — free-look is the default resting behaviour that authored moves momentarily override. Free-look is **fully disabled during live sets**, when the player is watching the crowd/VFX.

## Milestone context (the four sub-projects)

This design covers **M4b only**. The full milestone was decomposed during brainstorming:

- **M4a — Ground & build-spot foundation** *(shipped, PR #13)*: enlarged open ground, data-driven `VenueLayout`, `BuildSpotController`, `BuildSystem`, build-spots as a shop category, survey/live resting poses.
- **M4b — Free-look survey camera** *(this spec)*: bounded orbit/pan/zoom driver layered on `CameraRig`; authored fly-to wins; off during live sets.
- **M4c — Structure roster + economy**: the full set of structures and their effects, including *new* effect types (passive cash, cash multiplier, per-ability bonus) which may need Domain/economy work + tests.
- **M4d — Reskin/theme pass** *(later)*: a second world (indoor / underwater / space) to validate the swappable foundation, plus the real art-fidelity pass.

Build order: **M4a → M4b → M4c → M4d**. Each ships something playable on its own.

## Design decisions (locked during brainstorming)

1. **Control scheme:** **Civ-6-style.** `W/A/S/D` + arrow keys pan a focus point across the ground; hold **middle-mouse** and drag to orbit the **yaw**; **scroll wheel** zooms. The camera glides at a fixed tilt.
2. **Fixed tilt, no pitch control (for M4b):** the camera **pitch is held** — there is no pitch input. Free-look adopts whatever pitch the camera had when it took control (normally the survey pose's tilt) and holds it. This keeps the framing readable and avoids awkward under-ground angles. Pitch control is explicitly out of scope.
3. **Pan inputs:** keys + middle-mouse only. **Screen-edge push panning is out of scope** for M4b (fiddly in a windowed Editor, extra tuning surface) — it can be added later without changing the model.
4. **Architecture:** a thin **`FreeLookController`** MonoBehaviour drives a **pure `FreeLookRig`** in `Domain/`. The rig owns the orbit state + all clamping and is unit-tested; the MonoBehaviour handles input polling and transform writes. This matches the codebase's spine (`HypeCalculator`, `EconomyCalculator`, `CrowdFill`, `UpgradePricing` are all pure + tested) and keeps `CameraRig` a dumb tweener.
5. **Authored fly-to always wins:** free-look runs only when **`intermission && !CameraRig.IsTweening`**. Every authored move goes through `CameraRig.FlyTo`, which sets the tweening flag; free-look ignores input while tweening and **re-seeds from the landed pose** when the tween finishes.
6. **Off during live sets:** `FreeLookController` subscribes to the set lifecycle on the `EventBus` (`SetStarted` → hard off, `SetEnded` → back on), the same self-contained pattern as `HypeSystem`.

## Scope

**In scope (M4b):**
- A pure `Domain/FreeLookRig` (state + pan/orbit/zoom/resolve/seed + clamps) with unit tests.
- A `Unity/FreeLookController` MonoBehaviour: input polling, UI-hover gating, transform writes, lifecycle enable/disable, re-seed after authored fly-tos.
- `CameraRig`: add `IsTweening`; remove the now-dead `SnapHome`/`ReturnHome`/`_homePos`/`_homeRot` (flagged unused in the M4a review).
- `GameBootstrap`: optional serialized `FreeLookController` ref, initialized after the other systems.
- `FestivalGroundSetup` (editor builder): add + seed `FreeLookController` on the Main Camera from the ground extents and survey pose.
- SETUP.md section: build steps, verification, tuning.

**Out of scope (M4b):**
- Screen-edge push panning.
- Pitch/tilt control.
- The full structure roster and any new effect types — passive cash, multipliers, per-ability bonuses (M4c).
- A second theme and the art-fidelity pass (M4d).

## The pure model — `Domain/FreeLookRig`

Plain C# (netstandard2.1, no UnityEngine). Domain cannot reference `Vector3`/`Quaternion`, so the rig operates on floats and returns a small pose struct; the MonoBehaviour converts.

**State:**
- `focusX, focusZ` — the point on the ground plane the camera looks at.
- `yaw` — degrees, `0..360`, wraps.
- `distance` — camera distance from the focus (zoom).
- `pitch` — degrees, **held** (adopted on seed, never changed by input).
- `focusY` — the ground-plane height the focus/pan lives on (config; typically 0).

**Config / bounds (passed in at construction, sourced from serialized fields):**
- `minX, maxX, minZ, maxZ` — the pan rectangle (ground extents).
- `minDistance, maxDistance` — zoom clamps.
- `panDistanceFactor` — pan step scales with `distance` so panning feels consistent zoomed out (a small multiplier; Civ does this).

**Operations (pure; each mutates state then clamps):**
- `Pan(right, forward)` — the 2D input vector is rotated by the current `yaw` into world XZ (screen-relative pan), scaled by `panDistanceFactor * distance`, added to `focusX/Z`, then clamped to the pan rectangle.
- `Orbit(deltaYaw)` — `yaw = Wrap360(yaw + deltaYaw)`.
- `Zoom(delta)` — `distance = Clamp(distance - delta, minDistance, maxDistance)` (positive scroll zooms in).
- `Resolve()` → `CameraPose` — from `focus + yaw + distance + pitch`, compute the camera world position (a spherical offset up-and-back from the focus) and the look angles.
- `SeedFrom(CameraPose pose)` — adopt an arbitrary camera pose: project the pose's view ray onto the ground plane (`y = focusY`) to recover `focusX/Z`, back out `yaw`/`pitch` from the look angles and `distance` from the focus-to-camera length, then clamp `focus`/`distance` into bounds. Called whenever free-look regains control after an authored fly-to, so it continues seamlessly from wherever the camera landed (normally the survey overview).

**Return struct:** `readonly struct CameraPose { float PosX, PosY, PosZ, Yaw, Pitch; }`. The MonoBehaviour builds `new Vector3(PosX,PosY,PosZ)` and `Quaternion.Euler(Pitch, Yaw, 0)`.

**Math notes for the implementer (confirm during implementation):**
- Spherical resolve: horizontal radius `r = distance * cos(pitch)`, height `distance * sin(pitch)`; camera sits at `focus + (-sin(yaw)*r, height, -cos(yaw)*r)` (behind the focus for the given yaw) — pick the convention so that increasing `yaw` orbits the expected direction and `Resolve` looks *toward* the focus. Lock the sign conventions with the round-trip test below.
- `SeedFrom` is the inverse of `Resolve`; the round-trip test (`SeedFrom(Resolve(state))` reproduces `state`) is the guard that the conventions match.

## The MonoBehaviour — `Unity/FreeLookController`

On the **Main Camera**, alongside `CameraRig`.

**Serialized fields (bounds/speeds, tunable in Inspector, seeded by the editor builder):**
- Pan rect `minX/maxX/minZ/maxZ`, `minDistance/maxDistance`, `focusY`.
- `panSpeed` (units/sec), `orbitSpeed` (deg per pixel of mouse delta), `zoomSpeed` (units per scroll notch), `panDistanceFactor`.

**Refs:**
- `CameraRig rig` — to read `IsTweening`.

**Lifecycle:**
- `Initialize(EventBus bus)` — store the bus; subscribe `SetStarted`/`SetEnded`. Called by `GameBootstrap`.
- `OnDestroy` — unsubscribe.
- `_intermission` flag: `false` on `SetStarted`, `true` on `SetEnded`. Starts `true` (the game opens in intermission) — confirm against `GameBootstrap`/`SetController` start state during implementation.

**Per-frame `Update`:**
1. If `!_intermission || rig == null || rig.IsTweening` → do nothing (this is where "authored fly-to wins" and "off during live sets" fall out).
2. If the rig was tweening last frame and isn't now (just regained control), build a `CameraPose` from the camera's current transform and call `_rig.SeedFrom(...)` before reading input.
3. Read input:
   - Keyboard `W/A/S/D` + arrows → a `(right, forward)` vector → `Pan(...)` (scaled by `panSpeed * Time.unscaledDeltaTime`).
   - If **not** over UI (`EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()`):
     - Middle-mouse held (`Mouse.current.middleButton.isPressed`) → `Mouse.current.delta` × `orbitSpeed` → `Orbit(deltaX)`.
     - `Mouse.current.scroll.y` (nonzero) × `zoomSpeed` → `Zoom(...)`.
4. `var pose = _rig.Resolve();` → write `transform.SetPositionAndRotation(...)`.

Uses `Time.unscaledDeltaTime` to match `CameraRig`'s tweens. Null-guards `Mouse.current`/`Keyboard.current` (they can be null with no device), same as `AbilitySystem`/`DebugHud`.

**Local `FreeLookRig` instance:** the controller constructs its `FreeLookRig` from the serialized bounds in `Awake`/`Initialize`, and re-seeds it on first activation (step 2) so its initial state matches the live camera rather than a default.

## `CameraRig` change

- Add `public bool IsTweening => _tweening;`.
- Remove `SnapHome()`, `ReturnHome()`, and the `_homePos`/`_homeRot` fields (dead since M4a replaced snap-home-on-set-start with the two-pose model; flagged unused in the M4a whole-branch review). `Awake` becomes empty and can be removed. Nothing references them (`UpgradePreviewController.ReturnHome` routes to `GoToSurvey`, not the rig).

## Wiring

**`FestivalGroundSetup` (extend "Build Festival Ground"):**
It already resolves the Main Camera, the enlarged ground, and the survey pose. Extend it to also:
- Add `FreeLookController` to the Main Camera (`GetComponent ?? AddComponent`).
- Wire its `rig` ref to the camera's `CameraRig`.
- Seed bounds from the ground: pan rect = the ground's XZ extents (with a small inset); `focusY` = ground height.
- Seed `minDistance/maxDistance` and default `panSpeed/orbitSpeed/zoomSpeed/panDistanceFactor` around the survey pose's distance-to-ground (so the survey overview sits mid-zoom).
- Idempotent, using the same `SetRef`/`SetVector3` helpers as the rest of the builder.

**`GameBootstrap`:**
- Add `[SerializeField] private FreeLookController freeLook;` (optional; **not** in the null-guard, same as `preview`/`builds`).
- After the other systems initialize: `freeLook?.Initialize(Bus);`.

## Testing

**`tests/PitTycoon.Domain.Tests/FreeLookRigTests.cs`:**
- Pan clamps focus to the rectangle (pan hard in one direction, assert focus pinned at the bound).
- Pan is screen-relative: with `yaw = 90°`, `Pan(0, forward)` moves focus along the expected world axis.
- Zoom clamps at `minDistance` and `maxDistance`.
- Yaw wraps past 360 and below 0.
- `Resolve()` produces the expected position for a known `focus/yaw/distance/pitch` (locks the sign conventions).
- **Round-trip:** `SeedFrom(Resolve(state))` reproduces `state` (within float tolerance) for several states — the guard that resolve/seed are true inverses.

Target: existing **73 pass** stays green; new suite adds ~7 → **~80 passing**. This is the only automated gate; the `FreeLookController` (input, transform writes, lifecycle, UI gating) is verified in the Editor play-test checkpoint.

## Verification checklist (Editor checkpoint)

- Enter intermission → `W/A/S/D`/arrows pan across the ground, clamped at the edges (can't roam off the map).
- Middle-mouse drag orbits the yaw; scroll zooms between the min/max; tilt stays fixed.
- Scrolling while the pointer is over the shop panel scrolls the list, **not** the camera.
- Select a build spot → camera flies to it (free-look yields); on arrival you can orbit/pan/zoom around it; **⌂ Overview** flies back to the survey pose.
- **Start Next Set** → camera flies to the live pose and free-look is dead (no pan/orbit/zoom during the set).
- Set ends → back to survey, free-look live again.
- M1–M4a regression: hype/economy/abilities/upgrades/build spots all still work; F1 overlay intact.

## Tuning knobs

- `FreeLookController`: pan rect, min/max distance, `panSpeed`, `orbitSpeed`, `zoomSpeed`, `panDistanceFactor` — all in the Inspector, live-editable in Play.
- Survey pose (on `UpgradePreviewController`) still frames the default overview; free-look seeds from it.
