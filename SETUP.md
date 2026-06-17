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
