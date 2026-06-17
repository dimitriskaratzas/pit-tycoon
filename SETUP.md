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
   A dialog confirms it created `Assets/Scenes/Greybox.unity`, wired everything, and
   auto-assigned the bundled sample track (`Assets/Audio/sample_beat.wav`) to the AudioSource.

5. **Play.** Press the Play button. Expected:
   - The capsule crowd bobs with the music's intensity and pops on beats.
   - The DebugHud (top-left) shows the intensity bar and flashes **BEAT** on detected beats.

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
