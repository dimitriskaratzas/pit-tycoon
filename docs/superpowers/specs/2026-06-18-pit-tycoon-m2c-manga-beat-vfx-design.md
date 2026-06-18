# Pit Tycoon — Milestone 2c Design (Manga Beat-VFX)

**Date:** 2026-06-18
**Status:** Approved (design), pending spec review
**Scope:** M2c only — event-driven comic "energy" VFX (the Core loud-beat set). Speed lines and full-frame impact frames are deferred to the backlog (§8).
**Source brief:** `pit-tycoon-claude-code-brief.md` ("Beat VFX as manga vocabulary").
**Predecessors:** M2a comic pipeline + M2b festival geometry (PRs #4, #5 merged).

## 1. Goal

Make the gig feel *loud* by punctuating big musical moments with comic-book VFX, driven entirely by the events already on the `EventBus` (`BeatDetected`, `AbilityFired`). No gameplay change. When M2c is done, big beats throw onomatopoeia pops and a shockwave off the stage, a PERFECT whirlpool throws a "POW!", and the whirlpool/coins read as comic instead of grey primitives.

**Non-negotiable rule:** M2c must not regress the M1 loop, M2a look, or M2b geometry. It only adds transient, self-destructing VFX.

## 2. Trigger model

The crowd already reacts to every beat (the M2b hop). The conductor only fires the *loud punctuation*:

- **Big beat** — `BeatInfo.Strength ≥ strengthThreshold` (tunable), rate-limited to a minimum interval (~1.5s) so it punctuates rather than spams → spawns an **onomatopoeia pop** + a **shockwave ring** from the stage.
- **Great ability hit** — `AbilityFired.Multiplier ≥ multiplierThreshold` (a PERFECT whirlpool) → spawns an onomatopoeia **"POW!"** at the pit. Ties the loudest comic beat to player skill.

Thresholds + cooldown are serialized fields on the conductor, tuned at the checkpoint.

## 3. Environment & constraints

- **Unity 6 / URP 17.4**, comic pipeline from M2a, geometry from M2b.
- Event-driven via the injected `EventBus` — same pattern as `HypeSystem`/`WhirlpoolAbility`. No new singletons.
- **World-space, code-only VFX** — no font assets or sprite imports, mirroring the existing `WhirlpoolVfx`/`CoinFlyVfx` runtime-spawn philosophy. The M2a screen-space outline + halftone passes already ink anything on screen, so world-space VFX inherit the comic look for free.
- Claude authors C# + (if needed) tiny VFX materials via the editor setup; the user does the in-editor checkpoint.

## 4. Components

All new VFX self-spawn and self-destruct (no scene wiring), like the existing ones.

- **`BeatVfxController` (MonoBehaviour, bus-driven conductor).** `Initialize(EventBus)` subscribes to `BeatDetected` + `AbilityFired` (+ `SetStarted`/`SetEnded` to gate to the live phase). Holds serialized `strengthThreshold`, `multiplierThreshold`, `minInterval` cooldown, a stage-anchor `Transform` (shockwave origin), and the onomatopoeia word list. Decides what fires and spawns it.
- **`OnomatopoeiaVfx` (self-spawning).** A world-space `TextMesh` pop: accent-colored front + a black offset copy behind = ink outline. Scale-punch in, slight rise, fade out, billboards to the camera, self-destructs (~0.8s). Words: DON! / POW! / BOOM! / WHAM! etc., color picked from the accent palette.
- **`ShockwaveVfx` (self-spawning).** A flat ink ring on the ground expanding outward from the stage on a big beat, fading as it grows; self-destructs (~0.6s).
- **Restyle `WhirlpoolVfx` + `CoinFlyVfx`.** Give them accent materials so they read comic (gold coins, accent-tinted whirlpool); the M2a outline/halftone passes do the inking. No behavioural change.

## 5. Wiring

- `GameBootstrap`: add a serialized `BeatVfxController` ref + `beatVfx.Initialize(Bus)` in the init sequence, and a serialized stage-anchor (the M2b `Stage` transform) passed to the controller.
- The editor setup (extend `FestivalSceneSetup` or `ComicLookSetup`) adds the `BeatVfxController` component to the `Systems` object, wires the stage anchor, and creates any small VFX materials.

## 6. Verification

No automated tests (VFX/rendering). Manual in-editor checkpoint:

1. Big beats throw an onomatopoeia pop + a shockwave ring from the stage, rate-limited (not every beat).
2. A PERFECT whirlpool throws a "POW!" at the pit.
3. Whirlpool + coins read comic (inked, accent-colored), not grey.
4. **M1 loop intact**; effects don't spam or tank frame rate.
5. Domain suite still 50 green (untouched).

## 7. Out of scope

- Any gameplay/economy/ability change.
- Audio/SFX for the hits.
- Movable camera / venue / stalls (later milestone).

## 8. Backlog (deferred — keep, don't drop)

Explicitly deferred to a future polish pass (e.g., "M2d"), per decision during brainstorming:

- **Screen-space speed lines** — radial lines flashed on the biggest beats. Deferred because full-screen effects need careful rate-limiting to avoid a nauseating/seizure-y result.
- **Full-frame impact frames** — a brief high-contrast comic "freeze flash" on peak moments. Same rate-limiting concern; build after the localized set proves the trigger feel.

## 9. Risks & open questions

- **`BeatInfo.Strength` scale** — the spectral-flux strength magnitude is detector-dependent; the big-beat threshold is a tuning dial set at the checkpoint, not a fixed constant.
- **Spam/feel** — onomatopoeia + shockwave on every big beat may still be too much; the `minInterval` cooldown and threshold guard this and are tuned live.
- **TextMesh legibility** — legacy `TextMesh` is plain; the front+black-offset copy gives the inked comic read without a font import. Confirm it reads at the pulled-back camera.
- **Live-phase gating** — VFX should only fire during the live set, not intermission; the controller gates on `SetStarted`/`SetEnded`.
