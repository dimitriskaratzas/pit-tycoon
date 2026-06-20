# Pit Tycoon — Milestone 3a Design (Passive Upgrade Roster)

**Date:** 2026-06-19
**Status:** Approved (design), pending spec review
**Scope:** M3a only — generalize the single hardcoded upgrade into the brief's full set of four, with repeatable escalating-cost levels, each visibly scaling the M2b venue. The organic hype-driven crowd model is a separate later pass (§9).
**Source brief:** `pit-tycoon-claude-code-brief.md` (Purchases — A. Passive venue upgrades).
**Predecessors:** M1 loop + M2 art trilogy + M3b ability roster (PRs #2–#8 merged).

## 1. Goal

Turn the one hardcoded "Grounds" upgrade into a **data-driven roster of all four passive upgrades** — Grounds, Stage, Lighting, PA — each buyable repeatedly at an **escalating cost**, each with a real mechanical effect (hype ceiling / rate / crowd) **and** a visible step-change to the M2b venue (the stage grows, lights brighten, PA grows, the crowd densifies).

**Non-negotiable rule (the brief's bar):** no invisible stat bumps — every purchase changes what's on screen.

## 2. Economy model (locked)

**Repeatable, escalating cost.** Each upgrade tracks a `Level` (purchase count). Cost of the next level = `round(BaseCost × CostGrowth^Level)`. Each purchase applies one level's worth of numeric effect + one step of visible scaling. Cash stays meaningful as sets get bigger.

## 3. Architecture

Three responsibilities, cleanly split:

- **Domain `UpgradePricing`** (pure C#, TDD) — `CostAtLevel(int baseCost, float growth, int level) → int`. The only new pure logic; unit-tested (level 0 = base, monotonic growth, rounding).
- **Unity `UpgradeSystem`** (generalized) — holds a `List<UpgradeDefinition>`, tracks each one's `Level`, computes the current cost via `UpgradePricing`, spends through `EconomySystem`, applies the numeric effects (`crowd.Grow`, `hype.RaiseCeiling`, `hype.RaiseRate`), tells the `VenueController` to scale, and publishes `UpgradePurchased`. Exposes `CurrentCost(def)`, `LevelOf(def)`, `CanAfford(def)` for the HUD.
- **Unity `VenueController`** (new) — holds the M2b scene instances (the `Stage`, `PA Left`/`PA Right`, the accent lights) and does the **visible scaling** per kind/level (`ApplyStage(level)`, `ApplyLighting(level)`, `ApplyPa(level)`). Keeps "what's on screen" out of `UpgradeSystem`.

## 4. UpgradeDefinition (extended)

Replace the current fixed fields with:
- `Id`, `DisplayName`, `UpgradeKind` enum `{ Grounds, Stage, Lighting, PA }`.
- `int BaseCost`, `float CostGrowth` (e.g. 1.6).
- Per-level numeric deltas (only the relevant ones set per kind): `int AddColumns`, `int AddRows` (Grounds), `float CeilingDelta` (Stage, Grounds), `float RateDelta` (Lighting, PA).

Four assets, one per kind.

## 5. Effects per kind (numeric + visible)

| Kind | Numeric effect | Visible change |
|------|----------------|----------------|
| Grounds | `crowd.Grow(cols, rows)` + `CeilingDelta` | denser/larger crowd (existing) |
| Stage | `hype.RaiseCeiling(CeilingDelta)` | `VenueController` scales the **Stage** instance up a step (taller stage + band) |
| Lighting | `hype.RaiseRate(RateDelta)` | brightens / adds **accent lights** (the brief's "bigger beat-flashes" is approximated by brighter lights in M3a; deeper per-beat flash scaling is later polish) |
| PA | `hype.RaiseRate(RateDelta)` | scales / adds **PA stacks** ("reaches more crowd" reads as more speakers) |

## 6. HypeSystem change

Add `RaiseRate(float delta)` + a runtime `_rateBonus` (starts 0) added to `passiveRatePerSecond` inside `Tick`. Today the rate is a fixed serialized value; Lighting/PA need to raise it.

## 7. HUD (DebugHud, minimal)

Intermission lists all four upgrades, each with a **buy** button showing the **current escalating cost** and **level** (`Lv N`), disabled when unaffordable. Functional IMGUI; the real shop UI is M3d.

## 8. Wiring

- `FestivalSceneSetup` creates `VenueController` on `Systems`, wires its `Stage` / `PA Left` / `PA Right` / accent-light refs from the instances it places.
- `PitTycoonSetup` creates the four `UpgradeDefinition` assets (one per kind, tuned costs/deltas) and wires `UpgradeSystem`'s list + the `VenueController` ref. `UpgradeSystem.Initialize(bus)` is unchanged.
- `DebugHud` reference stays `upgrades`; the buy UI iterates the list.

## 9. Deferred: organic crowd-dynamics rework (captured, not built here)

Per the brainstorming discussion, the crowd model will later change so crowd size **emerges** from venue draw + in-set hype rather than a direct purchase:
- The pit **fills toward a venue capacity, swelling within a set as hype climbs** (locked: **hype-driven**, ratcheting up, not ebbing on dips).
- **Grounds** will be reworked from "add crowd rows now" → "raise the venue capacity" the pit fills toward; the other three upgrades are unaffected.
- Likely a Domain `CrowdFill` (pure, tested) + a `CrowdController` change from fixed-grid to fill-to-capacity.

M3a intentionally keeps the current direct `crowd.Grow` model so the four-upgrade roster ships now; the rework is its own focused pass afterward.

## 10. Verification

- **Domain `UpgradePricing` unit test** (TDD) — added to the suite (currently 56).
- **Manual checkpoint:** all four upgrades appear in intermission; buying each spends cash, **escalates** the next cost, increments the level, and **visibly scales** its venue part (stage grows / lights brighten / PA grows / crowd densifies); hype ceiling + rate climb; M1 loop + M2/M3b intact.

## 11. Out of scope

- M3c tracks, M3d shop/UI polish.
- The organic crowd-dynamics rework (§9).
- Spatial/per-member reaction radius (PA coverage stays cosmetic + a rate bonus).
