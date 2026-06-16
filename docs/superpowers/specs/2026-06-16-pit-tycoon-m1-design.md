# Pit Tycoon — Milestone 1 Design (Greybox Vertical Slice)

**Date:** 2026-06-16
**Status:** Approved (design), pending spec review
**Scope:** Milestone 1 only. M2 (cel-shaded pipeline) and M3 (content) are explicitly out of scope until M1 is confirmed fun.
**Source brief:** `pit-tycoon-claude-code-brief.md`

## 1. Goal

A *fun, playable* set-based loop rendered entirely in primitives — no art:

1. **Live phase** (one track): a greybox crowd reacts to audio; a hype meter fills from passive rate + on-beat ability fires.
2. **Set end:** hype banks to cash (visible coins-fly), transition to intermission.
3. **Intermission:** spend cash on one passive upgrade that *visibly* changes the scene.
4. **Repeat:** next set is visibly bigger/denser.

**Non-negotiable on-screen rule:** the whole hype → cash → bigger-crowd chain must be visible. No invisible stat bumps; every purchase changes what's on screen.

## 2. Environment & constraints

- **Unity 6 LTS (6000.0.x)**, URP.
- Audio is **track-agnostic** for M1: the analyzer + a `TrackDefinition` SO accept any `AudioClip` dropped into `Assets/Audio/` later. No bundled track is assumed.
- Claude can author: C# scripts, ScriptableObjects, editor scripts, `SETUP.md`. Claude **cannot** operate the Editor (scenes, prefabs, serialized refs, package installs). Every Editor-only action is delivered as an ordered step in `SETUP.md`, and automated via an editor script wherever reasonable.
- Engineer is C#-fluent (backend), first-time Unity. Explain Unity lifecycle/gotchas; don't explain C# basics.

## 3. Architecture

### 3.1 Assembly / folder layout

```
Assets/PitTycoon/
  Domain/            asmdef: PitTycoon.Domain          — pure C#, NO UnityEngine reference
  Unity/             asmdef: PitTycoon.Unity           — references Domain; MonoBehaviours, SOs, FFT analyzer
  Tests/EditMode/    asmdef: PitTycoon.Tests.EditMode  — references Domain (+ NUnit); runs with zero Editor
Assets/Settings/     — URP pipeline + renderer assets (created via Editor steps)
Assets/Audio/        — user drops track files here
Assets/Scenes/       — Greybox.unity
```

The Domain assembly has no `UnityEngine` dependency, so it physically cannot touch `AudioSource`/`MonoBehaviour`. This is what makes "unit-testable without the Editor" enforced rather than aspirational. Domain uses `System.Math`, not `Mathf`.

### 3.2 Composition root (no singletons, no DI framework)

`GameBootstrap : MonoBehaviour` is the single composition root (the "Program.cs/Startup" equivalent). On `Awake` it constructs the Domain systems, the analyzer, and the event bus, then wires their events together. Systems hold interface/event references — they never `FindObjectOfType` each other. No Zenject/VContainer for M1.

### 3.3 Domain layer (pure C#, unit-tested)

- `IAudioAnalyzer`
  - `float Intensity01` — smoothed overall intensity 0..1.
  - `IReadOnlyList<float> Bands` — current band energies.
  - `event Action<BeatInfo> BeatDetected`.
- `BeatInfo { double DspTime; float Strength; }` — beat timestamp on the **DSP clock**, plus confidence/strength.
- `BeatWindow` — given the most recent beat's dsp-time and a fire's dsp-time + a tolerance, returns an on-beat multiplier (1.0 off-beat, up to a configured max within tolerance). Pure function, fully tested.
- `HypeCalculator` — holds current hype; accumulates from a passive rate (function of `Intensity01` + upgrade modifiers) plus discrete ability spikes; clamps to a ceiling (raised by upgrades). Tracks peak and running average for the set.
- `EconomyCalculator` — `cash += f(peakHype, avgHype)` at set end; holds cash; `CanAfford(cost)` / `TrySpend(cost)`.
- `EventBus` (injected instance, not static) + event payload types: `BeatDetected`, `AbilityFired`, `SetEnded`, `UpgradePurchased`. Injected so EditMode tests get a clean bus each run. Plain C# `event`s are used for simple 1:1 wiring; the bus is for 1:many broadcast.

### 3.4 Unity layer (thin adapters)

- `GameBootstrap` — composition root (3.2).
- `FftAudioAnalyzer : MonoBehaviour, IAudioAnalyzer` — pulls `AudioSource.GetSpectrumData`, computes spectral-flux onset with an adaptive threshold, emits `BeatInfo` timestamped with `AudioSettings.dspTime`. All tuning (FFT size, flux threshold, smoothing, sensitivity) read from an `AudioAnalyzerConfig` SO so beat detection can be iterated in the Inspector without recompiling. Code comments note that dense genres (e.g. metal) yield noisier beats — consumers must tolerate noisy beats (v2 will add a file-backed `IAudioAnalyzer`).
- `CrowdController : MonoBehaviour` — a grid of greybox primitives that scale/bounce on `Intensity01`; grid dimensions driven by the capacity upgrade. Visibly denser/larger after a capacity purchase.
- `WhirlpoolAbility : MonoBehaviour` — on input, runs the `BeatWindow` check against the latest beat, applies a hype spike scaled by the on-beat multiplier, and spawns a greybox VFX (scaling/rotating primitive) at a crowd point. Has a cooldown.
- `SetController : MonoBehaviour` — state machine `LivePhase ↔ Intermission`; owns track playback (starts the `AudioSource` from the `TrackDefinition`), detects track end, fires `SetEnded`, drives the transition.
- UI (uGUI for M1): `HypeMeterUI` (fills live), `CashUI`, `IntermissionShopUI` (one capacity upgrade button, disabled when unaffordable), `AbilityButtonUI` (whirlpool button + cooldown readout).
- ScriptableObjects: `TrackDefinition` (AudioClip + metadata), `AbilityDefinition` (cost, cooldown, base spike, on-beat multiplier, tolerance window), `UpgradeDefinition` (cost, effect type + magnitude, e.g. capacity delta), `AudioAnalyzerConfig` (analyzer tuning).

### 3.5 Data flow (one set)

```
FftAudioAnalyzer ──Intensity01──▶ CrowdController (reacts) + HypeCalculator (passive fill)
FftAudioAnalyzer ──BeatDetected─▶ WhirlpoolAbility (latest beat cached for on-beat check)
Player click ────▶ WhirlpoolAbility ─BeatWindow multiplier─▶ HypeCalculator (spike) + VFX
Track ends ──────▶ SetController ─SetEnded─▶ EconomyCalculator (bank cash) + coins-fly VFX
Intermission ────▶ IntermissionShopUI ─buy─▶ EconomyCalculator.TrySpend ─UpgradePurchased─▶
                                                CrowdController/stage grow visibly ─▶ next set bigger
```

## 4. Build order (strict — from the brief)

1. Project scaffold: folder structure, asmdefs, URP install (Editor steps in `SETUP.md`), one greybox scene.
2. `FftAudioAnalyzer` + an `AudioSource` playing a track, with a visible debug readout of intensity + beat pulses.
3. `CrowdController` greybox: grid of primitives that scale/bounce on intensity.
4. `HypeCalculator` + on-screen hype meter filling during playback.
5. `WhirlpoolAbility` with on-beat bonus working (greybox VFX).
6. `SetController`: live phase → set ends → hype banks to cash → intermission.
7. `EconomyCalculator` + minimal intermission shop buying **one capacity upgrade** that visibly changes the greybox scene (densest visible change).
8. Loop back to a second set that is visibly bigger.

## 5. Delivery format

Each step delivers: scripts + SO assets + an editor setup script where possible + updates to `SETUP.md` listing every manual Editor step in order (menu paths, component names, field values). Claude pauses and hands over Editor steps wherever it cannot perform them.

## 6. Testing

EditMode (NUnit) tests in `PitTycoon.Tests.EditMode`, depending only on `PitTycoon.Domain`:
- `BeatWindow`: off-beat → 1.0; exactly on beat → max multiplier; edges of tolerance window; outside window.
- `HypeCalculator`: passive accumulation, ability spikes, ceiling clamp, peak/avg tracking, ceiling raised by upgrade.
- `EconomyCalculator`: cash banking formula, `CanAfford`/`TrySpend` (success + insufficient funds).

A fake `IAudioAnalyzer` feeds deterministic intensity/beats so the economy and beat math are proven without the Editor.

## 7. Out of scope (M1)

- Cel-shaded pipeline (ramp shader, outlines, halftone, manga beat-VFX) — Milestone 2.
- Remaining 3 passive upgrades, second ability (light-burst), additional tracks, intermission polish — Milestone 3.
- File-backed `IAudioAnalyzer` (user-supplied audio) — architected for via the interface, not built.

## 8. Open items / risks

- Beat detection quality will need iteration; mitigated by exposing all tuning via `AudioAnalyzerConfig` and tolerating noisy beats in consumers.
- Project is not currently a git repository; spec is saved to disk but not committed (no commit requested, no repo initialized).
