# PIT TYCOON — Build Brief (Claude Code)
*(working title — lineage from "THE PIT" music visualizer)*

## Your role
You are helping build a Unity game from scratch. I am a senior C# backend engineer (4+ yrs: ASP.NET Core, microservices, DI, message brokers) but a **first-time game/Unity dev**. Assume C# fluency — don't explain language basics. Do explain Unity-specific patterns, lifecycle, and gotchas. Favour clean, decoupled, testable architecture (events/interfaces over singletons-everywhere), the way I'd expect in backend code.

**Hard constraint on what you can do:** you can write C# scripts, ScriptableObjects, HLSL/URP shaders, editor scripts, and setup docs. You **cannot** operate the Unity Editor — creating scenes, wiring prefabs, assigning serialized references, installing packages. For anything that requires the Editor, **stop and give me a precise step-by-step** (menu paths, component names, field values) instead of pretending it's done. Minimise the number of manual Editor steps by generating prefabs/scene setup via editor scripts where reasonable.

---

## The game in one paragraph
A music-festival **tycoon**. A track plays ("a set"); a stylised crowd reacts to the audio in real time, building a **hype** meter. The player actively fires **abilities** (timed to the beat) to spike hype during the set. When the track ends, hype banks as **cash**; the player spends it on upgrades and new abilities during an intermission, then the next set starts bigger and louder. The music is the economy — not background. Art direction is **comic / manga cel-shaded** (bold outlines, flat colour, halftone), but that comes *after* the loop is fun.

---

## Core loop — set-based
1. **Live phase** (one track, ~3–4 min): crowd reacts to audio; hype meter fills from (a) passive venue upgrades and (b) player firing abilities on-beat.
2. **Intermission** (spend phase): hype converts to cash; player buys passive upgrades and/or active abilities.
3. Repeat. Venue is visibly bigger/louder/denser each cycle.

**On-screen rule (non-negotiable):** the whole hype → cash → bigger-crowd chain must be *visible* — meter fills live, coins fly up at set end, crowd visibly denser next set. If the player can't see the loop, the game is dead. No invisible stat bumps; every purchase changes what's on screen.

---

## Economy — two layers
- **Hype** = live, moment-to-moment meter. Fills during the set. Resets each set.
- **Cash** = persistent currency. `cash += f(peak/avg hype)` at set end. Spent at intermission.

---

## Purchases — two tracks
**A. Passive venue upgrades** (always-on, auto-income, visible). Ship all four:
| Upgrade | Economic effect | Visible change |
|---|---|---|
| Stage | raises hype ceiling | bigger stage + band |
| Lighting rig | faster hype gain, bigger beat-flashes | more lights, stronger flash on beat |
| PA / speakers | hype reaches more of the crowd | more/bigger stacks, wider reaction radius |
| Grounds / capacity | more crowd slots = more total income | denser, larger crowd |

**B. Active abilities** (triggered during the set, rewarded for on-beat timing). Data-driven — see below. v1 ships **1–2 abilities only**; the system must make adding more trivial.

**Design intent (the bar for "does this ability work"):** every ability must read as a *crowd-energizing burst* — fire it and the crowd visibly reacts and hype jumps. If an ability doesn't produce a visible lift in the crowd, it's not done.
- **Whirlpool** (starter): a swirling vortex VFX placed in the crowd that spikes local hype — Rasengan-style spiral energy, fits the manga look.
- (optional v1 second) **Light-burst**: screen-wide flash that spikes global hype.

Every ability: firing **on-beat** (within a tolerance window from the beat) multiplies its effect; off-beat does little. This reuses the beat-detection and keeps it a game, not effect-spam.

---

## Audio system
- Put analysis behind an interface: `IAudioAnalyzer` exposing e.g. current band energies, a smoothed intensity 0..1, and a `BeatDetected` event (with a confidence/strength).
- **v1 implementation:** bundled royalty-free tracks + runtime FFT (`AudioSource.GetSpectrumData` / `GetOutputData`). Onset/beat detection via spectral-flux + adaptive threshold (keep it simple and tunable).
- **v2 (architect for, don't build):** a second `IAudioAnalyzer` impl backed by a user-supplied audio file (personal use). Note in code comments that dense genres (e.g. metal) will give rougher beat detection — design the consumers to tolerate noisy beats.
- Crowd reaction and ability on-beat windows consume this interface only — never touch `AudioSource` directly outside the analyzer.

---

## Art direction (MILESTONE 2 — do not start in M1)
Comic / manga cel-shaded, URP:
- **Render pipeline:** URP (not HDRP, not Built-in).
- **Toon lighting:** stepped/ramp lighting (custom HLSL shader or Shader Graph custom function).
- **Outlines:** prefer screen-space **depth+normal edge-detect** as a full-screen render feature (cleaner for a controlled tycoon camera) over inverted-hull.
- **Halftone:** screen-space post effect, luminance thresholded against a dot pattern — biggest "instant comic" payoff.
- **Beat VFX as manga vocabulary:** impact frames, speed lines, halftone shockwaves from the stage, onomatopoeia pop-ups on big beats.
- Camera is pulled-back / RTS-ish, so spend visual budget on **silhouettes, crowd VFX, and UI** — fine ink/screentone won't read at distance.

---

## Tech stack
- Unity (latest LTS), URP.
- C#. Decoupled systems communicating via C# events or a lightweight event bus — not a web of singletons.
- Definitions (abilities, upgrades, tracks) as **ScriptableObjects**.
- Where logic is non-Unity (hype math, economy, beat-window checks), keep it in plain C# classes I can unit-test without the Editor.

---

## Architecture (proposed — adjust if you see better)
- `IAudioAnalyzer` (+ `FftAudioAnalyzer` v1 impl) — spectrum, intensity, `BeatDetected`.
- `HypeSystem` — accumulates hype from upgrades (passive rate) + ability events; exposes current hype.
- `EconomySystem` — converts hype→cash at set end; holds cash; validates purchases.
- `AbilitySystem` — loads `AbilityDefinition` SOs; handles input → on-beat check → effect + VFX.
- `UpgradeSystem` — loads `UpgradeDefinition` SOs; applies passive effects; raises "upgrade purchased" events.
- `CrowdController` — drives crowd size/density/reaction from intensity + upgrades. (Greybox: instanced capsules/quads.)
- `SetController` / `GameLoop` — state machine: `LivePhase` ↔ `Intermission`; owns track playback and transitions.
- `UI` — hype meter, cash, intermission shop, ability buttons + cooldowns.
- Keep systems referencing **interfaces/events**, not each other's concrete types, so I can swap/test pieces.

---

## Build order — STRICT
**Milestone 1 — Greybox vertical slice (NO art).** This is the only thing to build first. Target: a *fun, playable loop* in primitives.
1. Project scaffold: folder structure, asmdefs, URP installed (give me the Editor steps), one greybox scene.
2. `FftAudioAnalyzer` + one bundled track playing, with a visible debug readout of intensity + beat pulses.
3. `CrowdController` greybox: a grid of primitives that scale/bounce on intensity.
4. `HypeSystem` + on-screen hype meter filling during playback.
5. `AbilitySystem` + the **whirlpool** ability (greybox VFX is fine — a scaling/rotating primitive), with on-beat bonus working.
6. `SetController`: live phase → set ends → hype banks to cash → intermission.
7. `EconomySystem` + a minimal intermission shop buying **one** passive upgrade that visibly changes the greybox scene.
8. Loop back to a second set that's visibly bigger.

Deliver M1 as: scripts + SO assets + an editor setup script where possible + a `SETUP.md` listing every manual Editor step in order. **Do not touch the cel-shaded pipeline until I confirm M1 plays well.**

**Milestone 2 — Cel-shaded pipeline** (ramp shader, outlines, halftone, manga beat-VFG) applied over the working greybox.

**Milestone 3 — Content** (remaining 3 passive upgrades, second ability, more tracks, intermission polish).

---

## How to start
Confirm the architecture above (flag anything you'd change), then begin **Milestone 1, step 1**: give me the exact project setup (Unity version, URP package install steps, folder/asmdef layout) and the first scripts. Pause for my Editor steps wherever you can't do them yourself. Ask me before assuming any bundled track/asset exists — I'll supply audio.
