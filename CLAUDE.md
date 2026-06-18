# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Pit Tycoon** — a music-festival tycoon game in Unity 6 LTS (`6000.4.11f1`), URP. A track plays ("a set"), a stylised crowd reacts to the audio building a **hype** meter, the player fires beat-timed **abilities** to spike it; at set end hype banks to **cash** spent on upgrades during an intermission, then the next set is bigger. The music is the economy. Art direction is comic / manga cel-shaded.

Read `pit-tycoon-claude-code-brief.md` for full design intent and `SETUP.md` for the Editor bring-up + per-milestone setup steps. Per-milestone design/implementation notes live in `docs/` (dated `YYYY-MM-DD-pit-tycoon-*.md`).

## Hard constraint: you cannot operate the Unity Editor

You can write C# scripts, ScriptableObjects, HLSL/URP shaders, editor scripts, and docs. You **cannot** create scenes, wire prefabs, assign serialized references, or install packages. For anything needing the Editor, **stop and give precise step-by-step instructions** (menu paths, component names, field values) — don't pretend it's done. Prefer generating scene/prefab setup via editor scripts (the `Pit Tycoon → …` menu, see `Assets/PitTycoon/Unity/Editor/`) to minimise manual steps.

The dev is a senior C# backend engineer but a first-time Unity dev: assume C# fluency, explain Unity-specific lifecycle/patterns/gotchas.

## Build & test

The **Domain** layer is plain C# (netstandard2.1, no UnityEngine) and is unit-tested outside the Editor. From the repo root:
```bash
dotnet test PitTycoon.Domain.slnx     # NUnit tests in tests/PitTycoon.Domain.Tests (net10.0)
```
Editor-side code is compiled by Unity on project open — watch the Console for the first compile after pulling. This project is **new-Input-System-only** (`Active Input Handling = Input System Package`).

## Architecture

The codebase is deliberately split into two assemblies so game logic stays testable without the Editor:

- **`Assets/PitTycoon/Domain/`** (`PitTycoon.Domain.asmdef`, plain C#): pure logic + contracts — `HypeCalculator`, `EconomyCalculator`, `BeatWindow`, `BeatGrid`, `SpectralFluxDetector`, `IAudioAnalyzer`, the `Ability` model, `EventBus` and `Events`. No UnityEngine references; everything here is unit-tested.
- **`Assets/PitTycoon/Unity/`** (`PitTycoon.Unity.asmdef`, MonoBehaviours): the runtime systems that consume Domain — `FftAudioAnalyzer`, `HypeSystem`, `EconomySystem`, `AbilitySystem`, `UpgradeSystem`, `CrowdController`, `SetController`, `GameBootstrap`, `DebugHud`, and the VFX (`WhirlpoolVfx`, `LightBurstVfx`, `ShockwaveVfx`, `OnomatopoeiaVfx`, `CoinFlyVfx`, `BeatVfxController`).
- **`Assets/PitTycoon/Unity/Editor/`** (`PitTycoon.Unity.Editor.asmdef`): the `Pit Tycoon → …` menu scene/asset builders (`PitTycoonSetup`, `ComicLookSetup`, `FestivalSceneSetup`).

Key principles to preserve:
- **Systems communicate through `EventBus` / C# events and interfaces, not concrete cross-references or a web of singletons** (this is the whole point of the split — keep it decoupled and swappable).
- **Only the analyzer touches `AudioSource`.** Crowd reaction and ability on-beat windows consume `IAudioAnalyzer` (band energies, smoothed 0..1 intensity, a `BeatDetected` event with strength) — never read `AudioSource` directly elsewhere. Design consumers to tolerate noisy beats (a v2 analyzer will read user-supplied audio).
- **Abilities/upgrades/tuning are ScriptableObjects** (`AbilityDefinition`, `UpgradeDefinition`, `AudioAnalyzerConfig`, etc. under `Assets/Settings/`), tunable in the Inspector with no recompile. Firing **on-beat** (within tolerance) multiplies an ability's effect; off-beat does little.
- **The hype → cash → bigger-crowd loop must be visible on screen.** No invisible stat bumps — every purchase changes what's rendered (denser crowd, bigger stage, more lights, coins flying up). If a change isn't visible, it isn't done.

## Conventions

- Commit messages: imperative present tense, no `Co-Authored-By` trailer.
- Keep non-Unity logic (hype math, economy, beat-window checks) in `Domain/` so it stays unit-testable.
