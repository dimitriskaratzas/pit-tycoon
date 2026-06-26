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

## Crowd Dynamics rework

Prereq: M2a + M2b applied (the venue + crowd-figure prefab must exist).

1. Pull the branch, let Unity recompile (new `CrowdFill`, meter interfaces, reworked `CrowdController`).
2. Re-run **Pit Tycoon → Build Greybox Scene**, then **Apply Comic Look (M2a)** and
   **Build Festival Scene (M2b)** — the rebuild picks up the reworked `CrowdController`
   (new `startingCapacity`/`startingFollowing` defaults) and regenerates the upgrade assets
   (Grounds now uses `AddCapacity`).
3. Play. Expected, in order:
   - The pit starts **sparse** (a small following near the stage) and **packs from the stage
     outward** as hype climbs through the set — members pop in front-to-back.
   - Hype gains **faster when the pit is fuller** (fill multiplies the passive rate).
   - At set end the crowd **stays visible** (it doesn't vanish in intermission).
   - The next set starts at that **fuller floor** and grows higher — your following ratchets up.
   - Buy **Grounds Expansion**: the venue gains visible **empty room at the back**, which
     fills over the next sets as your following grows into it.

### Tuning
- `CrowdController` on the **Crowd** object: `startingCapacity` / `startingFollowing` (opening
  pit), `columns` (pit width), `scaleInPerSecond` (how fast members pop in).
- `HypeSystem` on **Systems**: `minFillMultiplier` — hype-rate multiplier when the pit is empty
  (lower = stronger warm-up pressure; 1 = fill has no economic effect).
- `Assets/Settings/Upgrade_Grounds.asset`: `AddCapacity` (slots added per purchase), `BaseCost`,
  `CostGrowth`.

## UI foundation (UGUI HUD + intermission shop)

Prereq: the festival scene exists (M2b) and systems are wired (`GameBootstrap`).

1. Pull the branch; let Unity recompile (new `PitTycoon.Unity.UI` scripts, `HudSetup`).
2. **One-time:** `Window → TextMeshPro → Import TMP Essential Resources` (UGUI text is invisible without it).
3. Run **Pit Tycoon → Build HUD**. This creates the `GameHUD` canvas, an `EventSystem`
   (with `InputSystemUIInputModule`), and wires every reference including `GameBootstrap.hud`.
   Re-running rebuilds cleanly.
4. Play. Expected:
   - **Live:** hype bar (top-center) tracks hype with a peak marker; cash (top-right) and
     set number (top-left) update; an ability button per owned ability (bottom-center) fires
     on click **and** on its hotkey, with a cooldown overlay; a hit-quality popup and a beat
     pulse fire during play.
   - **Set end:** the intermission shop docks on the right (scene stays visible, no dimming),
     shows the banked-this-set amount, upgrade rows (name/level/cost, greyed when unaffordable)
     and ability-unlock rows; buying refreshes the rows; **Start Next Set** returns to live.
   - **F1** toggles the dev overlay (analyzer intensity / beat / dsp).

### Tuning
- `LiveHudView`: `hitQualityHold` (popup duration), `beatPulseDecay` (pulse fade speed).
- `HudSetup` constants (`Panel`/`Bar`/`HypeOrange`/`Amber`, anchors/sizes) control the look;
  re-run **Build HUD** after editing.

## Upgrade ghost-preview (sub-project B — camera + ghost)

Prereq: the UI foundation HUD exists (run **Pit Tycoon → Build HUD** first) and the festival
scene + venue are built (M2b).

1. Pull the branch; let Unity recompile (new `CameraRig`, `UpgradePreviewController`,
   `PreviewSetup`, preview hooks on Venue/Crowd/Ability).
2. Run **Pit Tycoon → Build HUD** (so the shop rows have the new Buy/Cancel + a Return Home
   button), then **Pit Tycoon → Build Upgrade Preview**. The latter creates `GhostMat`, adds a
   `CameraRig` to the Main Camera and an `UpgradePreviewController` to **Systems**, fills the
   camera waypoints, and wires the ghost material + the ShopView/GameBootstrap preview refs.
   Re-running either rebuilds cleanly.
3. Play. At intermission:
   - Click an upgrade row → the camera flies to that spot and a **ghost** shows the next step
     (bigger stage / bigger PA / brighter lights / new back-row figures). The row expands with
     **BUY $cost** / **CANCEL**.
   - **Buy** commits (ghost becomes real), the row bumps level/cost, and the preview re-arms for
     the next level; the camera stays put.
   - **Cancel** reverts the ghost; the camera stays where it is.
   - Selecting another row flies straight there; **⌂ Overview** returns the camera home.
   - An ability row fires a one-shot VFX demo; **Buy** unlocks it.
   - **Start Next Set** snaps the camera home and starts cleanly.

### Tuning
- `UpgradePreviewController` on **Systems**: the per-kind `waypoints` (camera position/euler),
  `abilityCamPosition`/`abilityCamEuler`, and `flyDuration`.
- `CameraRig` on the Main Camera: `defaultDuration` (fly/return speed).
- `Assets/PitTycoon/Art/Materials/GhostMat.mat`: `Base Map` colour/alpha (ghost tint/opacity).
