# Pit Tycoon — UI Foundation (sub-project A) Design

**Date:** 2026-06-25
**Status:** Approved (brainstorm) — ready for implementation plan
**Milestone:** "Real UI" — sub-project **A** of two. Sub-project **B** (camera + ghost-preview) is a separate spec that builds on this.

## Goal

Replace the IMGUI `DebugHud` player-facing UI with a proper **UGUI + TextMeshPro** interface — a live-set HUD and an intermission shop — built by an editor script and bound to the existing systems through their public APIs. No new game logic; this is a presentation layer over the working hype → cash → upgrade loop.

## Scope

**In scope (A):**
- UGUI Canvas with a **live-set HUD** and an **intermission shop side panel**.
- Editor builder script that constructs and wires the entire canvas.
- `GameBootstrap` wiring so the UI initializes with the other systems.
- `DebugHud` stripped to a dev-only overlay behind an F1 toggle.

**Out of scope (deferred to sub-project B):**
- Camera rig / fly-to-spot on upgrade selection.
- Ghost preview of an upgrade before purchase, with revert on cancel.
- Two-step (select → preview → confirm) purchase flow.
- Preview/revert hooks in `VenueController` / `CrowdController`.

**Out of scope (later polish):**
- Halftone panel textures, speed-line accents, animated manga pop-ins, onomatopoeia tie-ins.

> The shop panel is designed **docked to one side with no screen dimming** specifically so sub-project B can later fly the camera to the venue and show a ghost preview while the panel stays open. This milestone ships **immediate buy-on-click** (no preview yet).

## Decisions (locked during brainstorm)

1. **UI tech:** UGUI + TextMeshPro (`com.unity.ugui` 2.0 already installed). Chosen over UI Toolkit for trivial editor-script construction and comic styling via `Image`.
2. **DebugHud:** kept as a toggle-able dev overlay (F1, default off) showing dev-only numbers (intensity, raw beat strength, last-beat dsp). Player-facing IMGUI removed.
3. **Live HUD layout:** hype bar **top-center** (the original layout), set+phase top-left, cash top-right, ability bar bottom-center, hit-quality popup above the ability bar.
4. **Intermission shop:** **side panel docked right**, auto-height, **no dimming** (scene stays fully visible). Contents: cash, banked-this-set callout, upgrade rows, ability-unlock rows, Start Next Set button.
5. **Binding:** hybrid — continuous values polled per frame; discrete changes via `EventBus`.
6. **Milestone split:** UI foundation (this spec) first; camera + ghost preview second.

## Architecture

A UGUI Canvas driven by a small set of focused MonoBehaviour view components in `Assets/PitTycoon/Unity/UI/`. All consume the **existing public APIs** of the systems — no system rewrites.

### Components

**`HudController`** (top-level)
- `Initialize(EventBus bus, HypeSystem hype, EconomySystem economy, SetController setController, AbilitySystem abilities, UpgradeSystem upgrades)` — called by `GameBootstrap`.
- Holds references to `LiveHudView` and `ShopView`.
- Subscribes to `SetStarted` (show Live, hide Shop) and `SetEnded` (show Shop, push banked amount).
- Unsubscribes in `OnDestroy`.
- Owns no widget logic itself — just view lifecycle/toggling.

**`LiveHudView`**
- **Hype bar:** background `Image` (full width = the current ceiling) + fill `Image` (`type = Filled`, horizontal) driven each frame by `hype.HypeFraction`; TMP `cur / ceiling` text from `hype.Current` / `hype.Ceiling`. A thin **peak marker** `Image` positioned at `hype.Peak / hype.Ceiling` shows the set's best-so-far (cash banks off peak, so this is direct feedback).
- **Cash:** TMP text polled from `economy.Cash`.
- **Set + phase:** TMP text from `setController.SetNumber` / `setController.Current`.
- **Ability bar:** one button per **owned** ability, cloned at runtime from an ability-button template, built from `abilities.Abilities` + `abilities.DefinitionOf(a)`. Each button: icon/label tinted with `def.HudColor`, hotkey label, and a cooldown overlay `Image` (`type = Filled`) driven by `a.CooldownRemaining` / `def.Cooldown`; disabled when `!a.CanFire`. Click → `abilities.TryFire(a)`.
- **Hit-quality popup:** subscribes to `AbilityFired` on the bus; reads `abilities.LastQuality`; shows PERFECT/GOOD/OK with a color, fades out over time.
- **Beat pulse:** subscribes to `BeatDetected` on the bus; pulses a HUD element by `beat.Strength`, decays per frame.
- Rebuilds the ability buttons on `SetStarted` (picks up any abilities unlocked during the preceding intermission; the live view is hidden during intermission so no mid-shop rebuild is needed).

**`ShopView`** (intermission side panel, docked right, no dimming)
- **Cash** TMP from `economy.Cash`; **banked-this-set** callout from the `SetEnded.CashEarned` pushed by `HudController`.
- **Upgrade rows:** cloned from a shop-row template, one per `upgrades.Upgrades`. Each row: `def.DisplayName`, `LvN` from `upgrades.LevelOf(u)`, cost from `upgrades.CurrentCost(u)`, greyed/disabled when `!upgrades.CanAfford(u)`. Click → `upgrades.TryPurchase(u)`.
- **Ability-unlock rows:** one per un-owned `AbilityDefinition` (via `abilities.Abilities` + `DefinitionOf` where `!a.Owned`): `def.DisplayName`, `def.Cost`, greyed when `!abilities.CanAfford(def)`. Click → `abilities.TryUnlock(def)`.
- **Start Next Set** button → `setController.StartNextSet()`.
- Refreshes all row states (cost/level/afford) on `UpgradePurchased` and `AbilityUnlocked`, and when the panel is shown.

### Editor builder

**`HudSetup`** in `Assets/PitTycoon/Unity/Editor/` — menu `Pit Tycoon → Build HUD`. Idempotent (find existing UI root by name, destroy + rebuild), matching `FestivalSceneSetup`/`PitTycoonSetup`.

Constructs:
- **Canvas** (Screen Space – Overlay) + **CanvasScaler** (Scale With Screen Size, reference 1920×1080, match width/height 0.5) + **GraphicRaycaster**.
- **EventSystem** with **`InputSystemUIInputModule`** — **not** `StandaloneInputModule`. (Project is Input-System-only; the legacy module silently never raises UGUI clicks.)
- Hype bar (filled fill image + ceiling tick), TMP texts (cash, set/phase, hype numbers), an **ability-button template** and a **shop-row template** (stored disabled and referenced by the views, which `Instantiate` them at runtime), the docked shop panel, the hit-quality popup, the beat-pulse element.
- Adds `HudController` + `LiveHudView` + `ShopView`, wires their serialized refs via `SerializedObject.FindProperty`, and sets the new `hud` reference on `GameBootstrap`.

### `GameBootstrap` changes

- Add `[SerializeField] private HudController hud;` to the field list and the existing null-guard.
- In `Awake`, after the systems are initialized, call:
  `hud.Initialize(Bus, hype, economy, setController, abilities, upgrades);`
- Add nothing to `OnDestroy` (views unsubscribe themselves).

### `DebugHud` changes

- Remove the player-facing IMGUI (hype bar, ability buttons, intermission shop, cash/set readouts).
- Keep only dev readouts: intensity bar, beat box + strength, last-beat dsp.
- Gate `OnGUI` behind a toggle flipped by **F1** (`Keyboard.current.f1Key.wasPressedThisFrame`), default **off**.

## Data flow

```
GameBootstrap.Awake()
  -> systems initialize (economy, hype, crowd, abilities, upgrades, setController, beatVfx)
  -> hud.Initialize(Bus, hype, economy, setController, abilities, upgrades)
     -> HudController subscribes SetStarted/SetEnded
SetController.Start() -> publishes SetStarted(1)
  -> HudController shows LiveHudView, hides ShopView
  -> LiveHudView builds ability buttons for owned abilities
[live] LiveHudView reads hype/cash/cooldowns each frame; bus events drive popup + beat pulse
SetController end-of-track -> SetEnded(peak,avg,earned)
  -> HudController shows ShopView with banked callout; LiveHudView hidden
[intermission] ShopView buy buttons -> TryPurchase/TryUnlock; rows refresh on UpgradePurchased/AbilityUnlocked
  Start Next Set -> setController.StartNextSet() -> SetStarted(n) -> back to live
```

## Error handling & gotchas

- **Input System UI module** (above) — the single most likely "buttons don't work" trap.
- **TMP essentials**: first TMP use requires `Window → TextMeshPro → Import TMP Essential Resources` (one-time, manual — documented in SETUP).
- Views null-guard their refs and log once if unwired (consistent with existing systems).
- `EventSystem` must exist exactly once; the builder creates it if absent.
- Ability/upgrade row lists are dynamic — always built from the live system collections, never hard-coded counts.

## Testing & verification

- **No Domain logic added.** Run `dotnet test PitTycoon.Domain.slnx` to confirm the **73** tests stay green (regression guard only; no new unit tests — this layer is pure UGUI presentation).
- **Unity side** is not compiled here; correctness is by code review + the manual checkpoint.
- **Manual checkpoint (in-Editor, by the dev):**
  1. `Pit Tycoon → Build HUD`; import TMP essentials if prompted.
  2. Play. Live HUD: hype bar tracks hype, ceiling tick correct, cash/set/phase update.
  3. Owned-ability buttons appear, fire on **click** and on **hotkey**, show cooldown fills, disable while cooling.
  4. Hit-quality popup shows on fire; beat pulse pulses on beats.
  5. At set end: shop docks right, **scene still visible (no dimming)**, banked callout correct.
  6. Upgrade + ability rows: buy works, grey when unaffordable, refresh after purchase; Start Next Set returns to live.
  7. **F1** toggles the dev overlay; M1/M2/M3 behavior intact (abilities, VFX, coins, venue scaling, crowd fill).

## File structure

**New (Unity runtime):**
- `Assets/PitTycoon/Unity/UI/HudController.cs`
- `Assets/PitTycoon/Unity/UI/LiveHudView.cs`
- `Assets/PitTycoon/Unity/UI/ShopView.cs`

**New (Editor):**
- `Assets/PitTycoon/Unity/Editor/HudSetup.cs`

**Modified:**
- `Assets/PitTycoon/Unity/GameBootstrap.cs` — `hud` ref + `Initialize` call.
- `Assets/PitTycoon/Unity/DebugHud.cs` — strip to dev overlay + F1 toggle.
- `SETUP.md` — Build HUD step, TMP essentials note, verification checklist.

## Notes for the implementer

- Commit messages: imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- Keep views decoupled: read system public state + subscribe to bus events; never reach across to another system's internals.
- The shop panel layout must not dim/overlay the scene — sub-project B depends on the scene staying visible behind it.
- All system read APIs already exist (`HypeSystem.HypeFraction/Current/Ceiling`, `EconomySystem.Cash`, `SetController.SetNumber/Current`, `AbilitySystem.Abilities/DefinitionOf/LastQuality/CanAfford/TryFire/TryUnlock`, `UpgradeSystem.Upgrades/LevelOf/CurrentCost/CanAfford/TryPurchase`) — confirm against source before use, don't add to them unless a gap is found.
