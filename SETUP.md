# Pit Tycoon — Setup (Milestone 1)

Unity 6 LTS (6000.0.x), URP. The Domain layer is already tested via `dotnet test PitTycoon.Domain.slnx` (run from the repo root).

## One-time project bring-up

1. **Create the Unity project (temp location).** In Unity Hub → **New Project** →
   template **Universal 3D** (the URP one), Unity **6 LTS (6000.0.x)**.
   - Project name: `PitTycoonTemp`
   - Location: `C:\Users\afabu\Desktop`
   - Click Create and let it finish importing. Then **close** the Editor.
   - (If you must use a different path/name, tell the agent so it can adjust the move command.)

2. **Merge the project into this repo.** The agent runs a move that places
   `Packages/`, `ProjectSettings/`, and the template's `Assets/*` into the repo root,
   alongside the existing `Assets/PitTycoon/` code. (You don't do this step manually.)

3. **Open the project at the repo root.** In Unity Hub → **Add** → select
   `C:\Users\afabu\Desktop\Personal Projects\FestivalGame` → open it.
   - Wait for import/compile. **Check the Console for errors** and report any to the agent.
     (This is the first time the C# scripts get compiled, so this is where we catch issues.)

4. **Build the greybox scene.** Menu bar: **Pit Tycoon → Build Greybox Scene**.
   A dialog confirms it created `Assets/Scenes/Greybox.unity`, wired the full loop, and
   auto-assigned the bundled sample track (`Assets/Audio/sample_beat.wav`) to the AudioSource.
   It also creates `Assets/Settings/AbilityDefinition.asset` and
   `Assets/Settings/UpgradeDefinition.asset` (tunable in the Inspector).
   - This project is **new-Input-System-only** (`Active Input Handling = Input System Package`);
     the ability reads the keyboard via `UnityEngine.InputSystem`. The first compile after
     pulling resolves the `Unity.InputSystem` asmdef reference automatically.

5. **Play the loop.** Press Play. Expected, in order:
   - The capsule crowd bobs with intensity and pops on beats; the **Hype** meter fills live.
   - Press **SPACE** (or click **WHIRLPOOL**) — fire *on the beat* for an `xN ON-BEAT!`
     multiplier and a bigger whirlpool; hype visibly jumps. Off-beat does little.
   - When the track ends, hype **banks to cash** (coins fly up) and the **INTERMISSION**
     panel appears (top-left).
   - Click **Buy Grounds Expansion** (if affordable), then **Start Next Set ▶** — the next
     set's crowd is visibly **bigger/denser** and the hype ceiling is higher.

   The DebugHud also shows the intensity bar and a **BEAT** flash for tuning beat detection.

### Tuning the loop
Select these assets and adjust in the Inspector (no recompile):
- `Assets/Settings/AbilityDefinition.asset` — `BaseSpike`, `MaxMultiplier`, `ToleranceSeconds`, `Cooldown`.
- `Assets/Settings/UpgradeDefinition.asset` — `Cost`, `AddColumns`/`AddRows`, `CeilingDelta`.
- On the **Systems** object: `HypeSystem` (`StartingCeiling`, `PassiveRatePerSecond`) and
  `EconomySystem` (`StartingCash`, `PeakWeight`, `AvgWeight`) control fill speed and payout.

### Using your own track instead
`Assets/Audio/sample_beat.wav` is a procedurally-generated, royalty-free placeholder
(120 BPM, kick + hats + bass) — regenerate it any time with
`dotnet run tools/audio-gen/GenerateSampleTrack.cs` from the repo root.
To use real music: drop a `.wav`/`.ogg`/`.mp3` into `Assets/Audio/`, select the **Audio**
GameObject, and set **Audio Source → AudioClip** to your file.

## Tuning beat detection
Select `Assets/Settings/AudioAnalyzerConfig.asset` and adjust in the Inspector:
- `ThresholdMultiplier` — lower = more (more sensitive) beats.
- `MinBeatIntervalSeconds` — minimum gap between beats (raise to suppress doubles).
- `IntensityGain` — scale to match your track's loudness so the bar uses the full 0..1 range.

## Milestone 2a — Comic look (cel-shading pipeline)

Prereq: the M1 greybox scene exists (run **Pit Tycoon → Build Greybox Scene** first).

1. With `Assets/Scenes/Greybox.unity` open, run **Pit Tycoon → Apply Comic Look (M2a)**.
   This generates the ramp, materials, and `Assets/Settings/ComicLook.asset` Volume profile,
   assigns ground/crowd materials, and adds a Global Volume + three accent lights.

2. Attach the two fullscreen render features (one-time, on the PC renderer):
   - Select `Assets/Settings/PC_Renderer.asset`.
   - **Add Renderer Feature → Full Screen Pass Renderer Feature**. Name it `Halftone`.
     - Pass Material: `Assets/PitTycoon/Art/Materials/HalftoneMat.mat`
     - Injection Point: **Before Rendering Post Processing**
     - Requirements: **Depth** (it reads scene depth to skip the sky)
     - Fetch Color Buffer: **on**
   - **Add Renderer Feature → Full Screen Pass Renderer Feature** again. Name it `Outline`.
     - Pass Material: `Assets/PitTycoon/Art/Materials/OutlineMat.mat`
     - Injection Point: **Before Rendering Post Processing**
     - Requirements: **Depth, Normal** (edge detect samples both)
     - Fetch Color Buffer: **on**
   - Order in the list: `Halftone` **above** `Outline` (ink draws over the dots).

3. Press **Play**. Expected: stepped (banded) lighting on ground/crowd, inked outlines on
   silhouettes, halftone dots concentrated in shadow areas, a warm/cool accent tint, and the
   full M1 loop still plays.

### Tuning (Inspector dials)
- `ComicRamp` hard steps: re-run the menu after editing `steps` in `ComicLookSetup` (default 3).
- `HalftoneMat`: `Dot Scale` (bigger = larger dots), `Shadow Threshold` (higher = more dots), `Ink Color`.
- `OutlineMat`: `Line Thickness`, `Depth/Normal Sensitivity` (raise if edges are missing, lower if noisy).
- `ComicLook` Volume: `Bloom` intensity (accent glow), `ColorAdjustments` saturation (the "color-pop" dial).
- Accent lights: color/intensity/position of `Accent Amber/Magenta/Cyan`.

### If outlines don't appear
The Outline feature needs `_CameraNormalsTexture`. If lines are missing, confirm the Outline
feature's Requirements includes **Normal**; if it still fails on this URP version, leave
Requirements = Depth+Normal (URP generates the DepthNormals prepass when a feature requests
Normal). The ComicLit shader's `DepthNormals` pass must compile for objects to appear there.

## Milestone 2b — Festival assets

Prereq: M1 scene built and M2a comic look applied (so the accent lights + ComicLit exist).

1. Let the FBX in `Assets/PitTycoon/Art/Models/` import (Unity does this automatically).
   They should appear upright at sensible scale; if any is rotated/huge, tell me and I'll
   re-export from Blender with corrected settings.
2. With `Greybox.unity` open, run **Pit Tycoon → Build Festival Scene (M2b)**.
   This creates `CrowdFigure.prefab` + `StructureMat`, places the stage/truss/PA/banner,
   reparents the accent lights onto the truss, and points CrowdController at the figure.
3. Press **Play**. Expected: the crowd is now arms-up figures that **hop** with the music
   (bigger hop on beats), with a stage/truss/PA/banner behind them, all comic-shaded.

### Tuning (CrowdController in the scene)
- `Bounce Height` / `Bob Speed` — hop size and tempo.
- `Beat Pop` — extra hop on detected beats.
- `Rotation Jitter` / `Scale Jitter` — per-figure variety (0 = uniform).
- `Member Prefab` — the crowd-figure prefab (auto-wired by the menu; capsule fallback if cleared).

## Milestone 2c — Manga beat-VFX

Prereq: M2a + M2b applied (comic look + festival scene).

1. Pull M2c, let Unity recompile (new VFX scripts).
2. With `Greybox.unity` open, re-run **Pit Tycoon → Build Festival Scene (M2b)** — it now also
   adds `BeatVfxController` to `Systems`, wires the stage anchor + whirlpool/coin materials, and
   points `GameBootstrap` at it.
3. Press **Play**. Expected: big beats throw a shockwave ring off the stage + an onomatopoeia
   pop (DON!/POW!); a PERFECT whirlpool throws a "POW!"; the whirlpool + coins read comic.

### Tuning (BeatVfxController on the Systems object)
- `Strength Threshold` — how loud a beat must be to fire (lower = more pops).
- `Multiplier Threshold` — how good an ability hit must be for its POW! (lower = more).
- `Min Interval` — seconds between big-beat pops (raise to thin them out).
- `Shockwave Radius` — how far the ring expands.
- `Whirlpool Material` / `Coin Material` — the comic accent materials.

## Milestone 3a — Passive upgrade roster

Prereq: M2a + M2b applied (the venue instances must exist for the visible scaling).

1. Pull M3a, let Unity recompile.
2. Run **Pit Tycoon → Build Greybox Scene** (creates the four `Upgrade_*` assets + wires the
   `UpgradeSystem` list), then **Apply Comic Look (M2a)** and **Build Festival Scene (M2b)** —
   the latter now also adds a `VenueController` and links it to `UpgradeSystem`.
3. Play a set, let it bank cash, and in intermission buy each upgrade. Expected: each buy
   **escalates** its next cost (shown as `Lv N ($cost)`), and visibly steps the venue —
   Grounds = denser crowd next set, Stage = bigger stage, Lighting = brighter lights,
   PA = bigger speaker stacks — while hype ceiling/rate climb.

### Tuning
- Each `Assets/Settings/Upgrade_*.asset`: `BaseCost`, `CostGrowth`, and the per-level deltas.
- `VenueController` on `Systems`: `stageStep` / `paStep` / `lightStep` (how much each level scales).
