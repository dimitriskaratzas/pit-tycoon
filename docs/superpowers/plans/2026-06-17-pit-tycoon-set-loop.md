# Pit Tycoon — Set Loop, Hype, Ability & Economy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver M1 build-order steps 4–8 — a complete, *visible* greybox loop: hype meter fills live during a set, the whirlpool ability spikes hype with an on-beat bonus, the set ends and banks hype→cash with coins flying up, an intermission shop buys one capacity upgrade, and the next set starts visibly bigger.

**Architecture:** All the *math* already lives in the tested Domain layer (`HypeCalculator`, `EconomyCalculator`, `BeatWindow`) — this plan adds no new pure logic except one event type, so there are no new `dotnet test` cases. The work is thin Unity adapters that compose those Domain types and broadcast over the existing injected `EventBus`: `HypeSystem`, `WhirlpoolAbility` (+ runtime `WhirlpoolVfx`), `SetController`, `EconomySystem`, `UpgradeSystem`, plus a `CoinFlyVfx`. `GameBootstrap` (composition root) constructs the bus and `Initialize(bus)`s each system; cross-system deps are serialized fields wired automatically by the editor script. The greybox UI stays IMGUI (one expanded HUD) so it needs zero Canvas wiring — uGUI is deferred to M2.

**Tech Stack:** Unity 6 LTS (6000.4.x), URP, C# 9, Input System 1.19.0 (project is new-input-only). Three asmdefs unchanged except `PitTycoon.Unity` gains a `Unity.InputSystem` reference. Domain stays unit-tested via `dotnet test PitTycoon.Domain.slnx`.

**Verification model (unchanged from the foundation plan):** I cannot operate the Unity Editor or compile Unity scripts. The single Domain change (Task 1) is verified by `dotnet test`. The Unity scripts (Tasks 2–6) are authored and committed without a compile check. **Task 7 is the one Unity bring-up checkpoint**: you add the asmdef package via the menu rebuild, run `Pit Tycoon → Build Greybox Scene`, press Play, and we fix any compile/wiring errors together.

**Spec:** `docs/superpowers/specs/2026-06-16-pit-tycoon-m1-design.md` (sections 3.3–3.5, 4 steps 4–8). **Brief:** `pit-tycoon-claude-code-brief.md` (core loop, on-screen rule, whirlpool, four-upgrade table → capacity for M1).

---

## Key design decisions (read before executing)

1. **No new Domain math.** `HypeCalculator.Tick/AddSpike/RaiseCeiling/ResetForNewSet/Peak/Average`, `EconomyCalculator.BankSet/CanAfford/TrySpend`, and `BeatWindow.Multiplier` already cover steps 4–8 and are tested (41 green). The only Domain addition is a `SetStarted` event struct (Task 1). Everything else is Unity glue → verified at the Task 7 play checkpoint, not by unit tests.

2. **Set phases via two events on the existing bus.** `SetStarted(setNumber)` and the existing `SetEnded(peak, avg, cash)` bracket the Live phase. Live = between `SetStarted` and `SetEnded`; Intermission = after `SetEnded` until the next `SetStarted`. `HypeSystem` and `WhirlpoolAbility` flip a `_live` flag off these; they don't reference `SetController`.

3. **`SetController` orchestrates the banking step with direct refs** (to `HypeSystem` + `EconomySystem` + `CrowdController` + `AudioSource`), because banking needs the set's peak/avg read synchronously at end-of-set. Loosely-coupled consumers (HUD, hype reset, ability gating) still go through the bus. This is the pragmatic M1 choice; M2 can interface-ize if needed.

4. **IMGUI HUD, not uGUI.** The spec lists uGUI components, but Canvas wiring is exactly what I can't do in the Editor. The existing `DebugHud` is expanded in place (kept its class name to avoid scene/GUID churn) into the full greybox HUD: intensity, hype meter (current/ceiling), cash, set #/phase, ability button + cooldown + last on-beat multiplier, and the intermission shop (buy + "Start Next Set"). M2 replaces this with real uGUI. Buttons are `GUI.Button` calls — no input-system dependency for UI.

5. **Input via the new Input System.** `activeInputHandler: 1` means legacy `UnityEngine.Input` throws at runtime. The ability reads `UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame`; the asmdef gains `"Unity.InputSystem"`. A GUI **Fire** button is also provided as a mouse fallback.

6. **Greybox VFX are runtime-spawned, zero-wiring** `MonoBehaviour`s (`WhirlpoolVfx`, `CoinFlyVfx`) created via `AddComponent` on primitives — they self-animate and self-destroy, so they never need scene references. This is what makes "coins fly up" and "whirlpool reacts on-beat" visible (the non-negotiable on-screen rule) without Editor work.

7. **`AudioSource.playOnAwake = false`.** `SetController` owns playback (starts/restarts the clip per set). The editor script sets this.

## File Structure

```
Assets/PitTycoon/Domain/
  Events.cs                          MODIFY — add SetStarted struct
Assets/PitTycoon/Unity/
  PitTycoon.Unity.asmdef             MODIFY — add "Unity.InputSystem" reference
  AbilityDefinition.cs               NEW — SO: whirlpool tuning (spike, max mult, tolerance, cooldown)
  UpgradeDefinition.cs               NEW — SO: capacity upgrade (cost, +cols/+rows, ceiling delta)
  HypeSystem.cs                      NEW — drives HypeCalculator; live fill + ability spikes
  WhirlpoolAbility.cs                NEW — on-beat fire → AbilityFired; spawns WhirlpoolVfx
  WhirlpoolVfx.cs                    NEW — runtime greybox spiral (self-animating)
  CoinFlyVfx.cs                      NEW — runtime coin burst at set end (self-animating)
  EconomySystem.cs                   NEW — wraps EconomyCalculator; banks at set end
  UpgradeSystem.cs                   NEW — buys upgrade → spend + apply (crowd grow, ceiling raise)
  SetController.cs                   NEW — Live↔Intermission state machine; owns playback + banking
  CrowdController.cs                 MODIFY — add Grow(); Build() driven by SetController
  GameBootstrap.cs                   MODIFY — construct bus, Initialize all systems, wire beat bridge
  DebugHud.cs                        MODIFY — expand into full greybox HUD (meter/cash/shop/ability)
  Editor/
    PitTycoonSetup.cs                MODIFY — build & wire the full loop scene + create the 2 SO assets
Assets/Settings/
  AbilityDefinition.asset            generated by the editor script (Task 6)
  UpgradeDefinition.asset            generated by the editor script (Task 6)
SETUP.md                             MODIFY — Task 7 bring-up + controls
```

---

### Task 1: Domain — `SetStarted` event

**Files:**
- Modify: `Assets/PitTycoon/Domain/Events.cs`

- [ ] **Step 1: Add the `SetStarted` struct**

Append inside the `PitTycoon.Domain` namespace in `Assets/PitTycoon/Domain/Events.cs` (after `UpgradePurchased`):

```csharp
    /// <summary>Raised when a new set begins (live phase starts).</summary>
    public readonly struct SetStarted
    {
        public int SetNumber { get; }
        public SetStarted(int setNumber) { SetNumber = setNumber; }
    }
```

- [ ] **Step 2: Verify Domain still compiles & tests pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 41` (no new tests; this is a struct-only addition with no logic to test).

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Domain/Events.cs
git commit -m "feat(domain): add SetStarted event for set lifecycle"
```

---

### Task 2: HypeSystem + live hype meter (build-order step 4)

**Files:**
- Create: `Assets/PitTycoon/Unity/HypeSystem.cs`
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs` (add hype meter readout)

- [ ] **Step 1: Create `HypeSystem`**

`Assets/PitTycoon/Unity/HypeSystem.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Drives the pure HypeCalculator from the analyzer's intensity during the live
    /// phase and applies ability spikes. Listens to set lifecycle on the bus; never
    /// references SetController. Hype resets each set; the ceiling persists (upgrades).
    /// </summary>
    public sealed class HypeSystem : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private float startingCeiling = 100f;
        [Tooltip("Hype/sec at full intensity from passive venue (before upgrades).")]
        [SerializeField] private float passiveRatePerSecond = 6f;

        private HypeCalculator _calc;
        private EventBus _bus;
        private bool _live;

        public float Current => _calc?.Current ?? 0f;
        public float Ceiling => _calc?.Ceiling ?? startingCeiling;
        public float Peak => _calc?.Peak ?? 0f;
        public float Average => _calc?.Average ?? 0f;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _calc = new HypeCalculator(startingCeiling);
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnSetStarted(SetStarted e) { _calc.ResetForNewSet(); _live = true; }
        private void OnSetEnded(SetEnded e) { _live = false; }
        private void OnAbilityFired(AbilityFired e) { _calc.AddSpike(e.HypeAdded); }

        /// <summary>Permanently raise the hype ceiling (capacity/stage upgrades).</summary>
        public void RaiseCeiling(float delta) => _calc?.RaiseCeiling(delta);

        private void Update()
        {
            if (!_live || analyzer == null || _calc == null) return;
            _calc.Tick(Time.deltaTime, analyzer.Intensity01, passiveRatePerSecond);
        }
    }
}
```

- [ ] **Step 2: Add the hype meter to the HUD**

In `Assets/PitTycoon/Unity/DebugHud.cs`, add a serialized field and render the meter. Add the field next to `analyzer`:

```csharp
        [SerializeField] private HypeSystem hype;
```

And in `OnGUI()`, after the existing "Last beat dsp" label, append:

```csharp
            if (hype != null)
            {
                float frac = hype.Ceiling > 0f ? Mathf.Clamp01(hype.Current / hype.Ceiling) : 0f;
                GUI.Label(new Rect(12, 180, 320, 20), $"Hype: {hype.Current:0} / {hype.Ceiling:0}");
                GUI.Box(new Rect(12, 202, 260, 22), GUIContent.none);
                Color prev = GUI.color;
                GUI.color = new Color(0.3f, 0.9f, 0.5f, 1f);
                GUI.Box(new Rect(12, 202, 260f * frac, 22), GUIContent.none);
                GUI.color = prev;
            }
```

(Full game HUD — cash, set #, ability, shop — is added in later tasks. Keeping one HUD avoids overlapping `OnGUI` components.)

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/HypeSystem.cs Assets/PitTycoon/Unity/DebugHud.cs
git commit -m "feat(unity): HypeSystem + live hype meter (M1 step 4)"
```

---

### Task 3: Whirlpool ability with on-beat bonus (build-order step 5)

**Files:**
- Create: `Assets/PitTycoon/Unity/AbilityDefinition.cs`
- Create: `Assets/PitTycoon/Unity/WhirlpoolVfx.cs`
- Create: `Assets/PitTycoon/Unity/WhirlpoolAbility.cs`
- Modify: `Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef` (add input system ref)
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs` (ability button + cooldown + last multiplier)

- [ ] **Step 1: Create `AbilityDefinition` SO**

`Assets/PitTycoon/Unity/AbilityDefinition.cs`:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven ability (M1: whirlpool only). Adding more abilities = new assets,
    /// no code. On-beat firing multiplies BaseSpike up to MaxMultiplier within tolerance.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Pit Tycoon/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string Id = "whirlpool";
        public string DisplayName = "Whirlpool";
        [Tooltip("Hype added on fire, before the on-beat multiplier.")]
        public float BaseSpike = 12f;
        [Tooltip("Multiplier when fired exactly on a beat (falls to 1.0 at the window edge).")]
        [Min(1f)] public float MaxMultiplier = 3f;
        [Tooltip("Seconds either side of a beat that still count as on-beat.")]
        public float ToleranceSeconds = 0.12f;
        [Tooltip("Seconds between fires.")]
        public float Cooldown = 1.2f;
    }
}
```

- [ ] **Step 2: Create `WhirlpoolVfx` (runtime greybox spiral)**

`Assets/PitTycoon/Unity/WhirlpoolVfx.cs`:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox whirlpool: a flattened cylinder that scales up and spins, then
    /// self-destructs. Spawned at runtime (no scene wiring). Bigger when fired
    /// closer to the beat, so the on-beat reward is visible.
    /// </summary>
    public sealed class WhirlpoolVfx : MonoBehaviour
    {
        private float _life;
        private float _maxLife = 0.7f;
        private float _maxScale = 4f;
        private const float SpinDegPerSec = 720f;

        /// <param name="onBeat01">0 = off-beat, 1 = perfect on-beat (sizes the VFX).</param>
        public static void Spawn(Vector3 pos, float onBeat01)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "WhirlpoolVFX";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = pos + Vector3.up * 0.1f;
            go.transform.localScale = new Vector3(0.1f, 0.05f, 0.1f);
            var fx = go.AddComponent<WhirlpoolVfx>();
            fx._maxScale = Mathf.Lerp(2.5f, 6f, Mathf.Clamp01(onBeat01));
        }

        private void Update()
        {
            _life += Time.deltaTime;
            float t = _life / _maxLife;
            if (t >= 1f) { Destroy(gameObject); return; }
            float s = Mathf.Lerp(0.1f, _maxScale, t);
            transform.localScale = new Vector3(s, 0.05f, s);
            transform.Rotate(0f, SpinDegPerSec * Time.deltaTime, 0f, Space.World);
        }
    }
}
```

- [ ] **Step 3: Create `WhirlpoolAbility`**

`Assets/PitTycoon/Unity/WhirlpoolAbility.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Player ability. Caches the latest beat's DSP time from the bus; on fire it runs
    /// the pure BeatWindow check (fire-time vs last beat) for an on-beat multiplier,
    /// publishes AbilityFired (HypeSystem applies the spike), and spawns a whirlpool VFX.
    /// Only fires during the live phase. Space (new Input System) or the HUD button.
    /// </summary>
    public sealed class WhirlpoolAbility : MonoBehaviour
    {
        [SerializeField] private AbilityDefinition definition;
        [Tooltip("Where the VFX spawns (the crowd centre).")]
        [SerializeField] private Transform vfxAnchor;

        private EventBus _bus;
        private double _lastBeatDsp = double.NegativeInfinity;
        private float _cooldownRemaining;
        private bool _live;

        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownTotal => definition != null ? definition.Cooldown : 0f;
        public float LastMultiplier { get; private set; }
        public bool Ready => _live && _cooldownRemaining <= 0f;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _bus.Subscribe<BeatDetected>(OnBeat);
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<BeatDetected>(OnBeat);
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
        }

        private void OnBeat(BeatDetected e) => _lastBeatDsp = e.Beat.DspTime;
        private void OnSetStarted(SetStarted e) { _live = true; _cooldownRemaining = 0f; }
        private void OnSetEnded(SetEnded e) { _live = false; }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) Fire();
        }

        /// <summary>Fire the ability (also called by the HUD button). No-op if not ready.</summary>
        public void Fire()
        {
            if (definition == null || _bus == null || !Ready) return;

            double now = AudioSettings.dspTime;
            float mult = BeatWindow.Multiplier(
                _lastBeatDsp, now, definition.ToleranceSeconds, definition.MaxMultiplier);
            float hypeAdded = definition.BaseSpike * mult;

            LastMultiplier = mult;
            _cooldownRemaining = definition.Cooldown;

            Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
            float onBeat01 = (mult - 1f) / Mathf.Max(0.0001f, definition.MaxMultiplier - 1f);
            WhirlpoolVfx.Spawn(pos, onBeat01);

            _bus.Publish(new AbilityFired(definition.Id, mult, hypeAdded));
        }
    }
}
```

- [ ] **Step 4: Add the `Unity.InputSystem` asmdef reference**

Replace the `references` array in `Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef`:

```json
    "references": [
        "PitTycoon.Domain",
        "Unity.InputSystem"
    ],
```

- [ ] **Step 5: Add ability readout + Fire button to the HUD**

In `Assets/PitTycoon/Unity/DebugHud.cs`, add the field:

```csharp
        [SerializeField] private WhirlpoolAbility ability;
```

And in `OnGUI()`, after the hype meter block, append:

```csharp
            if (ability != null)
            {
                bool ready = ability.Ready;
                float cd = ability.CooldownRemaining;
                string label = ready ? "WHIRLPOOL (Space)" : $"Whirlpool {cd:0.0}s";
                GUI.enabled = ready;
                if (GUI.Button(new Rect(12, 234, 200, 30), label)) ability.Fire();
                GUI.enabled = true;
                if (ability.LastMultiplier > 1.01f)
                    GUI.Label(new Rect(220, 240, 120, 20), $"x{ability.LastMultiplier:0.0} ON-BEAT!");
            }
```

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/AbilityDefinition.cs Assets/PitTycoon/Unity/WhirlpoolVfx.cs Assets/PitTycoon/Unity/WhirlpoolAbility.cs Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef Assets/PitTycoon/Unity/DebugHud.cs
git commit -m "feat(unity): whirlpool ability with on-beat bonus + VFX (M1 step 5)"
```

---

### Task 4: SetController + economy banking + coins-fly (build-order step 6)

**Files:**
- Create: `Assets/PitTycoon/Unity/EconomySystem.cs`
- Create: `Assets/PitTycoon/Unity/CoinFlyVfx.cs`
- Create: `Assets/PitTycoon/Unity/SetController.cs`
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs` (Grow; Build called externally)
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs` (cash, set #, phase)

- [ ] **Step 1: Create `EconomySystem`**

`Assets/PitTycoon/Unity/EconomySystem.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Persistent cash. Wraps the pure EconomyCalculator: banks a finished set's
    /// hype (weighted peak + average) into cash and validates/executes purchases.
    /// </summary>
    public sealed class EconomySystem : MonoBehaviour
    {
        [SerializeField] private int startingCash = 0;
        [Tooltip("Cash = round(peakHype*peakWeight + avgHype*avgWeight).")]
        [SerializeField] private float peakWeight = 1f;
        [SerializeField] private float avgWeight = 1f;

        private EconomyCalculator _calc;

        public int Cash => _calc?.Cash ?? startingCash;

        public void Initialize()
        {
            _calc = new EconomyCalculator(startingCash);
        }

        /// <summary>Bank a finished set's hype to cash; returns the amount earned.</summary>
        public int Bank(float peakHype, float avgHype)
            => _calc.BankSet(peakHype, avgHype, peakWeight, avgWeight);

        public bool CanAfford(int cost) => _calc.CanAfford(cost);
        public bool TrySpend(int cost) => _calc.TrySpend(cost);
    }
}
```

- [ ] **Step 2: Create `CoinFlyVfx` (runtime coin burst)**

`Assets/PitTycoon/Unity/CoinFlyVfx.cs`:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Greybox "coins fly up" at set end: spawns small spheres that arc upward under
    /// gravity and self-destruct. Count scales with cash earned. Runtime-only, no wiring.
    /// </summary>
    public sealed class CoinFlyVfx : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _life;
        private const float MaxLife = 1.3f;

        public static void Burst(int cashEarned, Vector3 origin)
        {
            int count = Mathf.Clamp(cashEarned / 5, 6, 40);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Coin";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.position = origin + Random.insideUnitSphere * 0.8f;
                go.transform.localScale = Vector3.one * 0.35f;
                var coin = go.AddComponent<CoinFlyVfx>();
                coin._velocity = new Vector3(
                    Random.Range(-2f, 2f), Random.Range(5f, 8f), Random.Range(-2f, 2f));
            }
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= MaxLife) { Destroy(gameObject); return; }
            _velocity += Physics.gravity * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(180f * Time.deltaTime, 220f * Time.deltaTime, 0f);
        }
    }
}
```

- [ ] **Step 3: Modify `CrowdController` — `Grow` + external build**

In `Assets/PitTycoon/Unity/CrowdController.cs`, add this public method (e.g. after `Initialize`):

```csharp
        /// <summary>Increase grid size (capacity upgrade). Visible after the next Build().</summary>
        public void Grow(int addColumns, int addRows)
        {
            columns = Mathf.Clamp(columns + addColumns, 1, 40);
            rows = Mathf.Clamp(rows + addRows, 1, 30);
        }

        public int MemberCount => columns * rows;
```

(`Build()` is already public and rebuilds from the current `columns`/`rows`, so growth applies on the next set's `Build()`. `GameBootstrap` will no longer call `Build()` itself — `SetController` does, per Task 5.)

- [ ] **Step 4: Create `SetController`**

`Assets/PitTycoon/Unity/SetController.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Set-based state machine: Live ↔ Intermission. Owns track playback, detects the
    /// end of the track on the DSP clock, banks hype→cash, and broadcasts SetStarted/
    /// SetEnded. Auto-starts set 1 on Start(); the HUD calls StartNextSet() to continue.
    /// </summary>
    public sealed class SetController : MonoBehaviour
    {
        public enum Phase { Live, Intermission }

        [SerializeField] private AudioSource source;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private CrowdController crowd;

        private EventBus _bus;
        private double _setEndDsp;
        private bool _started;

        public Phase Current { get; private set; } = Phase.Intermission;
        public int SetNumber { get; private set; }
        public int LastCashEarned { get; private set; }

        public void Initialize(EventBus bus) { _bus = bus; }

        private void Start() => StartSet();

        /// <summary>Begin the next set (called once on Start, then by the HUD button).</summary>
        public void StartNextSet()
        {
            if (Current == Phase.Intermission) StartSet();
        }

        private void StartSet()
        {
            if (source == null || source.clip == null)
            {
                Debug.LogError("SetController: AudioSource/clip not assigned.", this);
                return;
            }

            SetNumber++;
            if (crowd != null) crowd.Build(); // picks up any capacity growth bought last intermission

            Current = Phase.Live;
            _bus?.Publish(new SetStarted(SetNumber)); // HypeSystem resets, ability arms

            source.Stop();
            source.time = 0f;
            source.Play();
            _setEndDsp = AudioSettings.dspTime + source.clip.length;
            _started = true;
        }

        private void Update()
        {
            if (Current != Phase.Live || !_started) return;
            if (AudioSettings.dspTime >= _setEndDsp) EndSet();
        }

        private void EndSet()
        {
            Current = Phase.Intermission;
            source.Stop();

            float peak = hype != null ? hype.Peak : 0f;
            float avg = hype != null ? hype.Average : 0f;
            int earned = economy != null ? economy.Bank(peak, avg) : 0;
            LastCashEarned = earned;

            // Coins fly up where the crowd is.
            Vector3 origin = crowd != null ? crowd.transform.position + Vector3.up * 2f : Vector3.up * 2f;
            CoinFlyVfx.Burst(earned, origin);

            _bus?.Publish(new SetEnded(peak, avg, earned));
        }
    }
}
```

- [ ] **Step 5: Add cash / set # / phase to the HUD**

In `Assets/PitTycoon/Unity/DebugHud.cs`, add fields:

```csharp
        [SerializeField] private EconomySystem economy;
        [SerializeField] private SetController setController;
```

And in `OnGUI()`, render a top-right status block (append after the ability block):

```csharp
            float rx = Screen.width - 220;
            if (economy != null)
                GUI.Label(new Rect(rx, 12, 200, 22), $"Cash: ${economy.Cash}");
            if (setController != null)
            {
                GUI.Label(new Rect(rx, 34, 200, 22), $"Set {setController.SetNumber} — {setController.Current}");
                if (setController.Current == SetController.Phase.Intermission && setController.LastCashEarned > 0)
                    GUI.Label(new Rect(rx, 56, 200, 22), $"Banked: +${setController.LastCashEarned}");
            }
```

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/EconomySystem.cs Assets/PitTycoon/Unity/CoinFlyVfx.cs Assets/PitTycoon/Unity/SetController.cs Assets/PitTycoon/Unity/CrowdController.cs Assets/PitTycoon/Unity/DebugHud.cs
git commit -m "feat(unity): SetController, economy banking + coins-fly (M1 step 6)"
```

---

### Task 5: UpgradeSystem + intermission shop + bigger next set (build-order steps 7–8)

**Files:**
- Create: `Assets/PitTycoon/Unity/UpgradeDefinition.cs`
- Create: `Assets/PitTycoon/Unity/UpgradeSystem.cs`
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs` (intermission shop + "Start Next Set")

- [ ] **Step 1: Create `UpgradeDefinition` SO**

`Assets/PitTycoon/Unity/UpgradeDefinition.cs`:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven passive upgrade (M1: the capacity / "grounds" upgrade only — the
    /// densest visible change). Buying it grows the crowd grid and raises the hype
    /// ceiling, so the next set is visibly bigger and can bank more.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Pit Tycoon/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id = "capacity";
        public string DisplayName = "Grounds Expansion";
        [Min(0)] public int Cost = 60;
        [Tooltip("Crowd columns added per purchase.")]
        public int AddColumns = 4;
        [Tooltip("Crowd rows added per purchase.")]
        public int AddRows = 2;
        [Tooltip("Hype ceiling added per purchase (bigger venue holds more hype).")]
        public float CeilingDelta = 25f;
    }
}
```

- [ ] **Step 2: Create `UpgradeSystem`**

`Assets/PitTycoon/Unity/UpgradeSystem.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns the available upgrade(s) and applies their effects. On purchase: spend via
    /// EconomySystem, grow the crowd + raise the hype ceiling, then broadcast
    /// UpgradePurchased. The visible payoff lands when the next set's crowd is built.
    /// </summary>
    public sealed class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] private UpgradeDefinition capacityUpgrade;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private CrowdController crowd;

        private EventBus _bus;

        public UpgradeDefinition CapacityUpgrade => capacityUpgrade;

        public void Initialize(EventBus bus) { _bus = bus; }

        public bool CanAfford(UpgradeDefinition upg)
            => upg != null && economy != null && economy.CanAfford(upg.Cost);

        /// <summary>Attempt to buy; applies effects and broadcasts on success.</summary>
        public bool TryPurchase(UpgradeDefinition upg)
        {
            if (upg == null || economy == null) return false;
            if (!economy.TrySpend(upg.Cost)) return false;

            if (crowd != null) crowd.Grow(upg.AddColumns, upg.AddRows);
            if (hype != null) hype.RaiseCeiling(upg.CeilingDelta);

            _bus?.Publish(new UpgradePurchased(upg.Id));
            return true;
        }
    }
}
```

- [ ] **Step 3: Add the intermission shop to the HUD**

In `Assets/PitTycoon/Unity/DebugHud.cs`, add the field:

```csharp
        [SerializeField] private UpgradeSystem upgrades;
```

And in `OnGUI()`, append the intermission panel (only shown between sets):

```csharp
            if (setController != null && setController.Current == SetController.Phase.Intermission
                && upgrades != null)
            {
                const float px = 12, py = 290, pw = 320;
                GUI.Box(new Rect(px, py, pw, 110), "INTERMISSION");
                var upg = upgrades.CapacityUpgrade;
                if (upg != null)
                {
                    bool afford = upgrades.CanAfford(upg);
                    GUI.enabled = afford;
                    if (GUI.Button(new Rect(px + 10, py + 26, pw - 20, 30),
                            $"Buy {upg.DisplayName} (${upg.Cost})  +{upg.AddColumns}x{upg.AddRows} crowd"))
                        upgrades.TryPurchase(upg);
                    GUI.enabled = true;
                    if (!afford)
                        GUI.Label(new Rect(px + 10, py + 58, pw - 20, 20), "Not enough cash.");
                }
                if (GUI.Button(new Rect(px + 10, py + 76, pw - 20, 28), "Start Next Set ▶"))
                    setController.StartNextSet();
            }
```

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/UpgradeDefinition.cs Assets/PitTycoon/Unity/UpgradeSystem.cs Assets/PitTycoon/Unity/DebugHud.cs
git commit -m "feat(unity): upgrade system + intermission shop + bigger next set (M1 steps 7-8)"
```

---

### Task 6: Composition root + editor scene builder

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`
- Modify: `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs`

- [ ] **Step 1: Rewrite `GameBootstrap` to construct & wire all systems**

`Assets/PitTycoon/Unity/GameBootstrap.cs` (full replacement):

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Single composition root (the "Startup.cs"): constructs the EventBus, bridges
    /// analyzer beats onto it, and Initialize(bus)s every system. Systems get their
    /// peer references via serialized fields (wired by the editor script), so nothing
    /// uses FindObjectOfType or singletons. Order: all Awakes (wiring) run before any
    /// Start (SetController auto-starts set 1 in its Start).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private WhirlpoolAbility ability;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private SetController setController;

        public EventBus Bus { get; private set; }

        private void Awake()
        {
            Bus = new EventBus();

            if (analyzer == null || crowd == null || hype == null || ability == null
                || economy == null || upgrades == null || setController == null)
            {
                Debug.LogError("GameBootstrap: one or more system references are not assigned.", this);
                return;
            }

            // Bridge raw analyzer beats onto the bus (ability, future consumers subscribe here).
            analyzer.BeatDetected += OnBeat;

            crowd.Initialize(analyzer);
            economy.Initialize();
            hype.Initialize(Bus);
            ability.Initialize(Bus);
            upgrades.Initialize(Bus);
            setController.Initialize(Bus);
            // SetController.Start() (after all Awakes) kicks off set 1.
        }

        private void OnDestroy()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat) => Bus.Publish(new BeatDetected(beat));
    }
}
```

- [ ] **Step 2: Rewrite the editor scene builder**

`Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs` (full replacement):

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// One-click greybox scene builder for the full M1 loop. Creates the SO assets,
    /// builds the GameObjects, and wires every serialized reference automatically.
    /// Menu: "Pit Tycoon/Build Greybox Scene".
    /// </summary>
    public static class PitTycoonSetup
    {
        private const string AudioConfigPath = "Assets/Settings/AudioAnalyzerConfig.asset";
        private const string AbilityPath = "Assets/Settings/AbilityDefinition.asset";
        private const string UpgradePath = "Assets/Settings/UpgradeDefinition.asset";
        private const string ScenePath = "Assets/Scenes/Greybox.unity";

        [MenuItem("Pit Tycoon/Build Greybox Scene")]
        public static void BuildGreyboxScene()
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Audio");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Assets created AFTER NewScene (NewScene's unused-asset unload would otherwise
            // destroy a freshly-created asset before anything references it -> wires as None).
            var audioConfig = LoadOrCreate<AudioAnalyzerConfig>(AudioConfigPath);
            var abilityDef = LoadOrCreate<AbilityDefinition>(AbilityPath);
            var upgradeDef = LoadOrCreate<UpgradeDefinition>(UpgradePath);

            // Camera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(35f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();

            // Light.
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ground.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 3f);

            // Audio + analyzer (SetController owns playback, so playOnAwake = false).
            var audioGo = new GameObject("Audio");
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;

            bool clipAssigned = false;
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" });
            if (clipGuids.Length > 0)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(clipGuids[0]));
                src.clip = clip;
                clipAssigned = clip != null;
            }

            var analyzer = audioGo.AddComponent<FftAudioAnalyzer>();
            WireRefs(analyzer, ("config", audioConfig), ("source", src));

            // Crowd.
            var crowdGo = new GameObject("Crowd");
            var crowd = crowdGo.AddComponent<CrowdController>();

            // Systems hub.
            var sysGo = new GameObject("Systems");
            var hype = sysGo.AddComponent<HypeSystem>();
            var ability = sysGo.AddComponent<WhirlpoolAbility>();
            var economy = sysGo.AddComponent<EconomySystem>();
            var upgrades = sysGo.AddComponent<UpgradeSystem>();
            var setController = sysGo.AddComponent<SetController>();

            WireRefs(hype, ("analyzer", analyzer));
            WireRefs(ability, ("definition", abilityDef), ("vfxAnchor", crowdGo.transform));
            WireRefs(upgrades,
                ("capacityUpgrade", upgradeDef), ("economy", economy), ("hype", hype), ("crowd", crowd));
            WireRefs(setController,
                ("source", src), ("hype", hype), ("economy", economy), ("crowd", crowd));

            // HUD (expanded DebugHud).
            var hudGo = new GameObject("DebugHud");
            var hud = hudGo.AddComponent<DebugHud>();
            WireRefs(hud,
                ("analyzer", analyzer), ("hype", hype), ("ability", ability),
                ("economy", economy), ("setController", setController), ("upgrades", upgrades));

            // Composition root.
            var bootGo = new GameObject("Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            WireRefs(boot,
                ("analyzer", analyzer), ("crowd", crowd), ("hype", hype), ("ability", ability),
                ("economy", economy), ("upgrades", upgrades), ("setController", setController));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            string next = clipAssigned
                ? "A clip from Assets/Audio was auto-assigned.\n\nPress Play:\n" +
                  "- Crowd bobs with intensity, pops on beats.\n" +
                  "- Hype meter fills; press SPACE on-beat to spike it (whirlpool).\n" +
                  "- When the track ends, hype banks to cash (coins fly), then the\n" +
                  "  intermission shop appears — buy capacity and Start Next Set."
                : "No clip found in Assets/Audio. Put a .wav/.ogg/.mp3 there and rebuild.";
            EditorUtility.DisplayDialog("Pit Tycoon", "Greybox loop scene built at:\n" + ScenePath + "\n\n" + next, "OK");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void WireRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var entry in refs)
            {
                var prop = so.FindProperty(entry.field);
                if (prop == null)
                {
                    Debug.LogError($"[PitTycoonSetup] Serialized field '{entry.field}' not found on {target.GetType().Name}.");
                    continue;
                }
                prop.objectReferenceValue = entry.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/GameBootstrap.cs Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs
git commit -m "feat(unity): wire full loop in bootstrap + editor scene builder"
```

---

### Task 7: Unity bring-up checkpoint + SETUP.md (the one Editor session)

**Files:**
- Modify: `SETUP.md`

- [ ] **Step 1: Update `SETUP.md`**

Replace the "Play" section and add a controls/loop section. In `SETUP.md`, update step 4–5 region to:

```markdown
4. **Rebuild the greybox scene.** Menu bar: **Pit Tycoon → Build Greybox Scene**.
   This regenerates `Assets/Scenes/Greybox.unity` with the full loop wired and creates
   `Assets/Settings/AbilityDefinition.asset` and `Assets/Settings/UpgradeDefinition.asset`.
   (The first compile after pulling also pulls in the `Unity.InputSystem` asmdef reference.)

5. **Play the loop.** Press Play. Expected:
   - Crowd bobs with intensity and pops on beats; the **Hype** meter fills live.
   - Press **SPACE** (or click **WHIRLPOOL**) — fire *on the beat* for an `xN ON-BEAT!`
     multiplier and a bigger whirlpool; hype visibly jumps. Off-beat does little.
   - When the track ends, hype **banks to cash** (coins fly up), and the
     **INTERMISSION** panel appears.
   - Click **Buy Grounds Expansion** (if affordable), then **Start Next Set ▶** — the
     next set's crowd is visibly **bigger/denser** and the hype ceiling is higher.
```

- [ ] **Step 2: Bring-up checkpoint (you, in the Editor)**

1. Pull `feat/m1-set-loop` and open the project in Unity (repo root).
2. Wait for compile. **Report any Console errors** — most likely candidates and fixes:
   - *"The type or namespace name 'InputSystem' does not exist"* → the asmdef reference
     didn't resolve; confirm `com.unity.inputsystem` is installed (it is, 1.19.0) and
     reimport. We adjust the asmdef together if needed.
   - Any `WireRefs ... field not found` error in the Console after the menu run → a field
     name mismatch; we fix the editor script string.
3. Run **Pit Tycoon → Build Greybox Scene**.
4. Press Play and walk the loop above. Report what does/doesn't read as fun/visible.

- [ ] **Step 3: Commit the generated scene + assets (after a clean play)**

```bash
git add Assets/Scenes/Greybox.unity Assets/Settings/AbilityDefinition.asset Assets/Settings/UpgradeDefinition.asset SETUP.md
git commit -m "chore(unity): regenerate Greybox scene with full loop + SETUP loop steps"
```

(Plus any `.meta` files Unity creates for the two new `.asset`s.)

- [ ] **Step 4: Finish the branch**

Use `superpowers:finishing-a-development-branch` to verify and open PR #3.

---

## Self-Review

**Spec coverage (sections 4 steps 4–8):**
- Step 4 (HypeCalculator + on-screen meter) → Task 2. ✓
- Step 5 (WhirlpoolAbility, on-beat bonus, greybox VFX) → Task 3. ✓
- Step 6 (SetController live→end→bank→intermission) → Task 4. ✓
- Step 7 (EconomyCalculator + shop, one capacity upgrade, visible change) → Tasks 4 (economy) + 5 (shop/upgrade). ✓
- Step 8 (loop to a bigger second set) → Task 5 (`Grow`) + Task 4 (`StartNextSet`/`Build`). ✓
- On-screen rule: meter fills (Task 2), coins fly (Task 4 `CoinFlyVfx`), crowd denser next set (Task 5 `Grow` + rebuild). ✓
- Data-driven abilities/upgrades as SOs (3.4) → `AbilityDefinition`, `UpgradeDefinition`. ✓
- No singletons; composition root wires via events/refs (3.2) → `GameBootstrap.Initialize(bus)`. ✓

**Type/name consistency check:**
- `HypeSystem`: `Peak`, `Average`, `Current`, `Ceiling`, `RaiseCeiling(float)`, `Initialize(EventBus)` — used by `SetController`/`UpgradeSystem`/`DebugHud` as written. ✓
- `EconomySystem`: `Initialize()` (no bus), `Bank(float,float)`, `CanAfford(int)`, `TrySpend(int)`, `Cash` — matches callers. ✓
- `WhirlpoolAbility`: `Ready`, `CooldownRemaining`, `CooldownTotal`, `LastMultiplier`, `Fire()`, `Initialize(EventBus)` — matches HUD. ✓
- `SetController`: `Phase` enum, `Current`, `SetNumber`, `LastCashEarned`, `StartNextSet()`, `Initialize(EventBus)` — matches HUD/bootstrap. ✓
- `UpgradeSystem`: `CapacityUpgrade`, `CanAfford(UpgradeDefinition)`, `TryPurchase(UpgradeDefinition)`, `Initialize(EventBus)` — matches HUD. ✓
- `CrowdController`: `Grow(int,int)`, `Build()`, `Initialize(IAudioAnalyzer)` — matches. ✓
- Editor `WireRefs` field strings match every `[SerializeField]` name above (analyzer, source, config, definition, vfxAnchor, capacityUpgrade, economy, hype, crowd, setController, ability, upgrades). ✓ — this is the one place a typo silently wires None, so it's re-verified at the Task 7 checkpoint.

**Placeholder scan:** none — every step has complete code or an exact command.

---

## Execution Handoff

Two execution options:
1. **Inline Execution (matches how the foundation was built)** — I author Tasks 1–6 in this session (Domain test-verified, Unity authored without compile), then we hit the single Unity bring-up checkpoint (Task 7) together.
2. **Subagent-Driven** — a fresh subagent per task with review between. Less useful here since Tasks 2–6 can't be compile-verified until the one Editor checkpoint anyway.
