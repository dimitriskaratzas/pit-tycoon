# Pit Tycoon M3b — Ability Roster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single hardcoded whirlpool with a data-driven, purchasable ability roster (Whirlpool + Light-burst + Woofer), with cooldown/on-beat logic in the tested Domain layer.

**Architecture:** A pure-C# `Ability` (Domain, TDD) holds cooldown + owned + on-beat scoring (reusing `BeatWindow`). A Unity `AbilitySystem` owns one shared `BeatGrid`, builds `Ability`s from `AbilityDefinition` SOs, routes input (Space + buttons/hotkeys), spawns VFX, pops the crowd, and handles purchases via `EconomySystem`. It replaces `WhirlpoolAbility`.

**Tech Stack:** .NET 10 (Domain tests, NUnit 4 constraint style), Unity 6 / URP 17.4, New Input System. PC-first.

> **Verification model:** Domain `Ability` is real TDD via `dotnet test PitTycoon.Domain.slnx`. The Unity layer has no automated tests — it's verified at the manual in-editor checkpoint (Task 10). The Domain suite (currently 50) must stay green and grow.

---

## File structure

```
Assets/PitTycoon/Domain/
  Ability.cs            (new) pure-C# ability: cooldown, owned, Fire()->FireResult; reuses BeatWindow
  Events.cs             (modify) + AbilityUnlocked event
tests/PitTycoon.Domain.Tests/
  AbilityTests.cs       (new) TDD for Ability
Assets/PitTycoon/Unity/
  AbilityDefinition.cs  (modify) + Cost, OwnedFromStart, AbilityTrigger, VfxKind, HudColor (+ the two enums)
  AbilitySystem.cs      (new) the conductor; replaces WhirlpoolAbility
  WhirlpoolAbility.cs   (delete)
  LightBurstVfx.cs      (new) code-only screen flash
  CrowdController.cs    (modify) + public Pop(strength)
  GameBootstrap.cs      (modify) ability -> abilities (AbilitySystem)
  DebugHud.cs           (modify) buy (intermission) + fire (live) buttons for the roster
  Editor/PitTycoonSetup.cs (modify) create 3 ability SOs, add AbilitySystem, rewire
SETUP.md                (modify) + M3b section
```

---

### Task 1: Domain `Ability` (TDD)

**Files:**
- Create: `tests/PitTycoon.Domain.Tests/AbilityTests.cs`
- Create: `Assets/PitTycoon/Domain/Ability.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class AbilityTests
    {
        private static Ability Make(bool owned = true, float baseSpike = 4f, float maxMult = 6f,
            double tol = 0.1, double cooldown = 0.5)
            => new Ability("test", baseSpike, maxMult, tol, cooldown, owned);

        [Test]
        public void NotOwned_CannotFire_AndUnlockOwns()
        {
            var a = Make(owned: false);
            Assert.That(a.Owned, Is.False);
            Assert.That(a.CanFire, Is.False);
            Assert.That(a.Fire(1.0, 1.0).Fired, Is.False);
            a.Unlock();
            Assert.That(a.Owned, Is.True);
            Assert.That(a.CanFire, Is.True);
        }

        [Test]
        public void OnBeatFire_GivesMaxMultiplier_AndPerfect()
        {
            var a = Make();
            var r = a.Fire(2.0, 2.0); // fire exactly on the beat
            Assert.That(r.Fired, Is.True);
            Assert.That(r.Multiplier, Is.EqualTo(6f).Within(1e-4));
            Assert.That(r.HypeAdded, Is.EqualTo(24f).Within(1e-3)); // 4 * 6
            Assert.That(r.Quality, Is.EqualTo(HitQuality.Perfect));
        }

        [Test]
        public void OffBeatFire_GivesNoBonus_AndMiss()
        {
            var a = Make(tol: 0.1);
            var r = a.Fire(2.5, 2.0); // 0.5s off a 0.1s window
            Assert.That(r.Multiplier, Is.EqualTo(1f).Within(1e-4));
            Assert.That(r.HypeAdded, Is.EqualTo(4f).Within(1e-3));
            Assert.That(r.Quality, Is.EqualTo(HitQuality.Miss));
        }

        [Test]
        public void Fire_StartsCooldown_BlocksUntilTicked()
        {
            var a = Make(cooldown: 0.5);
            Assert.That(a.Fire(2.0, 2.0).Fired, Is.True);
            Assert.That(a.CanFire, Is.False);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(a.Fire(2.05, 2.0).Fired, Is.False); // still cooling
            a.Tick(0.5);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(a.CanFire, Is.True);
        }

        [Test]
        public void Tick_DoesNotDriveCooldownNegative()
        {
            var a = Make(cooldown: 0.5);
            a.Fire(2.0, 2.0);
            a.Tick(10.0);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void MaxMultiplierBelowOne_IsClampedToOne()
        {
            var a = new Ability("x", 4f, 0.5f, 0.1, 0.5, true);
            var r = a.Fire(2.0, 2.0);
            Assert.That(r.Multiplier, Is.EqualTo(1f).Within(1e-4));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL (compile error — `Ability` / `FireResult` / `HitQuality` not defined).

- [ ] **Step 3: Implement `Ability`**

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>On-beat hit quality, shared by every ability (was on WhirlpoolAbility).</summary>
    public enum HitQuality { None, Miss, Good, Perfect }

    /// <summary>Result of attempting to fire an ability.</summary>
    public readonly struct FireResult
    {
        public bool Fired { get; }
        public float Multiplier { get; }
        public float HypeAdded { get; }
        public HitQuality Quality { get; }

        public FireResult(bool fired, float multiplier, float hypeAdded, HitQuality quality)
        {
            Fired = fired; Multiplier = multiplier; HypeAdded = hypeAdded; Quality = quality;
        }

        public static readonly FireResult NotFired = new FireResult(false, 0f, 0f, HitQuality.None);
    }

    /// <summary>
    /// One purchasable ability. Pure C#: owns its cooldown + owned flag, and scores a fire
    /// against the nearest beat via <see cref="BeatWindow"/>. The Unity layer feeds it the
    /// shared BeatGrid's nearest-beat time and applies the VFX/hype side effects.
    /// </summary>
    public sealed class Ability
    {
        private readonly float _baseSpike;
        private readonly float _maxMultiplier;
        private readonly double _tolerance;
        private readonly double _cooldown;

        public string Id { get; }
        public bool Owned { get; private set; }
        public double CooldownRemaining { get; private set; }
        public bool CanFire => Owned && CooldownRemaining <= 0.0;

        public Ability(string id, float baseSpike, float maxMultiplier,
            double toleranceSeconds, double cooldown, bool ownedFromStart)
        {
            Id = id;
            _baseSpike = baseSpike;
            _maxMultiplier = maxMultiplier < 1f ? 1f : maxMultiplier;
            _tolerance = toleranceSeconds;
            _cooldown = cooldown;
            Owned = ownedFromStart;
        }

        public void Unlock() => Owned = true;

        public void Tick(double dt)
        {
            if (CooldownRemaining <= 0.0) return;
            CooldownRemaining -= dt;
            if (CooldownRemaining < 0.0) CooldownRemaining = 0.0;
        }

        public FireResult Fire(double now, double nearestBeatDspTime)
        {
            if (!CanFire) return FireResult.NotFired;
            float mult = BeatWindow.Multiplier(nearestBeatDspTime, now, _tolerance, _maxMultiplier);
            float hypeAdded = _baseSpike * mult;
            float onBeat01 = (mult - 1f) / Math.Max(0.0001f, _maxMultiplier - 1f);
            HitQuality quality = onBeat01 >= 0.66f ? HitQuality.Perfect
                               : onBeat01 >= 0.25f ? HitQuality.Good
                               : HitQuality.Miss;
            CooldownRemaining = _cooldown;
            return new FireResult(true, mult, hypeAdded, quality);
        }
    }
}
```

> Note: confirm `BeatWindow.Multiplier(double beatDspTime, double fireDspTime, double tolerance, float maxMultiplier)` matches the existing signature (it's the one `BeatGridTests` and `WhirlpoolAbility` call). If tolerance is typed `float` there, change `_tolerance` to `float`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS (56 tests — the original 50 plus these 6).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Domain/Ability.cs" "tests/PitTycoon.Domain.Tests/AbilityTests.cs"
git commit -m "feat(domain): Ability — cooldown + owned + on-beat scoring (TDD, M3b)"
```

---

### Task 2: AbilityUnlocked event

**Files:**
- Modify: `Assets/PitTycoon/Domain/Events.cs`

- [ ] **Step 1: Append the event struct**

After the `SetStarted` struct (end of the file, before the closing brace), add:

```csharp

    /// <summary>Raised when an ability is purchased in the intermission shop.</summary>
    public readonly struct AbilityUnlocked
    {
        public string AbilityId { get; }
        public AbilityUnlocked(string abilityId) { AbilityId = abilityId; }
    }
```

- [ ] **Step 2: Verify Domain still builds**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS (56 tests).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Domain/Events.cs"
git commit -m "feat(domain): AbilityUnlocked event (M3b)"
```

---

### Task 3: Extend AbilityDefinition

**Files:**
- Modify: `Assets/PitTycoon/Unity/AbilityDefinition.cs`

- [ ] **Step 1: Add the enums + fields**

Replace the whole file with:

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>How an ability is triggered.</summary>
    public enum AbilityTrigger { Spacebar, Button }

    /// <summary>Which VFX an ability spawns on fire.</summary>
    public enum VfxKind { Whirlpool, LightBurst, Woofer }

    /// <summary>
    /// Data-driven ability. Adding an ability = a new asset (no code) as long as it reuses an
    /// existing VfxKind. On-beat firing multiplies BaseSpike up to MaxMultiplier within tolerance.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Pit Tycoon/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string Id = "whirlpool";
        public string DisplayName = "Whirlpool";
        [Tooltip("Hype added off-beat (the floor). On-beat multiplies this up to MaxMultiplier.")]
        public float BaseSpike = 4f;
        [Tooltip("Multiplier when fired exactly on a beat (falls to 1.0 at the window edge).")]
        [Min(1f)] public float MaxMultiplier = 6f;
        [Tooltip("Seconds either side of a beat that still count as on-beat.")]
        public float ToleranceSeconds = 0.1f;
        [Tooltip("Seconds between fires.")]
        public float Cooldown = 0.5f;

        [Header("Roster (M3b)")]
        [Tooltip("Shop price. 0 + OwnedFromStart for the starter whirlpool.")]
        [Min(0)] public int Cost = 0;
        public bool OwnedFromStart = false;
        [Tooltip("Spacebar = the timed/rhythm ability; Button = a click/hotkey purchasable.")]
        public AbilityTrigger Trigger = AbilityTrigger.Button;
        public VfxKind Vfx = VfxKind.Whirlpool;
        public Color HudColor = Color.white;
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: Unity recompiles with no errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/AbilityDefinition.cs"
git commit -m "feat(unity): AbilityDefinition roster fields + triggers/VFX enums (M3b)"
```

---

### Task 4: LightBurstVfx

**Files:**
- Create: `Assets/PitTycoon/Unity/LightBurstVfx.cs`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Self-spawning screen flash for the Light-burst ability. Code-only (IMGUI overlay),
    /// brief and capped below full white for comfort; player-triggered + cooldown-gated, so
    /// it avoids the every-beat concern that parked the M2c full-frame impact frames.
    /// </summary>
    public sealed class LightBurstVfx : MonoBehaviour
    {
        private const float MaxLife = 0.35f;
        private const float PeakAlpha = 0.55f;
        private float _life;
        private Color _color = new Color(1f, 0.95f, 0.7f);

        public static void Flash() => Flash(new Color(1f, 0.95f, 0.7f));

        public static void Flash(Color color)
        {
            var go = new GameObject("LightBurst");
            go.AddComponent<LightBurstVfx>()._color = color;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= MaxLife) Destroy(gameObject);
        }

        private void OnGUI()
        {
            float a = Mathf.Clamp01(1f - _life / MaxLife) * PeakAlpha;
            Color prev = GUI.color;
            GUI.color = new Color(_color.r, _color.g, _color.b, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no Console errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/LightBurstVfx.cs"
git commit -m "feat(vfx): LightBurstVfx screen flash (M3b)"
```

---

### Task 5: CrowdController.Pop

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs`

- [ ] **Step 1: Add the public Pop method**

After the `OnBeat` method (which sets `_pop`), add:

```csharp
        /// <summary>One-shot crowd jolt (an ability fired). Visible on the next Update.</summary>
        public void Pop(float strength)
        {
            _pop = Mathf.Max(_pop, strength);
        }
```

- [ ] **Step 2: Verify compile + Domain still green**

Expected: no Console errors. `dotnet test PitTycoon.Domain.slnx` → PASS (56).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/CrowdController.cs"
git commit -m "feat(unity): CrowdController.Pop one-shot jolt for ability fires (M3b)"
```

---

### Task 6: AbilitySystem (replaces WhirlpoolAbility)

**Files:**
- Create: `Assets/PitTycoon/Unity/AbilitySystem.cs`
- Delete: `Assets/PitTycoon/Unity/WhirlpoolAbility.cs`

- [ ] **Step 1: Write AbilitySystem**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven ability roster. Owns one shared BeatGrid (fed by BeatDetected), builds a
    /// Domain Ability per AbilityDefinition, routes input (Space fires the Spacebar ability;
    /// owned Button abilities fire via HUD buttons / number hotkeys), spawns VFX by VfxKind,
    /// pops the crowd, and handles purchases. Live-phase only. Replaces WhirlpoolAbility.
    /// </summary>
    public sealed class AbilitySystem : MonoBehaviour
    {
        [SerializeField] private List<AbilityDefinition> definitions = new List<AbilityDefinition>();
        [SerializeField] private EconomySystem economy;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private Transform vfxAnchor;
        [SerializeField] private float wooferRadius = 7f;
        [SerializeField] private float crowdPopStrength = 0.9f;

        private readonly BeatGrid _grid = new BeatGrid();
        private readonly List<Ability> _abilities = new List<Ability>();
        private readonly Dictionary<Ability, AbilityDefinition> _defOf = new Dictionary<Ability, AbilityDefinition>();
        private EventBus _bus;
        private bool _live;

        public IReadOnlyList<Ability> Abilities => _abilities;
        public AbilityDefinition DefinitionOf(Ability a) => _defOf[a];
        public HitQuality LastQuality { get; private set; }
        public string LastFiredId { get; private set; }

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _abilities.Clear();
            _defOf.Clear();
            foreach (var d in definitions)
            {
                if (d == null) continue;
                var a = new Ability(d.Id, d.BaseSpike, d.MaxMultiplier, d.ToleranceSeconds, d.Cooldown, d.OwnedFromStart);
                _abilities.Add(a);
                _defOf[a] = d;
            }
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

        private void OnBeat(BeatDetected e) => _grid.Register(e.Beat.DspTime);
        private void OnSetStarted(SetStarted e) { _live = true; LastQuality = HitQuality.None; }
        private void OnSetEnded(SetEnded e) => _live = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var a in _abilities) a.Tick(dt);
            if (!_live) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) FireFirstWithTrigger(AbilityTrigger.Spacebar);
            if (kb.digit1Key.wasPressedThisFrame) FireButtonIndex(0);
            if (kb.digit2Key.wasPressedThisFrame) FireButtonIndex(1);
            if (kb.digit3Key.wasPressedThisFrame) FireButtonIndex(2);
        }

        private void FireFirstWithTrigger(AbilityTrigger trigger)
        {
            foreach (var a in _abilities)
                if (_defOf[a].Trigger == trigger) { TryFire(a); return; }
        }

        private void FireButtonIndex(int idx)
        {
            int i = 0;
            foreach (var a in _abilities)
            {
                if (_defOf[a].Trigger != AbilityTrigger.Button) continue;
                if (i == idx) { TryFire(a); return; }
                i++;
            }
        }

        /// <summary>Fire an ability (HUD button or hotkey). No-op if not live / not ready.</summary>
        public void TryFire(Ability a)
        {
            if (!_live || a == null || !a.CanFire) return;
            double now = AudioSettings.dspTime;
            double nearest = _grid.NearestBeatTime(now);
            FireResult r = a.Fire(now, nearest);
            if (!r.Fired) return;

            AbilityDefinition def = _defOf[a];
            LastQuality = r.Quality;
            LastFiredId = def.Id;

            float onBeat01 = (r.Multiplier - 1f) / Mathf.Max(0.0001f, def.MaxMultiplier - 1f);
            Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
            switch (def.Vfx)
            {
                case VfxKind.Whirlpool: WhirlpoolVfx.Spawn(pos, onBeat01); break;
                case VfxKind.Woofer: ShockwaveVfx.Spawn(pos, wooferRadius); break;
                case VfxKind.LightBurst: LightBurstVfx.Flash(def.HudColor); break;
            }

            if (crowd != null) crowd.Pop(crowdPopStrength * Mathf.Clamp(r.Multiplier * 0.3f, 0.5f, 2f));
            _bus.Publish(new AbilityFired(def.Id, r.Multiplier, r.HypeAdded));
        }

        // ---- Purchases (intermission) ----
        public bool CanAfford(AbilityDefinition def)
            => def != null && economy != null && economy.CanAfford(def.Cost);

        public bool TryUnlock(AbilityDefinition def)
        {
            if (def == null || economy == null) return false;
            Ability a = _abilities.Find(x => _defOf[x] == def);
            if (a == null || a.Owned) return false;
            if (!economy.TrySpend(def.Cost)) return false;
            a.Unlock();
            _bus?.Publish(new AbilityUnlocked(def.Id));
            return true;
        }
    }
}
```

- [ ] **Step 2: Delete WhirlpoolAbility**

```bash
git rm "Assets/PitTycoon/Unity/WhirlpoolAbility.cs"
```
(Its meta will be removed too; Unity regenerates AbilitySystem's meta on import.)

- [ ] **Step 3: Verify compile**

Expected: Console errors *only* where `WhirlpoolAbility` is still referenced (`GameBootstrap`, `DebugHud`, `PitTycoonSetup`) — fixed in Tasks 7–9. The AbilitySystem itself must compile.

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/AbilitySystem.cs"
git commit -m "feat(unity): AbilitySystem data-driven roster, replaces WhirlpoolAbility (M3b)"
```

---

### Task 7: GameBootstrap swap

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

- [ ] **Step 1: Swap the field**

Replace `[SerializeField] private WhirlpoolAbility ability;` with:

```csharp
        [SerializeField] private AbilitySystem abilities;
```

- [ ] **Step 2: Update the null-check and Initialize**

In the null-check, replace `ability == null` with `abilities == null`. Then replace `ability.Initialize(Bus);` with:

```csharp
            abilities.Initialize(Bus);
```

- [ ] **Step 3: Verify compile**

Expected: GameBootstrap compiles; remaining errors only in `DebugHud`/`PitTycoonSetup` (next tasks).

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/GameBootstrap.cs"
git commit -m "feat(unity): GameBootstrap wires AbilitySystem (M3b)"
```

---

### Task 8: DebugHud — buy + fire buttons

**Files:**
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs`

- [ ] **Step 1: Swap the field**

Replace `[SerializeField] private WhirlpoolAbility ability;` with:

```csharp
        [SerializeField] private AbilitySystem abilities;
```

- [ ] **Step 2: Replace the live ability block**

Replace the whole `if (ability != null) { ... }` block (the WHIRLPOOL button + quality label) with:

```csharp
            if (abilities != null && setController != null
                && setController.Current == SetController.Phase.Live)
            {
                float ay = 234f;
                foreach (var a in abilities.Abilities)
                {
                    if (!a.Owned) continue;
                    var def = abilities.DefinitionOf(a);
                    bool ready = a.CanFire;
                    string label = ready ? $"{def.DisplayName}" : $"{def.DisplayName} {a.CooldownRemaining:0.0}s";
                    GUI.enabled = ready;
                    if (GUI.Button(new Rect(12, ay, 200, 28), label)) abilities.TryFire(a);
                    GUI.enabled = true;
                    ay += 32f;
                }
                if (abilities.LastQuality != PitTycoon.Domain.HitQuality.None)
                {
                    Color prevQ = GUI.color;
                    GUI.color = abilities.LastQuality switch
                    {
                        PitTycoon.Domain.HitQuality.Perfect => new Color(0.3f, 1f, 0.4f),
                        PitTycoon.Domain.HitQuality.Good => new Color(1f, 0.9f, 0.3f),
                        _ => new Color(0.7f, 0.7f, 0.7f),
                    };
                    GUI.Label(new Rect(220, 238, 180, 22), abilities.LastQuality.ToString().ToUpperInvariant());
                    GUI.color = prevQ;
                }
            }
```

- [ ] **Step 3: Add ability buy buttons in the intermission panel**

Inside the intermission `if (... Phase.Intermission ... upgrades != null)` block, after the "Start Next Set" button, add ability buys (still inside that block):

```csharp
                float by = py + 110f;
                if (abilities != null)
                {
                    foreach (var a in abilities.Abilities)
                    {
                        if (a.Owned) continue;
                        var def = abilities.DefinitionOf(a);
                        bool afford = abilities.CanAfford(def);
                        GUI.enabled = afford;
                        if (GUI.Button(new Rect(px + 10, by, pw - 20, 26), $"Buy {def.DisplayName} (${def.Cost})"))
                            abilities.TryUnlock(def);
                        GUI.enabled = true;
                        by += 30f;
                    }
                }
```

> The intermission `GUI.Box` height (currently `110`) should grow to fit the new buttons — change that box's height from `110` to `200`.

- [ ] **Step 4: Verify compile**

Expected: DebugHud compiles; only `PitTycoonSetup` errors remain.

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/DebugHud.cs"
git commit -m "feat(unity): DebugHud roster buy + fire buttons (M3b)"
```

---

### Task 9: Editor setup — create the 3 ability assets + rewire

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs`

- [ ] **Step 1: Replace the ability creation + Systems wiring**

In `BuildGreyboxScene`, the M1 setup currently creates one `abilityDef` and adds a `WhirlpoolAbility`. Replace the `abilityDef` creation and the `ability` component/wiring with the roster. Specifically:

Replace `var abilityDef = LoadOrCreate<AbilityDefinition>(AbilityPath);` with:

```csharp
            var whirlpoolDef = LoadOrCreate<AbilityDefinition>("Assets/Settings/Ability_Whirlpool.asset");
            ConfigureAbility(whirlpoolDef, "whirlpool", "Whirlpool", 0, true, AbilityTrigger.Spacebar, VfxKind.Whirlpool,
                new Color(0.3f, 0.8f, 1f), baseSpike: 4f, maxMult: 6f, tol: 0.1f, cooldown: 0.5f);
            var lightDef = LoadOrCreate<AbilityDefinition>("Assets/Settings/Ability_LightBurst.asset");
            ConfigureAbility(lightDef, "lightburst", "Light-burst", 80, false, AbilityTrigger.Button, VfxKind.LightBurst,
                new Color(1f, 0.9f, 0.4f), baseSpike: 8f, maxMult: 5f, tol: 0.12f, cooldown: 4f);
            var wooferDef = LoadOrCreate<AbilityDefinition>("Assets/Settings/Ability_Woofer.asset");
            ConfigureAbility(wooferDef, "woofer", "Woofer", 120, false, AbilityTrigger.Button, VfxKind.Woofer,
                new Color(0.88f, 0.27f, 0.49f), baseSpike: 10f, maxMult: 5f, tol: 0.12f, cooldown: 5f);
```

Replace the `var ability = sysGo.AddComponent<WhirlpoolAbility>();` line with:

```csharp
            var abilities = sysGo.AddComponent<AbilitySystem>();
```

Replace the `WireRefs(ability, ...)` call with:

```csharp
            WireAbilityList(abilities, new[] { whirlpoolDef, lightDef, wooferDef });
            WireRefs(abilities, ("economy", economy), ("crowd", crowd), ("vfxAnchor", crowdGo.transform));
```

In the `WireRefs(boot, ...)` call, replace `("ability", ability)` with `("abilities", abilities)`.
In the `WireRefs(hud, ...)` call, replace `("ability", ability)` with `("abilities", abilities)`.

- [ ] **Step 2: Add the helper methods**

Add to the class:

```csharp
        private static void ConfigureAbility(AbilityDefinition def, string id, string name, int cost,
            bool owned, AbilityTrigger trigger, VfxKind vfx, Color hud,
            float baseSpike, float maxMult, float tol, float cooldown)
        {
            var so = new SerializedObject(def);
            so.FindProperty("Id").stringValue = id;
            so.FindProperty("DisplayName").stringValue = name;
            so.FindProperty("BaseSpike").floatValue = baseSpike;
            so.FindProperty("MaxMultiplier").floatValue = maxMult;
            so.FindProperty("ToleranceSeconds").floatValue = tol;
            so.FindProperty("Cooldown").floatValue = cooldown;
            so.FindProperty("Cost").intValue = cost;
            so.FindProperty("OwnedFromStart").boolValue = owned;
            so.FindProperty("Trigger").enumValueIndex = (int)trigger;
            so.FindProperty("Vfx").enumValueIndex = (int)vfx;
            so.FindProperty("HudColor").colorValue = hud;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        private static void WireAbilityList(Object target, AbilityDefinition[] defs)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty("definitions");
            list.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
```

- [ ] **Step 3: Remove the stale AbilityPath constant**

Delete the `private const string AbilityPath = "Assets/Settings/AbilityDefinition.asset";` line (replaced by the three explicit paths). The old `Assets/Settings/AbilityDefinition.asset` can be left on disk (unused) or deleted in Unity; the scene no longer references it.

- [ ] **Step 4: Verify compile**

Expected: the whole project compiles with no Console errors (all `WhirlpoolAbility` references are gone).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs"
git commit -m "feat(editor): create ability roster assets + wire AbilitySystem (M3b)"
```

---

### Task 10: In-editor checkpoint (user)

**No file changes.** Manual gate.

- [ ] **Step 1: Rebuild**

Pull `feat/m3b-ability-roster`, let Unity recompile. Run **Pit Tycoon → Build Greybox Scene** (regenerates the scene with `AbilitySystem` + the 3 ability assets), then re-apply **Comic Look** and **Build Festival Scene** (M2a/M2b) on top.

- [ ] **Step 2: Verify against the checklist**

Press Play and confirm:
- **Whirlpool unchanged**: Space still fires it with the same on-beat feel + PERFECT/GOOD/MISS.
- **Buy in intermission**: Light-burst / Woofer show as buy buttons; buying spends cash and they become owned.
- **Fire owned abilities**: live HUD shows fire buttons (+ number hotkeys 1/2/3); firing spawns the right VFX (flash / shockwave), **pops the crowd**, and spikes hype.
- Cooldowns + on-beat quality read correctly; off-beat does little.
- M1 loop intact; Light-burst flash is comfortable.

- [ ] **Step 3: Tune & report**

Adjust costs / cooldowns / base spikes on the ability assets to taste; report feel + a clip.

---

### Task 11: Commit generated assets + open PR #7

**After Task 10 confirms.**

- [ ] **Step 1: Stage**

```bash
git status --short
git add "Assets/Settings/Ability_Whirlpool.asset" "Assets/Settings/Ability_Whirlpool.asset.meta" \
        "Assets/Settings/Ability_LightBurst.asset" "Assets/Settings/Ability_LightBurst.asset.meta" \
        "Assets/Settings/Ability_Woofer.asset" "Assets/Settings/Ability_Woofer.asset.meta" \
        "Assets/PitTycoon/Unity/AbilitySystem.cs.meta" "Assets/PitTycoon/Unity/LightBurstVfx.cs.meta" \
        "Assets/PitTycoon/Domain/Ability.cs.meta" "Assets/Scenes/Greybox.unity"
git status --short
```
Discard line-ending-only churn with `git checkout -- <file>`. If the old `Assets/Settings/AbilityDefinition.asset` was deleted in Unity, stage that removal too.

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(unity): generated ability roster assets + scene wiring (M3b)"
```

- [ ] **Step 3: Push + PR #7**

```bash
git push -u origin feat/m3b-ability-roster
gh pr create --base master --head feat/m3b-ability-roster \
  --title "M3b: data-driven ability roster (whirlpool + light-burst + woofer)" \
  --body "Generalizes the one hardcoded whirlpool into a purchasable, data-driven roster. On-beat + cooldown logic moves into a unit-tested Domain Ability (suite now 56 green); a Unity AbilitySystem owns the shared BeatGrid, routes input (Space + buttons/hotkeys), spawns VFX, pops the crowd, and handles shop purchases. Whirlpool feel preserved. Adds Light-burst (screen flash) + Woofer (shockwave). First of the M3 sub-milestones (M3a upgrades / M3c tracks / M3d UI to follow). Spec: docs/superpowers/specs/2026-06-18-pit-tycoon-m3b-ability-roster-design.md."
```

---

## Self-Review

**1. Spec coverage:**
- Domain `Ability` (cooldown, owned, on-beat via BeatWindow, quality) → Task 1 (TDD). ✓
- `AbilityUnlocked` event → Task 2. ✓
- Shared `BeatGrid` → Task 6 (one instance in AbilitySystem). ✓
- AbilityDefinition extended (Cost, OwnedFromStart, Trigger, VfxKind, HudColor) → Task 3. ✓
- AbilitySystem (input Space + button/hotkey, publish AbilityFired, VFX dispatch, crowd pop, TryUnlock) replacing WhirlpoolAbility → Task 6. ✓
- Light-burst VFX + Woofer reuse + crowd pop → Tasks 4, 5, 6. ✓
- Three abilities (Whirlpool/Light-burst/Woofer) → Task 9. ✓
- HUD buy + fire → Task 8. ✓
- Wiring (GameBootstrap, editor) → Tasks 7, 9. ✓
- Verification (Domain TDD + manual checkpoint, whirlpool preserved) → Task 1 step 4, Task 10. ✓
- Out of scope (M3a/c/d, spatial hype) → respected. ✓

**2. Placeholder scan:** No TBD/TODO. Each code step has complete content. The `BeatWindow.Multiplier` signature note (Task 1) is a verification instruction, not a placeholder.

**3. Type/name consistency:** `Ability(string,float,float,double,double,bool)` ctor matches the call in `AbilitySystem.Initialize` (Task 6) and `AbilityTests.Make` (Task 1). `FireResult` fields (`Fired`, `Multiplier`, `HypeAdded`, `Quality`) used consistently in Task 1 + Task 6. `HitQuality` defined in Domain (Task 1), referenced in `DebugHud` as `PitTycoon.Domain.HitQuality` (Task 8) and exposed by `AbilitySystem.LastQuality` (Task 6). Serialized names `definitions`/`economy`/`crowd`/`vfxAnchor` on AbilitySystem (Task 6) match the `WireRefs`/`WireAbilityList` calls (Task 9). `abilities` field name matches between GameBootstrap (Task 7), DebugHud (Task 8), and the editor wiring (Task 9). Enum order (`AbilityTrigger { Spacebar, Button }`, `VfxKind { Whirlpool, LightBurst, Woofer }`, Task 3) matches the `(int)` casts in `ConfigureAbility` (Task 9). `AbilityUnlocked(string)` (Task 2) matches the publish in Task 6.
