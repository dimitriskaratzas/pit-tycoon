# Pit Tycoon — Crowd Layout & Beat Wave (Milestone 5, sub-project F) Design

**Date:** 2026-08-12
**Milestone:** "The Fidelity Pass" — sub-project **M5f**. M5a crowd figures, M5b main-stage refresh, and M5e lighting & atmosphere have shipped (M5e is PR #20). Revised order from here: **M5f → M5c → M5d.**

**Branch:** `feat/crowd-layout-m5f`, branched from the **M5e tip**, not from `master`. M5e is still an open PR, and branching from `master` would revert `Greybox.unity` to the pre-M5e daylight scene — the Editor checkpoint for this milestone would then be judged under lighting that is about to be replaced. Nothing in this milestone touches a file M5e touched, so the eventual merge is clean either way.

## Summary

The pit is a rigid 12-column lattice at uniform 1.2 m spacing, and it reads as one. Members vary only by ±18° rotation and ±12% scale; every position is exactly on grid. Beat pops fire on every member in the same frame, so the crowd bounces as one wall rather than as a wave rolling back from the stage.

M5f makes the pit read as a crowd: position jitter that grows with distance from the stage, rows that pack tight at the barrier and loosen toward the back, and a beat pop that travels backward through the pit instead of firing everywhere at once.

This is the **code half** of the crowd work. The other half — splitting the bodies into skin/shirt/legs/hair material slots and adding accessory meshes — is Blender work and gets its own milestone.

## Design decisions (locked during brainstorming)

1. **Code half only.** The crowd prefabs carry a single material slot, so the outfit tint necessarily paints skin, hair, and clothing the same colour. Fixing that means re-authoring four bodies in Blender and re-exporting, on the M5a pipeline with its per-batch Editor checks. Out of scope here.
2. **Jittered rectangle with density falloff**, not a wedge and not clumps. Keeps the existing `index → row/col` fill maths intact, which is what `CrowdFill` and the capacity upgrades are built on. A wedge would mean columns varying per row and a rewrite of that maths; clumps are hard to tune without being able to look at the result, and risk reading as patchy rather than intentional at low capacity.
3. **Layout is a pure function of member index, not a random draw.** This is forced by two existing bugs (below), and it is what makes the maths unit-testable without the Editor.

## The bugs this has to fix first

Adding jitter on top of the current implementation would make both of these visibly worse, so they are part of the work rather than follow-ups.

**Rebuilds reshuffle the whole crowd.** `Build()` draws `Random.Range` for rotation, scale, and pop jitter per member. `RaiseCapacity` calls `Build()`, so every capacity upgrade re-randomises every existing member — the crowd visibly resettles. Today that only shows as a small twitch in rotation and scale. With position jitter added, the entire pit would teleport on every purchase.

**Ghost previews would lie.** `PreviewCapacity()` duplicates the layout maths and applies no jitter at all. Ghost members sit exactly on grid; the real members that later replace them would land jittered. The preview would systematically not match what you get.

Both dissolve if position, rotation, and scale are derived from the member's index rather than drawn randomly: the same member lands in the same spot on every rebuild, and a ghost lands exactly where its real member will.

## Domain: `CrowdLayout`

New pure C# type in `Assets/PitTycoon/Domain/`, no UnityEngine reference.

Because the Domain assembly cannot reference `Vector3`, the per-member result is a small plain struct:

```csharp
public readonly struct CrowdSlot
{
    public float X { get; }
    public float Z { get; }
    public float RotationY { get; }
    public float Scale { get; }      // full-size scale, jitter already applied
    public float PopScale { get; }   // per-member beat-pop height factor
}
```

Two static methods:

- **`Slot(int index, CrowdLayoutSettings settings)` → `CrowdSlot`** — everything about where member `index` stands and how it is shaped. Deterministic: derived from an integer hash of the index, never from a random draw.
- **`PopHeight(int row, float secondsSinceBeat, float strength, float rowDelay, float decay)` → `float`** — the travelling wave. Returns `0` before the wave reaches that row, then a linearly decaying lift.

`CrowdLayoutSettings` is a plain struct carrying `columns`, `spacing`, `startRows`, `positionJitter`, `rowSpacingFalloff`, `rotationJitter`, and `scaleJitter`, so the two callers cannot drift apart on parameters.

### Layout maths

Row and column are unchanged: `row = index / columns`, `col = index % columns`. The front row stays pinned near the stage exactly as today, so the crowd does not drift away from the barrier as capacity grows.

**Row depth with falloff.** Instead of `row * spacing`, the gap between consecutive rows grows with distance: `gap(r) = spacing * (1 + rowSpacingFalloff * r)`. Cumulative depth has a closed form, so no loop is needed:

```
depth(row) = spacing * (row + rowSpacingFalloff * row * (row - 1) / 2)
```

At `rowSpacingFalloff = 0.06`, row 10 sits at roughly 1.3× the depth it would on a uniform grid, and the gap there is 1.6× the front gap. The barrier stays packed; the back thins out.

**Jitter that grows with row.** Two hash-derived values in `[-1, 1]` offset X and Z. The magnitude scales from a tight front to a loose back:

```
rowT  = min(1, row / max(1, startRows - 1))
amount = positionJitter * (0.35 + 0.65 * rowT)
```

so the front row keeps roughly a third of the jitter the back rows get. Jitter is expressed as a fraction of `spacing`, so it stays proportional if spacing is tuned.

**Rotation and scale** use the same hash with different salts, replacing today's `Random.Range` calls with no change in range or feel.

**The hash.** A small integer bit-mixer over `(index, salt)` returning `[0, 1)`. It must be deterministic across runs and platforms, which rules out `GetHashCode` on anything but the raw int, and it must decorrelate neighbouring indices — adjacent members are adjacent in the pit, so a weak hash would produce visible diagonal banding rather than scatter. This is precisely the kind of thing worth having tests for, since it cannot be eyeballed from outside the Editor.

### The travelling wave

```
arrival = row * rowDelay
age     = secondsSinceBeat - arrival
if (age < 0) return 0
return max(0, strength - age * decay)
```

Front rows lift first and the pop rolls backward through the pit. Linear decay matches the `Mathf.MoveTowards` feel the current single-`_pop` implementation already has, so the pop itself does not change character — only its timing across rows. Returning `0` before arrival is what makes the wave visible; without that guard every row fires on the same frame, which is the current behaviour.

## `CrowdController` changes

- `Build()` and `PreviewCapacity()` both call `CrowdLayout.Slot`. The duplicated layout maths in `PreviewCapacity` is deleted — one source of truth.
- Per-member `Random.Range` calls for rotation, scale, and pop jitter are replaced by the values on `CrowdSlot`.
- The single `_pop` float becomes one wave slot — a beat **timestamp** plus strength — and a per-member height that decays every frame and is re-raised once `CrowdLayout.PopHeight` says the wave has reached that member's row (row capped at `waveMaxRows`, so traverse time stays bounded as capacity grows). One slot is enough regardless of retrigger rate: members the newer wave hasn't reached yet just keep decaying from where they were. `Pop(float strength)` (the ability jolt) triggers the same wave, so abilities also read as a pulse travelling through the pit.
- New serialized knobs: `positionJitter`, `rowSpacingFalloff`, `waveRowDelay`, `waveMaxRows`. The wave's `decay` argument is the **existing** `popDecayPerSecond` field, which already means exactly that — a second decay knob would be the same number under two names.
- Existing `spacing`, `columns`, `rotationJitter`, `scaleJitter`, `beatPop`, and `popDecayPerSecond` keep their current meanings.

`FillFraction` / `ICrowdMeter`, the fill and scale-in logic, the Animator blend tree, and the hype maths are untouched.

## Out of scope

- Material slots, separate skin palette, accessories, hero props (crowd surfer, someone on shoulders) — the Blender half, its own milestone.
- Crowd LODs, GPU-instanced animation, changes to crowd count or capacity pricing.
- Beat-reactive venue geometry (M5c) and structure surface detail (M5d).

## Testing & verification

- `dotnet test PitTycoon.Domain.slnx` — the suite grows past its current 98 with real coverage of the layout: determinism (same index yields the same slot across calls), hash spread (neighbouring indices do not produce correlated offsets), row-depth monotonicity and falloff, jitter bounded by `positionJitter`, front-row jitter smaller than back-row, and the wave's arrival ordering, pre-arrival zero, and decay to zero.
- Editor checkpoint (the developer's step — the milestone cannot be visually verified from outside the Editor):
  1. No visible lattice from the default camera or from free-look.
  2. The pit is denser at the barrier and thins toward the back.
  3. A beat pop visibly travels backward from the stage rather than firing as one wall.
  4. Firing an ability sends the same travelling pulse.
  5. Buying a capacity upgrade adds new members **without** the existing crowd resettling.
  6. Ghost preview members stand where the real members appear after purchase.
  7. M1–M5e regression intact: hype builds, abilities fire, upgrades and build spots work, and the M5e night lighting and beams are unaffected.

## Tuning knobs

- `positionJitter`, `rowSpacingFalloff` — how organic and how front-weighted the pit reads.
- `waveRowDelay` — how fast the pop travels back through the pit. `0` reproduces today's everyone-at-once behaviour, which makes it easy to A/B the change in the Inspector.
- Existing `spacing`, `columns`, `rotationJitter`, `scaleJitter`, `beatPop`, `popDecayPerSecond` (the latter doubling as the wave's decay rate).
