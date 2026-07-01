# Pit Tycoon — Structure Roster + Economy (Milestone 4, sub-project C) Design

**Date:** 2026-07-02
**Milestone:** "The Festival Ground" — sub-project **M4c** (structure roster + economy) of four. Builds on M4a (open ground + build spots, PR #13) and M4b (free-look camera, PR #14), both merged.

## Summary

M4a proved the build-spot loop with two effect primitives (hype-rate, crowd-capacity). M4c fleshes out the **full structure roster (~10 spots)** and adds the **new economic effect types** the milestone was always aiming at: **passive cash income**, a **cash multiplier**, and a **targeted per-ability spike bonus**. The new numeric effects live in the pure Domain layer (`EconomyCalculator`, `Ability`) behind unit tests; the Unity side stays a thin passthrough plus three new cases in `BuildSystem`'s effect switch. Purely additive over M4a's architecture — no refactor of `VenueLayout`/`BuildSpot`/`BuildSystem`'s shape.

## Milestone context (the four sub-projects)

This design covers **M4c only**. The milestone was decomposed during brainstorming:

- **M4a — Ground & build-spot foundation** *(shipped, PR #13)*: enlarged ground, `VenueLayout`/`BuildSpot`/`BuildSpotController`/`BuildSystem`, build-spots as a shop category, 3 proof spots (hype-rate / capacity).
- **M4b — Free-look survey camera** *(shipped, PR #14)*: bounded Civ-style pan/orbit/zoom during intermission.
- **M4c — Structure roster + economy** *(this spec)*: the full ~10-structure roster plus new effect types (passive cash, cash multiplier, per-ability spike), with the numeric logic in Domain + tests.
- **M4d — Reskin/theme pass** *(later)*: a second world (indoor / underwater / space) + the real art-fidelity pass.

## Design decisions (locked during brainstorming)

1. **Roster scope:** a **fuller roster of ~10 structures**, **2+ per effect type**, keeping M4a's three existing spots and adding ~7 new ones.
2. **Per-ability bonus = targeted stronger spike:** each ability-spike structure boosts **one named ability's** hype spike (e.g. "Amp Stack: +50% Woofer"). Distinct identity per structure; the most legible on screen (visibly bigger spike).
3. **Effect model = Approach 1 (extend, don't refactor):** add three values to `BuildEffectKind`; keep one `effectMagnitude`; add **one** optional `string targetAbilityId` to `BuildSpot` (used only by the ability-spike effect). One effect per spot — variety comes from the ~10 count, not multi-effect spots (a combo model is a later option).
4. **Numeric logic in Domain:** passive income + cash multiplier go into `EconomyCalculator`; the spike bonus into `Ability`. Both unit-tested. The Unity layer is thin passthrough + `BuildSystem` switch cases.
5. **Payout formula:** `earned = round( (peakHype·peakWeight + avgHype·avgWeight) · CashMultiplier ) + PassiveIncome`. The **multiplier scales hype performance; passive cash is a flat top-up.** Multipliers stack additively (two +0.15 → ×1.30).
6. **Economy tuning stays raw:** M4c adds the effect *mechanisms* and the roster, not a rebalance of the tycoon curve (that remains a deliberately deferred future milestone). All magnitudes are starting values, tunable in the Inspector.

## Scope

**In scope (M4c):**
- Domain: `EconomyCalculator` passive-income + cash-multiplier; `Ability` spike-bonus. Unit tests for each.
- Unity passthroughs: `EconomySystem.AddPassiveIncome/AddCashMultiplier`; `AbilitySystem.AddSpikeBonus(id, pct)`.
- `BuildEffectKind` +3 values; `BuildSpot.targetAbilityId`; `BuildSystem` three new switch cases + an `abilities` ref.
- ~8 new greybox structure prefabs (`StructureGreyboxPrefabs`).
- `FestivalGroundSetup`: author the full ~10-spot roster (poses, costs, effects, `targetAbilityId`s) + wire `BuildSystem.abilities`.
- SETUP.md section.

**Out of scope (M4c):**
- Economy-curve rebalancing (deliberately deferred).
- Combo / multi-effect structures (Approach 2, a later option).
- New `ShopView` behaviour — it is effect-agnostic (label + cost rows); new spots appear automatically.
- The second theme and the art-fidelity pass (M4d).

## Domain changes (pure, unit-tested)

### `EconomyCalculator`
Add mutable state and accumulators; keep the existing `BankSet` signature so no caller changes.

```
public int PassiveIncome { get; private set; }      // starts 0
public float CashMultiplier { get; private set; }   // starts 1

public void AddPassiveIncome(int delta)   // delta >= 0; PassiveIncome += delta
public void AddCashMultiplier(float pct)  // pct >= 0; CashMultiplier += pct
```

`BankSet(peak, avg, peakWeight, avgWeight)` becomes:
```
double hype  = peak*peakWeight + avg*avgWeight;
int    earned = round(hype * CashMultiplier) + PassiveIncome;   // clamp >= 0
Cash += earned;  return earned;
```
Defaults (`CashMultiplier == 1`, `PassiveIncome == 0`) reproduce today's result exactly — existing `EconomyCalculatorTests` stay green.

**New tests:** passive adds a flat amount on top; multiplier scales the hype portion only; passive + multiplier stack correctly; negative `delta`/`pct` rejected (`ArgumentOutOfRangeException`, matching the class's existing guard style); defaults are a no-op vs. the old formula.

### `Ability`
Add a mutable spike bonus applied in `Fire`.
```
public float SpikeBonus { get; private set; }   // starts 1
public void AddSpikeBonus(float pct)             // pct >= 0; SpikeBonus += pct
```
`Fire`: `hypeAdded = _baseSpike * mult * SpikeBonus;` (multiplier/quality/cooldown logic unchanged).

**New tests:** default `SpikeBonus == 1` → `Fire` hype unchanged (regression-safe); `AddSpikeBonus(0.5)` → hype ×1.5; two bonuses stack (`+0.5` then `+0.5` → ×2.0); negative rejected.

## Effect model + Unity wiring

### Data (`VenueLayout.cs`)
```
public enum BuildEffectKind { HypeRate, Capacity, PassiveCash, CashMultiplier, AbilitySpike }

// BuildSpot gains:
public string targetAbilityId;   // used only by AbilitySpike; empty for other kinds
```

### `BuildSystem`
- New serialized ref: `[SerializeField] private AbilitySystem abilities;`
- `TryBuild`'s switch (after the existing spend-first `TrySpend`) gains:
```
case BuildEffectKind.PassiveCash:    economy.AddPassiveIncome(Mathf.RoundToInt(spot.effectMagnitude)); break;
case BuildEffectKind.CashMultiplier: economy.AddCashMultiplier(spot.effectMagnitude); break;
case BuildEffectKind.AbilitySpike:   abilities?.AddSpikeBonus(spot.targetAbilityId, spot.effectMagnitude); break;
```
The existing spend-first → apply-effect → `spots.Build(id)` → publish `StructureBuilt` ordering is preserved. (`economy` is guaranteed non-null by the existing guard; `abilities` is null-tolerant like the other optional refs.)

### Unity passthroughs
- `EconomySystem`: `public void AddPassiveIncome(int delta)` and `public void AddCashMultiplier(float pct)` → forward to `_calc`.
- `AbilitySystem`: `public void AddSpikeBonus(string abilityId, float pct)` — find the `Ability` with that id in its roster and call `AddSpikeBonus`; if no ability matches, log a warning and no-op (guards against a typo'd `targetAbilityId`). The exact ability ids (Woofer / Whirlpool / Light-Burst) are read from the `AbilityDefinition` assets during implementation and used verbatim in the layout.

### No `ShopView` change
`ShopView` lists build spots by label + cost and previews/buys them agnostic of effect kind; the new spots appear as rows automatically.

## Roster (~10 structures)

Keeps M4a's three; adds seven. Magnitudes are **starting values** (tuned in play; curve stays raw).

| Structure | Effect | Magnitude (start) | Target |
|---|---|---|---|
| Second Stage *(existing)* | HypeRate | +3 | — |
| Entrance Gate *(existing)* | HypeRate | +1.5 | — |
| Camping Field *(existing)* | Capacity | +40 | — |
| Grandstand | Capacity | +30 | — |
| Food Court | PassiveCash | +25 | — |
| Bar | PassiveCash | +15 | — |
| VIP Lounge | CashMultiplier | +0.15 | — |
| Sponsor Banner | CashMultiplier | +0.10 | — |
| Amp Stack | AbilitySpike | +0.5 | Woofer |
| Strobe Rig | AbilitySpike | +0.5 | Light-Burst |
| Speaker Wall | AbilitySpike | +0.5 | Whirlpool |

Coverage: HypeRate ×2, Capacity ×2, PassiveCash ×2, CashMultiplier ×2, AbilitySpike ×3 (one per ability). Poses are spread across the ~120u ground, inside the M4b free-look pan rect (±58); each has a camera fly-to pose for preview.

## Greybox prefabs

`StructureGreyboxPrefabs` gains ~8 new idempotent primitive builders (Grandstand, Food Court, Bar, VIP Lounge, Sponsor Banner, Amp Stack, Strobe Rig, Speaker Wall), each a small primitive composition readable in silhouette (a bar reads as a bar, an amp stack as speakers), same pattern/material as M4a's three. Real art is M4d.

## Editor builder

`FestivalGroundSetup` (the `Pit Tycoon → Build Festival Ground` menu) extends its authored spot array from 3 to the full ~10, ensures the new greybox prefabs, sets `targetAbilityId` on the three ability-spike spots, spreads the poses across the ground, and wires the new `BuildSystem.abilities` ref (to the scene's `AbilitySystem`). Idempotent; re-running rewrites `OpenAirLayout.asset`.

## Visibility ("if it isn't visible, it isn't done")

- **Structures physically rise** on the ground — the core spatial growth.
- **Passive cash + multiplier** flow through the existing set-end coin burst: `CoinFlyVfx.Burst(earned, …)` where `earned` now includes passive income and the multiplier, so visibly more coins fly.
- **Ability spike** shows as a visibly bigger hype spike plus the ability's existing VFX.

## Testing

- `dotnet test PitTycoon.Domain.slnx` is the only automated gate. It is **85 passing** today; M4c adds `EconomyCalculator` (passive/multiplier) and `Ability` (spike-bonus) tests → **~92 passing**, 0 failed.
- The roster, greybox prefabs, editor wiring, and on-screen visibility are verified in the manual Editor checkpoint (Unity compiles only in-Editor).

## Verification checklist (Editor checkpoint)

- Build **Food Court**/**Bar** → next set banks more cash even at the same hype (bigger coin burst); passive persists across sets.
- Build **VIP Lounge**/**Sponsor Banner** → the same hype yields more cash; two multipliers stack (×1.25).
- Build **Amp Stack** → the Woofer's hype spike is visibly bigger; **Strobe Rig** → Light-Burst; **Speaker Wall** → Whirlpool. Non-targeted abilities are unchanged.
- All ~10 structures appear as Build-shop rows, preview (ghost + camera fly-to), and rise on purchase; built state + effects persist across sets.
- Free-look (M4b) still frames every spot within bounds; M1–M4b regression intact (F1 overlay, upgrades, abilities, existing spots).

## Tuning knobs

- Per-spot `cost`, `effectMagnitude`, `targetAbilityId`, poses — on `OpenAirLayout` (Inspector).
- `EconomySystem.peakWeight`/`avgWeight` (existing) still govern the base hype→cash rate.
