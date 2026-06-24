# Pit Tycoon — Crowd Dynamics Rework Design

**Date:** 2026-06-24
**Status:** Approved (design), pending spec review
**Scope:** Rework the crowd from a fixed grid that grows only on purchase into an **organic pit that fills toward venue capacity as hype climbs**, with a **persistent following** that ratchets up across sets and **feeds the hype rate** (more crowd → faster hype → more cash). Grounds is repurposed from "add crowd rows now" to "raise venue capacity."
**Source brief:** `pit-tycoon-claude-code-brief.md` — Grounds upgrade ("more crowd slots = more total income") + the core-loop "next set starts bigger" intent.
**Predecessor:** Deferred item from `2026-06-19-pit-tycoon-m3a-passive-upgrades-design.md` §9.

## 1. Goal

Make the crowd **emerge from the music and the venue** instead of stepping up only when you buy the Grounds upgrade. Within a set the pit fills toward capacity as hype rises; the people who showed up **stay** as a persistent following; how full the pit is **multiplies hype gain**, so a bigger, fuller crowd earns faster. Grounds raises the capacity the pit fills toward.

**Non-negotiable rule (the brief's bar):** every change is visible on screen — the pit visibly packs from the stage outward as a set builds, the crowd stays between sets, and buying Grounds shows new (initially empty) room that fills over subsequent sets.

## 2. Locked decisions (from brainstorming)

- **Fill is hype-driven and ratchets up within a set** — it never ebbs on a hype dip (carried from M3a §9).
- **Persistent following** — the crowd that arrives **stays visible**; between sets they relax out of the mosh (later: heading to merch/food stalls) but do not despawn. The following floor only ever grows.
- **Fill feeds the economy (Option 1):** `FillFraction` multiplies the passive hype rate through a forgiving curve, so a fuller pit earns hype (and therefore cash) faster, but a sparse pit never fully stalls.
- **Render via a pre-built pool (Approach A):** all capacity members are instantiated once and scaled in/out; no instantiation mid-set (avoids GC hitches during hype spikes).
- **Out of scope:** the merch/food between-set economy, disk persistence/save-game, and festival-scale (thousands) crowd rendering. This rework only creates the persistent, visible population those would later build on.

## 3. Architecture

Three responsibilities, cleanly split, consistent with the existing Domain/Unity divide:

- **Domain `CrowdFill`** (pure C#, TDD) — the only new pure logic. Holds `Capacity`, `Following`, `Active`; advances `Active` toward `Capacity` from hype; banks `Active` into `Following` at set end; exposes `FillFraction`. No UnityEngine.
- **Domain interfaces `IHypeMeter` / `ICrowdMeter`** — decouple the mutual hype↔crowd dependency. `IHypeMeter { float HypeFraction }`, `ICrowdMeter { float FillFraction }`. Implemented by the Unity systems; consumers hold the interface, never the concrete type (preserves the codebase's "interfaces/events, not a web of singletons" rule).
- **Unity `CrowdController`** (reworked) — owns the `CrowdFill`, renders it as a pooled grid that scales members in front-to-back, reads hype via `IHypeMeter`, reacts to `SetStarted`/`SetEnded`, and accepts `RaiseCapacity` from `UpgradeSystem`. Implements `ICrowdMeter`.
- **Unity `HypeSystem`** (small change) — reads `ICrowdMeter.FillFraction` and applies it as a multiplier on the effective passive rate it feeds to `HypeCalculator`. Implements `IHypeMeter`.

## 4. Domain `CrowdFill`

Pure C#. Member counts are conceptually integers; `Active` is a `float` so the swell is smooth and the renderer rounds it.

State + constructor:
- `int Capacity`, `int Following`, `float Active`.
- `CrowdFill(int capacity, int initialFollowing)` — clamps `0 ≤ initialFollowing ≤ capacity`; `Active = Following`. Throws `ArgumentOutOfRangeException` if `capacity <= 0`.

Methods:
- `void BeginSet()` → `Active = Following`. (Start each set at the persistent floor.)
- `void Tick(float hypeFraction01)` → `float target = Following + (Capacity - Following) * Clamp01(hypeFraction01); Active = Max(Active, Min(target, Capacity));` Negative `hypeFraction` is clamped to 0 (no throw — it's a per-frame hot path fed a derived ratio).
- `void BankSet()` → `Following = Clamp(round(Active), 0, Capacity)`.
- `void RaiseCapacity(int delta)` → `if (delta < 0) throw; Capacity += delta;` (Following/Active unchanged → the added room is empty until filled.)

Properties:
- `int ActiveCount => (int)Round(Active)` (away-from-zero).
- `float FillFraction => Capacity > 0 ? Active / Capacity : 0f`.

Ratchet rationale: `HypeCalculator.Current` is monotonic non-decreasing within a set today, so `hypeFraction` only rises; the `Max` in `Tick` makes `CrowdFill` correct anyway if a future hype model allows decay.

## 5. Hype coupling

`HypeSystem` gains a serialized `[Range(0,1)] float minFillMultiplier = 0.4f` and an injected `ICrowdMeter`:

```
float fill = crowdMeter?.FillFraction ?? 1f;            // no meter wired → no penalty
float mult = Mathf.Lerp(minFillMultiplier, 1f, fill);
_calc.Tick(Time.deltaTime, analyzer.Intensity01, (passiveRatePerSecond + _rateBonus) * mult);
```

`HypeCalculator.Tick`'s signature is unchanged. `HypeSystem` implements `IHypeMeter.HypeFraction => Ceiling > 0 ? Current / Ceiling : 0f`.

`minFillMultiplier` is the only new tuning knob here: lower = stronger "warm-up" pressure (sparse pit earns little), higher = gentler. Default 0.4.

## 6. `CrowdController` rework

Serialized fields:
- Keep: `spacing`, `baseHeight`, `bounceHeight`, `beatPop`, `popDecayPerSecond`, `bobSpeed`, `memberMaterial`, `memberPrefab`, `rotationJitter`, `scaleJitter`.
- Replace `columns`/`rows` with: `int columns = 14` (layout width only), `int startingCapacity = 84`, `int startingFollowing = 24`, `float scaleInPerSecond = 3f` (how fast a member pops in).
- Remove the old `Grow(int,int)` / `MemberCount` grid semantics.

Owns `CrowdFill _fill = new CrowdFill(startingCapacity, startingFollowing)`.

Wiring (`Initialize`): keep the analyzer parameter, add an `IHypeMeter` and the `EventBus` (for set lifecycle):
`public void Initialize(IAudioAnalyzer analyzer, IHypeMeter hype, EventBus bus)`.

`Build()`:
- Destroys existing children, instantiates **`_fill.Capacity`** members.
- Layout: `rows = ceil(Capacity / columns)`; index members **front-to-back** (row 0 = nearest the stage), so the first N active members occupy the front rows. Within a row, fill center-out for a natural look (optional polish; left-to-right is acceptable).
- All members start at `localScale = 0` (hidden); a per-member current-scale array tracks the scale-in.

Per-frame `Update()`:
- Guard on `_analyzer`, `_members`, `_hype`.
- `_fill.Tick(_hype?.HypeFraction ?? 0f)`.
- `int active = _fill.ActiveCount;`
- For each member i: `targetScale = (i < active) ? memberFullScale : 0f`; move current scale toward target at `scaleInPerSecond` (MoveTowards); apply bob + `_pop` to `localPosition.y` **only when its scale > ~0.05** (inactive members stay parked at scale 0).
- `_pop` decay and `OnBeat`/`Pop` behavior are unchanged.

Lifecycle:
- Subscribe to `SetStarted` → `_fill.BeginSet()` and `SetEnded` → `_fill.BankSet()`. CrowdController takes only the analyzer today, so the `EventBus` is added to its `Initialize` (above); subscribe in `Initialize`, unsubscribe in `OnDestroy`, per the established pattern (the existing analyzer `BeatDetected` subscribe/unsubscribe already follows it).
- `RaiseCapacity(int delta)` → `_fill.RaiseCapacity(delta); Build();` (rebuild bigger; new slots appear empty). Called by `UpgradeSystem` during intermission.

Implements `ICrowdMeter.FillFraction => _fill.FillFraction`.

## 7. Grounds becomes capacity

- `UpgradeDefinition`: remove `AddColumns` / `AddRows`; add `[Min(0)] int AddCapacity`. `CeilingDelta` stays (Stage uses it); Grounds sets `CeilingDelta = 0`.
- `UpgradeSystem.TryPurchase`: replace `if (u.AddColumns > 0 || u.AddRows > 0) crowd?.Grow(...)` with `if (u.AddCapacity > 0) crowd?.RaiseCapacity(u.AddCapacity);`.
- `Upgrade_Grounds.asset` + `PitTycoonSetup.ConfigureUpgrade`: Grounds → `AddCapacity = 30`, `CeilingDelta = 0`, `AddColumns/AddRows` removed. Other three upgrades unchanged.

## 8. Wiring (GameBootstrap + editor)

- `GameBootstrap` already holds `crowd`, `hype`, `analyzer`, and the `EventBus`. Update the init sequence:
  - `hype.Initialize(bus)` (unchanged), then `hype.SetCrowdMeter(crowd)` injects the `ICrowdMeter`.
  - `crowd.Initialize(analyzer, hype, bus)` injects the analyzer, the `IHypeMeter`, and the bus.
  - Both objects exist before cross-wiring; the one-frame mutual lag (hype reads last frame's fill, crowd reads last frame's hype) is intentional and safe.
- No editor scene-builder changes are required for the cross-wiring if `GameBootstrap` does it in code (it holds both refs). `PitTycoonSetup` only changes for the Grounds asset fields.
- `DebugHud` upgrade list is unchanged (Grounds still appears with its escalating cost); optionally show `Crowd N/Cap` for tuning (nice-to-have, not required).

## 9. Verification

- **Domain `CrowdFill` unit tests (TDD), added to the suite (currently 61):**
  1. `BeginSet` sets `Active == Following`.
  2. `Tick(0)` keeps `Active == Following`; `Tick(1)` drives `Active == Capacity`.
  3. `Tick(0.5)` puts `Active` halfway between `Following` and `Capacity`.
  4. **Ratchet:** after `Tick(0.8)`, a `Tick(0.2)` does **not** reduce `Active`.
  5. `BankSet` promotes `Following` to the rounded `Active`.
  6. `RaiseCapacity` increases `Capacity` and allows `Active` to grow further on the next `Tick`; `Following`/`Active` unchanged by the call itself.
  7. `FillFraction == Active / Capacity`; clamps (Active never exceeds Capacity; `initialFollowing` clamped).
  8. Constructor rejects `capacity <= 0`; `RaiseCapacity` rejects negative delta.
- **Manual checkpoint:** start a set with a sparse pit → it packs from the stage outward as hype climbs; set ends → crowd stays visible (relaxed); next set starts at the fuller floor and grows higher; buy Grounds → venue gains visible empty room that fills over later sets; hype gain is visibly faster when the pit is full; M1 loop + M2 art + M3a/M3b rosters all intact.

## 10. Out of scope

- Merch / food / between-set stall economy (the population this enables is built here; the economy on top is not).
- Save-game / disk persistence of the following.
- Festival-scale crowd rendering (Approach C two-layer impostors) — only the modest pooled grid is built.
- Making fill the *sole* hype source (brainstorming Option 3) — fill multiplies the existing rate; it does not replace it.
