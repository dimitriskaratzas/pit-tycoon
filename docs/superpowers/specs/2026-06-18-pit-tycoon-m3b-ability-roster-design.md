# Pit Tycoon — Milestone 3b Design (Ability Roster)

**Date:** 2026-06-18
**Status:** Approved (design), pending spec review
**Scope:** M3b only — generalize the single hardcoded ability into a data-driven, purchasable roster, and add two new click-fired abilities. The other M3 pieces (M3a upgrades, M3c tracks, M3d shop/UI polish) are separate sub-milestones.
**Source brief:** `pit-tycoon-claude-code-brief.md` (Purchases — B. Active abilities).
**Predecessors:** M1 loop + M2 art trilogy (PRs #2–#6 merged).

## 1. Goal

Turn the one hardcoded whirlpool into a **data-driven ability roster**: abilities are bought in the intermission shop, then fired during the set (each with its own cooldown), all scored on-beat through the already-tested `BeatGrid` + `BeatWindow`. Ship the generalized system + two new abilities (Light-burst, Woofer) so adding a 4th/5th is just a new ScriptableObject.

**Non-negotiable rule (the brief's bar):** every ability must read as a *crowd-energizing burst* — firing it visibly jolts the crowd and spikes hype. The whirlpool's existing feel (Space, beat anticipation, PERFECT/GOOD/MISS) must be preserved.

## 2. M3 decomposition (context)

M3 is split into independently shippable sub-milestones; this is the first:
- **M3b (this)** — ability roster.
- **M3a** — the other three passive upgrades (Stage, Lighting, PA), scaling the M2b geometry.
- **M3c** — multiple bundled tracks + selection.
- **M3d** — shop/intermission UI polish (after the content exists).

## 3. Architecture

On-beat + cooldown logic lives in the **pure-C# Domain layer** (unit-tested); the Unity layer is a thin adapter. This replaces `WhirlpoolAbility` (the single hardcoded MonoBehaviour) with one general system.

- **`Ability` (Domain, pure C#, TDD).** Constructed from a definition's values (`BaseSpike`, `MaxMultiplier`, `ToleranceSeconds`, `Cooldown`, `Cost`, `OwnedFromStart`). State: `CooldownRemaining`, `Owned`. API:
  - `void Tick(double dt)` — decays cooldown.
  - `bool CanFire` — owned && cooldown elapsed.
  - `FireResult Fire(double now, double nearestBeatDspTime)` — if `CanFire`, computes `multiplier = BeatWindow.Multiplier(nearestBeat, now, tolerance, maxMultiplier)`, `hypeAdded = baseSpike * multiplier`, a `HitQuality` (Perfect/Good/Miss from the normalized on-beat), starts the cooldown, and returns the result; otherwise returns a "not fired" result.
  - `bool Unlock(...)` — marks owned (the spend itself stays in the Unity `EconomySystem`).
  - Reuses the existing `BeatWindow`. New unit tests cover: cooldown gating, owned gating, on-beat multiplier, quality bands.
- **Shared `BeatGrid`.** One instance owned by the Unity `AbilitySystem`, fed by `BeatDetected`; all abilities score against the same tempo grid (today the whirlpool owns its own — this de-duplicates it).

## 4. AbilityDefinition (extended)

Add to the existing SO:
- `int Cost` — shop price (whirlpool 0).
- `bool OwnedFromStart` — whirlpool true; others false.
- `Trigger trigger` — enum `{ Spacebar, Button }` (whirlpool = Spacebar/rhythm; new ones = Button/click).
- `VfxKind vfx` — enum `{ Whirlpool, LightBurst, Woofer }` mapping to the spawn.
- `Color hudColor` — the ability's HUD tint.

Three assets: **Whirlpool** (Spacebar, owned, free, Whirlpool VFX), **Light-burst** (Button, cost, LightBurst VFX), **Woofer** (Button, cost, Woofer VFX).

## 5. Unity `AbilitySystem` (replaces WhirlpoolAbility)

- Holds the Domain `Ability` list (one per serialized `AbilityDefinition`) + the shared `BeatGrid`; live-gated via `SetStarted`/`SetEnded`.
- **Input:** Space fires the `Spacebar`-trigger ability; each owned `Button` ability fires via a HUD button **and** a number-key hotkey (1, 2, …).
- **On fire:** publish `AbilityFired(id, multiplier, hypeAdded)` (the existing `HypeSystem` already applies `HypeAdded` generically — no HypeSystem change), spawn VFX by `VfxKind`, and call `CrowdController.Pop(strength)` so the crowd visibly jolts.
- **Purchase:** `TryUnlock(AbilityDefinition)` during intermission — `EconomySystem.TrySpend(cost)` then mark the Domain `Ability` owned; publishes `AbilityUnlocked(id)` (new event).
- Exposes per-ability `CanFire`, `CooldownRemaining`, `Owned`, `LastQuality` for the HUD.

## 6. VFX + crowd reaction

- **Whirlpool** → existing `WhirlpoolVfx`. **Woofer** → existing `ShockwaveVfx` (low, wide, at the crowd centre). **Light-burst** → new `LightBurstVfx` (a brief code-only screen flash; player-triggered so it doesn't hit the every-beat seizure concern that parked the M2c impact frames).
- **`CrowdController.Pop(float strength)`** — a new public method that injects a one-shot bump into the existing pop value, so any ability fire jolts the crowd (the visible-lift bar).

## 7. HUD (DebugHud, minimal)

- **Intermission:** an ability row with **buy** buttons (cost, disabled if unaffordable, "Owned" when bought) alongside the existing upgrade buy.
- **Live:** **fire** buttons for owned abilities with cooldown readouts + PERFECT/GOOD/MISS, replacing the single hardcoded whirlpool button.
- This is functional IMGUI; the real UI pass is M3d.

## 8. Wiring

- `GameBootstrap`: replace the `WhirlpoolAbility ability` field with `AbilitySystem abilities`; `abilities.Initialize(Bus)`.
- Editor setup (extend `PitTycoonSetup` / `FestivalSceneSetup`): create the three `AbilityDefinition` assets, add `AbilitySystem` to `Systems`, wire the definition list + `CrowdController`/`vfxAnchor` refs, and update `DebugHud`'s reference.
- `WhirlpoolAbility.cs` is removed (its behaviour is now the Whirlpool definition + the general system).

## 9. Verification

- **Domain `Ability` unit tests** (TDD) — added to the suite (currently 50 green).
- **Manual in-editor checkpoint:** whirlpool still feels identical on Space; buying Light-burst/Woofer in intermission makes them fireable; firing pops the crowd + spikes hype + shows the right VFX; cooldowns + on-beat quality read correctly; M1 loop intact.

## 10. Out of scope

- M3a passive upgrades, M3c tracks, M3d shop/UI polish.
- Spatial/local hype (all abilities add to the single global hype; "local vs global" is cosmetic for now).
- New ability VFX beyond the three `VfxKind`s.

## 11. Risks & open questions

- **Refactor risk** — replacing `WhirlpoolAbility` must preserve its exact feel; the Domain `Ability` + shared `BeatGrid` reproduce the anticipation + quality bands, covered by tests + the checkpoint.
- **Light-burst flash comfort** — keep it brief and not pure-white; it's player-triggered and cooldown-gated, but still tune for comfort at the checkpoint.
- **Input collision** — number-key hotkeys must use the New Input System (`Keyboard.current.digitNKey`), consistent with the existing Space handling.
- **Editor re-wiring** — swapping the serialized ability reference on `GameBootstrap`/`DebugHud` is an editor step; the setup script handles it, with a `SETUP.md` note.
