# Crowd Dynamics Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed-grid crowd with an organic pit that fills toward venue capacity as hype climbs, keeps a persistent following that ratchets up across sets, and feeds that fill back into hype gain — with Grounds repurposed to raise capacity.

**Architecture:** A pure-C# `CrowdFill` (Domain, TDD) holds `Capacity`/`Following`/`Active` and the ratcheting rules. `CrowdController` owns one `CrowdFill`, renders it as a pre-built pool of members that scale in front-to-back, and exposes `FillFraction` via `ICrowdMeter`. `HypeSystem` reads `ICrowdMeter` and multiplies its passive rate by the fill; `CrowdController` reads `IHypeMeter` for the hype fraction that drives the fill. `GameBootstrap` cross-wires the two through those interfaces. Grounds' `UpgradeDefinition` gains `AddCapacity`, applied via `CrowdController.RaiseCapacity`.

**Tech Stack:** Unity 6 LTS / URP, C#; Domain layer is plain netstandard2.1 tested with NUnit 4 via `dotnet test PitTycoon.Domain.slnx`. New Input System. Commits: imperative present tense, **no `Co-Authored-By` trailer**.

---

## File Structure

**Domain (pure C#, unit-tested):**
- Create `Assets/PitTycoon/Domain/CrowdFill.cs` — the fill model (Capacity/Following/Active, ratcheting).
- Create `Assets/PitTycoon/Domain/IHypeMeter.cs` — `{ float HypeFraction }`.
- Create `Assets/PitTycoon/Domain/ICrowdMeter.cs` — `{ float FillFraction }`.
- Create `tests/PitTycoon.Domain.Tests/CrowdFillTests.cs` — `CrowdFill` unit tests.

**Unity (MonoBehaviours):**
- Modify `Assets/PitTycoon/Unity/HypeSystem.cs` — implement `IHypeMeter`, add `minFillMultiplier`, `SetCrowdMeter`, fill-multiplied rate.
- Modify `Assets/PitTycoon/Unity/CrowdController.cs` — own `CrowdFill`, pooled scale-in render, set lifecycle, `RaiseCapacity`, implement `ICrowdMeter`.
- Modify `Assets/PitTycoon/Unity/UpgradeDefinition.cs` — replace `AddColumns`/`AddRows` with `AddCapacity`.
- Modify `Assets/PitTycoon/Unity/UpgradeSystem.cs` — apply `AddCapacity` via `crowd.RaiseCapacity`.
- Modify `Assets/PitTycoon/Unity/GameBootstrap.cs` — cross-wire `IHypeMeter`/`ICrowdMeter`.

**Editor + assets:**
- Modify `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs` — `ConfigureUpgrade` takes `addCapacity`.
- Modify `Assets/Settings/Upgrade_Grounds.asset` — `AddCapacity: 30`, `CeilingDelta: 0`.

**Docs:**
- Modify `SETUP.md` — crowd dynamics bring-up + tuning subsection.

---

## Task 1: Domain `CrowdFill` (TDD)

**Files:**
- Create: `Assets/PitTycoon/Domain/CrowdFill.cs`
- Test: `tests/PitTycoon.Domain.Tests/CrowdFillTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/CrowdFillTests.cs`:

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class CrowdFillTests
    {
        [Test]
        public void BeginSet_ResetsActiveToFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);              // push Active up to 100
            f.BeginSet();
            Assert.That(f.Active, Is.EqualTo(20f));
        }

        [Test]
        public void Tick_Zero_StaysAtFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0f);
            Assert.That(f.Active, Is.EqualTo(20f));
        }

        [Test]
        public void Tick_Full_ReachesCapacity()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);
            Assert.That(f.Active, Is.EqualTo(100f));
        }

        [Test]
        public void Tick_Half_IsMidwayBetweenFollowingAndCapacity()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // 20 + (100-20)*0.5
            Assert.That(f.Active, Is.EqualTo(60f));
        }

        [Test]
        public void Tick_Ratchets_DoesNotShrinkOnLowerHype()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.75f);           // Active = 20 + 80*0.75 = 80 (0.75 is exact in binary)
            f.Tick(0.25f);           // target 40 < 80 -> unchanged
            Assert.That(f.Active, Is.EqualTo(80f));
        }

        [Test]
        public void BankSet_PromotesActiveToFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // Active 60
            f.BankSet();
            Assert.That(f.Following, Is.EqualTo(60));
        }

        [Test]
        public void RaiseCapacity_AllowsFurtherGrowth()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);              // Active 100 at old cap
            f.RaiseCapacity(40);     // cap 140
            f.Tick(1f);              // target 20 + 120 = 140
            Assert.That(f.Capacity, Is.EqualTo(140));
            Assert.That(f.Active, Is.EqualTo(140f));
        }

        [Test]
        public void RaiseCapacity_DoesNotChangeActiveOrFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // Active 60
            f.RaiseCapacity(50);
            Assert.That(f.Active, Is.EqualTo(60f));
            Assert.That(f.Following, Is.EqualTo(20));
        }

        [Test]
        public void FillFraction_IsActiveOverCapacity()
        {
            var f = new CrowdFill(100, 25);
            Assert.That(f.FillFraction, Is.EqualTo(0.25f));
        }

        [Test]
        public void Constructor_ClampsInitialFollowingToCapacity()
        {
            var f = new CrowdFill(50, 999);
            Assert.That(f.Following, Is.EqualTo(50));
            Assert.That(f.Active, Is.EqualTo(50f));
        }

        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.That(() => new CrowdFill(0, 0),
                        Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void RaiseCapacity_RejectsNegativeDelta()
        {
            var f = new CrowdFill(100, 20);
            Assert.That(() => f.RaiseCapacity(-1),
                        Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL — `CrowdFill` does not exist (compile error / type not found).

- [ ] **Step 3: Write the implementation**

Create `Assets/PitTycoon/Domain/CrowdFill.cs`:

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// The organic pit model. A persistent Following (arrived fans) ratchets up across
    /// sets; within a set, Active swells from Following toward Capacity as hype climbs and
    /// never ebbs. Grounds raises Capacity. Pure C# — no UnityEngine, fully unit-testable.
    /// CrowdController renders this; HypeSystem reads FillFraction.
    /// </summary>
    public sealed class CrowdFill
    {
        public int Capacity { get; private set; }
        public int Following { get; private set; }
        public float Active { get; private set; }

        public CrowdFill(int capacity, int initialFollowing)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Following = Clamp(initialFollowing, 0, capacity);
            Active = Following;
        }

        /// <summary>Each set starts at the persistent following floor.</summary>
        public void BeginSet() => Active = Following;

        /// <summary>Swell Active toward Capacity by hype fraction; ratchets, never ebbs.</summary>
        public void Tick(float hypeFraction01)
        {
            float h = Clamp01(hypeFraction01);
            float target = Following + (Capacity - Following) * h;
            if (target > Capacity) target = Capacity;
            if (target > Active) Active = target;
        }

        /// <summary>End of set: the arrivals stay — promote peak Active to the following.</summary>
        public void BankSet()
        {
            int banked = (int)Math.Round(Active, MidpointRounding.AwayFromZero);
            Following = Clamp(banked, 0, Capacity);
        }

        /// <summary>Grounds upgrade: raise the capacity the pit fills toward.</summary>
        public void RaiseCapacity(int delta)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            Capacity += delta;
        }

        public int ActiveCount => (int)Math.Round(Active, MidpointRounding.AwayFromZero);
        public float FillFraction => Capacity > 0 ? Active / Capacity : 0f;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — 73 tests total (61 existing + 12 new).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Domain/CrowdFill.cs" "tests/PitTycoon.Domain.Tests/CrowdFillTests.cs"
git commit -m "feat(domain): add CrowdFill — organic pit fill with ratcheting following"
```

---

## Task 2: Domain meter interfaces

**Files:**
- Create: `Assets/PitTycoon/Domain/IHypeMeter.cs`
- Create: `Assets/PitTycoon/Domain/ICrowdMeter.cs`

No unit tests — these are pure declarations with no behavior; their consumers (Tasks 3–4) exercise them.

- [ ] **Step 1: Create `IHypeMeter`**

Create `Assets/PitTycoon/Domain/IHypeMeter.cs`:

```csharp
namespace PitTycoon.Domain
{
    /// <summary>
    /// Read-only hype progress, decoupling crowd fill from the concrete HypeSystem.
    /// </summary>
    public interface IHypeMeter
    {
        /// <summary>Current hype as a 0..1 fraction of the ceiling.</summary>
        float HypeFraction { get; }
    }
}
```

- [ ] **Step 2: Create `ICrowdMeter`**

Create `Assets/PitTycoon/Domain/ICrowdMeter.cs`:

```csharp
namespace PitTycoon.Domain
{
    /// <summary>
    /// Read-only pit fill, decoupling the hype-rate multiplier from the concrete CrowdController.
    /// </summary>
    public interface ICrowdMeter
    {
        /// <summary>How full the pit is, 0..1 (Active / Capacity).</summary>
        float FillFraction { get; }
    }
}
```

- [ ] **Step 3: Verify Domain still builds**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — still 73 tests (interfaces add no tests, break nothing).

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Domain/IHypeMeter.cs" "Assets/PitTycoon/Domain/ICrowdMeter.cs"
git commit -m "feat(domain): add IHypeMeter/ICrowdMeter to decouple hype<->crowd coupling"
```

---

## Task 3: `HypeSystem` implements `IHypeMeter` and multiplies rate by fill

**Files:**
- Modify: `Assets/PitTycoon/Unity/HypeSystem.cs`

> Unity compiles this on next Editor open; there is no command-line compile here. Correctness is by code review now and the Unity checkpoint in Task 9. `dotnet test` does **not** compile Unity assemblies, so it stays green regardless.

- [ ] **Step 1: Add the interface, tuning field, crowd meter, and multiplied tick**

In `Assets/PitTycoon/Unity/HypeSystem.cs`:

Change the class declaration:

```csharp
    public sealed class HypeSystem : MonoBehaviour, IHypeMeter
```

Add a serialized tuning field right after `passiveRatePerSecond`:

```csharp
        [Range(0f, 1f)]
        [Tooltip("Hype-rate multiplier when the pit is empty; 1 = full pit. Lower = stronger warm-up pressure.")]
        [SerializeField] private float minFillMultiplier = 0.4f;
```

Add a private field next to `_rateBonus`:

```csharp
        private ICrowdMeter _crowd;
```

Add the `IHypeMeter` implementation and the crowd-meter setter near the other public members (e.g. after `Average`):

```csharp
        /// <summary>IHypeMeter: current hype as a 0..1 fraction of the ceiling.</summary>
        public float HypeFraction => Ceiling > 0f ? Current / Ceiling : 0f;

        /// <summary>Inject the crowd fill meter (called by GameBootstrap after both exist).</summary>
        public void SetCrowdMeter(ICrowdMeter crowd) => _crowd = crowd;
```

Replace the `Update` method body:

```csharp
        private void Update()
        {
            if (!_live || analyzer == null || _calc == null) return;
            float fill = _crowd?.FillFraction ?? 1f;          // no meter wired -> no penalty
            float mult = Mathf.Lerp(minFillMultiplier, 1f, Mathf.Clamp01(fill));
            _calc.Tick(Time.deltaTime, analyzer.Intensity01, (passiveRatePerSecond + _rateBonus) * mult);
        }
```

(`using PitTycoon.Domain;` is already present, so `IHypeMeter`/`ICrowdMeter` resolve.)

- [ ] **Step 2: Self-check the edit**

Confirm: class implements `IHypeMeter`; `HypeFraction` reads `Current`/`Ceiling`; `Update` uses `_crowd?.FillFraction ?? 1f` and the `Lerp`. No other method changed.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/HypeSystem.cs"
git commit -m "feat(unity): HypeSystem implements IHypeMeter and scales rate by pit fill"
```

---

## Task 4: `CrowdController` — pooled fill render, set lifecycle, `RaiseCapacity`

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs` (full rewrite of the class body)

> Stage is at z = +9, so the **front row (nearest the stage) is the high-z end**; the pit fills front→back and capacity growth adds rows toward −z (deeper audience). `columns = 12` + `startingCapacity = 84` reproduces today's 7-row footprint (z ∈ [−3.6, +3.6]).

- [ ] **Step 1: Replace the file contents**

Replace all of `Assets/PitTycoon/Unity/CrowdController.cs` with:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Organic pit: a pre-built pool of members that scale in front-to-back (from the
    /// stage outward) as the CrowdFill's Active count rises with hype. Owns one CrowdFill;
    /// the persistent Following ratchets up across sets (banked at set end). Reacts only
    /// through IAudioAnalyzer (never touches AudioSource) and reads hype via IHypeMeter.
    /// Exposes FillFraction (ICrowdMeter) so HypeSystem can scale its rate by how full it is.
    /// </summary>
    public sealed class CrowdController : MonoBehaviour, ICrowdMeter
    {
        [SerializeField] private int columns = 12;
        [SerializeField] private int startingCapacity = 84;
        [SerializeField] private int startingFollowing = 24;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float bounceHeight = 0.6f;
        [SerializeField] private float beatPop = 0.7f;
        [SerializeField] private float popDecayPerSecond = 2.5f;
        [SerializeField] private float bobSpeed = 7f;
        [Tooltip("How fast a member pops in/out as the pit fills (scale units/sec).")]
        [SerializeField] private float scaleInPerSecond = 3f;
        [SerializeField] private Material memberMaterial;
        [SerializeField] private GameObject memberPrefab;
        [SerializeField] private float rotationJitter = 18f;
        [SerializeField] private float scaleJitter = 0.12f;

        private IAudioAnalyzer _analyzer;
        private IHypeMeter _hype;
        private EventBus _bus;
        private CrowdFill _fill;

        private Transform[] _members;
        private float[] _fullScale;   // per-member uniform scale (with jitter)
        private float[] _curScale;    // per-member 0..1 scale-in progress
        private float _pop;
        private bool _live;

        /// <summary>ICrowdMeter: how full the pit is, 0..1.</summary>
        public float FillFraction => _fill?.FillFraction ?? 0f;

        /// <summary>Wire dependencies (called once by GameBootstrap before the first set).</summary>
        public void Initialize(IAudioAnalyzer analyzer, IHypeMeter hype, EventBus bus)
        {
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
            _analyzer = analyzer;
            if (_analyzer != null) _analyzer.BeatDetected += OnBeat;

            _hype = hype;

            if (_bus != null)
            {
                _bus.Unsubscribe<SetStarted>(OnSetStarted);
                _bus.Unsubscribe<SetEnded>(OnSetEnded);
            }
            _bus = bus;
            if (_bus != null)
            {
                _bus.Subscribe<SetStarted>(OnSetStarted);
                _bus.Subscribe<SetEnded>(OnSetEnded);
            }

            _fill ??= new CrowdFill(startingCapacity, startingFollowing);
        }

        private void OnDestroy()
        {
            if (_analyzer != null) _analyzer.BeatDetected -= OnBeat;
            if (_bus != null)
            {
                _bus.Unsubscribe<SetStarted>(OnSetStarted);
                _bus.Unsubscribe<SetEnded>(OnSetEnded);
            }
        }

        private void OnSetStarted(SetStarted e) { _fill.BeginSet(); _live = true; }
        private void OnSetEnded(SetEnded e) { _live = false; _fill.BankSet(); }

        private void OnBeat(BeatInfo beat)
        {
            _pop = Mathf.Max(_pop, beatPop * Mathf.Clamp01(0.4f + beat.Strength));
        }

        /// <summary>One-shot crowd jolt (an ability fired). Visible on the next Update.</summary>
        public void Pop(float strength)
        {
            _pop = Mathf.Max(_pop, strength);
        }

        /// <summary>Grounds upgrade: raise capacity, then rebuild so the new (empty) room shows.</summary>
        public void RaiseCapacity(int delta)
        {
            if (_fill == null) _fill = new CrowdFill(startingCapacity, startingFollowing);
            _fill.RaiseCapacity(delta);
            Build();
        }

        /// <summary>Build (or rebuild) the member pool: all Capacity members, active ones at full
        /// scale, the rest hidden at scale 0 (no flicker on rebuild).</summary>
        public void Build()
        {
            if (_fill == null) _fill = new CrowdFill(startingCapacity, startingFollowing);

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            int n = _fill.Capacity;
            int active = _fill.ActiveCount;
            _members = new Transform[n];
            _fullScale = new float[n];
            _curScale = new float[n];

            int startRows = Mathf.CeilToInt((float)startingCapacity / columns);
            float frontZ = (startRows - 1) * spacing * 0.5f;   // front row fixed near the stage
            float offsetX = (columns - 1) * spacing * 0.5f;

            for (int i = 0; i < n; i++)
            {
                int row = i / columns;                          // row 0 = front (near stage)
                int col = i % columns;

                GameObject go;
                if (memberPrefab != null)
                {
                    go = Instantiate(memberPrefab);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    if (memberMaterial != null)
                    {
                        var rend = go.GetComponent<Renderer>();
                        if (rend != null) rend.sharedMaterial = memberMaterial;
                    }
                }

                go.name = $"Crowd_{row}_{col}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(
                    col * spacing - offsetX, 0f, frontZ - row * spacing);
                go.transform.localRotation = Quaternion.Euler(
                    0f, Random.Range(-rotationJitter, rotationJitter), 0f);

                float full = 1f + Random.Range(-scaleJitter, scaleJitter);
                float cur = (i < active) ? 1f : 0f;             // active members appear immediately
                _fullScale[i] = full;
                _curScale[i] = cur;
                go.transform.localScale = Vector3.one * (full * cur);
                _members[i] = go.transform;
            }
        }

        private void Update()
        {
            if (_analyzer == null || _members == null || _fill == null) return;

            _pop = Mathf.MoveTowards(_pop, 0f, popDecayPerSecond * Time.deltaTime);

            if (_live && _hype != null)                         // fill only advances during a set
                _fill.Tick(_hype.HypeFraction);

            int active = _fill.ActiveCount;
            float intensity = _analyzer.Intensity01;
            float t = Time.time;

            for (int i = 0; i < _members.Length; i++)
            {
                Transform tr = _members[i];
                if (tr == null) continue;

                float target = (i < active) ? 1f : 0f;
                _curScale[i] = Mathf.MoveTowards(_curScale[i], target, scaleInPerSecond * Time.deltaTime);
                float s = _fullScale[i] * _curScale[i];
                tr.localScale = Vector3.one * s;

                bool visible = _curScale[i] > 0.05f;
                float bob = visible ? Mathf.Abs(Mathf.Sin(t * bobSpeed + i * 0.6f)) * bounceHeight * intensity : 0f;
                Vector3 p = tr.localPosition;
                p.y = bob + (visible ? _pop : 0f);
                tr.localPosition = p;
            }
        }
    }
}
```

Note: the old `Grow(int,int)` and `MemberCount` members are intentionally gone; Task 5 removes their last caller, and the grep in this plan's prep confirmed nothing else references them.

- [ ] **Step 2: Self-check the edit**

Confirm: implements `ICrowdMeter`; `Initialize(IAudioAnalyzer, IHypeMeter, EventBus)`; subscribes `SetStarted`/`SetEnded` and unsubscribes in `OnDestroy`; `Tick` only runs when `_live`; `Build` sets active members to full scale immediately; `RaiseCapacity` rebuilds. `Pop` is preserved (AbilitySystem calls it).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/CrowdController.cs"
git commit -m "feat(unity): CrowdController renders organic fill-to-capacity pit with persistent following"
```

---

## Task 5: `UpgradeDefinition` + `UpgradeSystem` — Grounds raises capacity

**Files:**
- Modify: `Assets/PitTycoon/Unity/UpgradeDefinition.cs`
- Modify: `Assets/PitTycoon/Unity/UpgradeSystem.cs`

These two change together (field rename + its only consumer) so the branch stays coherent.

- [ ] **Step 1: Replace the per-level effect fields in `UpgradeDefinition`**

In `Assets/PitTycoon/Unity/UpgradeDefinition.cs`, replace the block from `[Header("Per-level effects ...")]` through the `RateDelta` line with:

```csharp
        [Header("Per-level effects (set the ones relevant to this kind)")]
        [Tooltip("Crowd capacity (member slots) added per purchase (Grounds).")]
        [Min(0)] public int AddCapacity = 0;
        [Tooltip("Hype ceiling added per purchase (Stage).")]
        public float CeilingDelta = 0f;
        [Tooltip("Passive hype/sec added per purchase (Lighting, PA).")]
        public float RateDelta = 0f;
```

(`AddColumns` and `AddRows` are removed; `CeilingDelta` and `RateDelta` are unchanged in meaning.)

- [ ] **Step 2: Apply capacity in `UpgradeSystem.TryPurchase`**

In `Assets/PitTycoon/Unity/UpgradeSystem.cs`, replace the effects block inside `TryPurchase`:

```csharp
            if (u.AddColumns > 0 || u.AddRows > 0) crowd?.Grow(u.AddColumns, u.AddRows);
            if (u.CeilingDelta > 0f) hype?.RaiseCeiling(u.CeilingDelta);
            if (u.RateDelta > 0f) hype?.RaiseRate(u.RateDelta);
            venue?.Apply(u.Kind, level);
```

with:

```csharp
            if (u.AddCapacity > 0) crowd?.RaiseCapacity(u.AddCapacity);
            if (u.CeilingDelta > 0f) hype?.RaiseCeiling(u.CeilingDelta);
            if (u.RateDelta > 0f) hype?.RaiseRate(u.RateDelta);
            venue?.Apply(u.Kind, level);
```

- [ ] **Step 3: Self-check the edit**

Confirm: `UpgradeDefinition` has `AddCapacity` and no `AddColumns`/`AddRows`; `UpgradeSystem` calls `crowd?.RaiseCapacity(u.AddCapacity)` and no longer references `Grow`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/UpgradeDefinition.cs" "Assets/PitTycoon/Unity/UpgradeSystem.cs"
git commit -m "feat(unity): Grounds upgrade raises crowd capacity (AddCapacity) instead of grid rows"
```

---

## Task 6: `GameBootstrap` cross-wires hype <-> crowd

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

- [ ] **Step 1: Update the init sequence**

In `Assets/PitTycoon/Unity/GameBootstrap.cs`, replace the init block (the lines from `crowd.Initialize(analyzer);` through `beatVfx.Initialize(Bus);`) with:

```csharp
            economy.Initialize();
            hype.Initialize(Bus);
            crowd.Initialize(analyzer, hype, Bus);   // hype passed as IHypeMeter
            hype.SetCrowdMeter(crowd);               // crowd passed as ICrowdMeter
            abilities.Initialize(Bus);
            upgrades.Initialize(Bus);
            setController.Initialize(Bus);
            beatVfx.Initialize(Bus);
            // SetController.Start() (after all Awakes) kicks off set 1.
```

(`crowd` and `hype` are the concrete `CrowdController`/`HypeSystem`, which implement `ICrowdMeter`/`IHypeMeter`, so the interface parameters bind implicitly — no extra `using` needed.)

- [ ] **Step 2: Self-check the edit**

Confirm: `hype.Initialize(Bus)` runs before `crowd.Initialize(...)`; `crowd.Initialize(analyzer, hype, Bus)` has three args; `hype.SetCrowdMeter(crowd)` is present. The null-guard `if (analyzer == null || crowd == null || ...)` above is unchanged and still covers all refs.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/GameBootstrap.cs"
git commit -m "feat(unity): cross-wire HypeSystem<->CrowdController via meter interfaces"
```

---

## Task 7: Editor + Grounds asset — `AddCapacity`

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs`
- Modify: `Assets/Settings/Upgrade_Grounds.asset`

- [ ] **Step 1: Update `ConfigureUpgrade` signature and body**

In `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs`, change the method signature:

```csharp
        private static void ConfigureUpgrade(UpgradeDefinition def, string id, string name, UpgradeKind kind,
            int baseCost, float growth, int addCapacity, float ceilingDelta, float rateDelta)
```

and inside the body replace the two lines:

```csharp
            so.FindProperty("AddColumns").intValue = addCols;
            so.FindProperty("AddRows").intValue = addRows;
```

with:

```csharp
            so.FindProperty("AddCapacity").intValue = addCapacity;
```

- [ ] **Step 2: Update the four `ConfigureUpgrade` calls**

Replace the four call sites with (note Grounds gets `addCapacity: 30`, `ceilingDelta: 0f`; the others `addCapacity: 0`):

```csharp
            var groundsDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Grounds.asset");
            ConfigureUpgrade(groundsDef, "grounds", "Grounds Expansion", UpgradeKind.Grounds, 60, 1.6f,
                addCapacity: 30, ceilingDelta: 0f, rateDelta: 0f);
            var stageDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Stage.asset");
            ConfigureUpgrade(stageDef, "stage", "Stage", UpgradeKind.Stage, 90, 1.7f,
                addCapacity: 0, ceilingDelta: 40f, rateDelta: 0f);
            var lightingDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Lighting.asset");
            ConfigureUpgrade(lightingDef, "lighting", "Lighting Rig", UpgradeKind.Lighting, 75, 1.6f,
                addCapacity: 0, ceilingDelta: 0f, rateDelta: 2.5f);
            var paDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_PA.asset");
            ConfigureUpgrade(paDef, "pa", "PA / Speakers", UpgradeKind.PA, 110, 1.7f,
                addCapacity: 0, ceilingDelta: 0f, rateDelta: 3.5f);
```

- [ ] **Step 3: Update the Grounds asset directly**

In `Assets/Settings/Upgrade_Grounds.asset`, replace the trailing fields:

```yaml
  AddColumns: 4
  AddRows: 2
  CeilingDelta: 15
  RateDelta: 0
```

with:

```yaml
  AddCapacity: 30
  CeilingDelta: 0
  RateDelta: 0
```

(The Stage/Lighting/PA assets keep stale `AddColumns: 0`/`AddRows: 0` keys that Unity ignores; their `AddCapacity` defaults to 0, so their behavior is unchanged. Re-running **Build Greybox Scene** rewrites all four assets cleanly — done in Task 9.)

- [ ] **Step 4: Self-check the edit**

Confirm: `ConfigureUpgrade` takes `int addCapacity` (no `addCols`/`addRows`); writes `AddCapacity`; all four calls use the `addCapacity:` named arg; the Grounds asset shows `AddCapacity: 30`, `CeilingDelta: 0`.

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs" "Assets/Settings/Upgrade_Grounds.asset"
git commit -m "feat(editor): generate Grounds upgrade with AddCapacity (30) replacing grid grow"
```

---

## Task 8: `SETUP.md` — crowd dynamics section

**Files:**
- Modify: `SETUP.md`

- [ ] **Step 1: Append the crowd dynamics section**

At the end of `SETUP.md`, append:

```markdown
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
```

- [ ] **Step 2: Commit**

```bash
git add "SETUP.md"
git commit -m "docs: SETUP crowd dynamics — organic pit fill + tuning knobs"
```

---

## Task 9: Unity bring-up checkpoint (needs user)

**Files:** none (Editor + Play-mode verification by the user).

This is a manual checkpoint — the agent cannot operate the Editor.

- [ ] **Step 1: Recompile**

Open the Unity project; watch the Console for the first compile. Expected: **no errors**
(new `CrowdFill`/interfaces, reworked `CrowdController`, `HypeSystem`, `UpgradeSystem`,
`GameBootstrap`, `PitTycoonSetup`).

- [ ] **Step 2: Rebuild the scene**

Run, in order: **Pit Tycoon → Build Greybox Scene** → **Apply Comic Look (M2a)** →
**Build Festival Scene (M2b)**.

- [ ] **Step 3: Play and verify the checklist**

- [ ] Pit starts sparse and packs from the stage outward as hype climbs.
- [ ] Hype visibly gains faster as the pit fills.
- [ ] Crowd stays visible at set end / during intermission (no despawn).
- [ ] Next set starts fuller than the last (following ratcheted up).
- [ ] Buying Grounds adds visible empty room at the back that fills over later sets.
- [ ] Buying Stage / Lighting / PA still scales the venue + climbs ceiling/rate (M3a intact).
- [ ] Abilities still fire and pop the crowd; coins still fly at set end (M1/M2c intact).

- [ ] **Step 4: Report**

Report results to the agent. If anything is off (e.g., the pit fills from the wrong end —
flip `frontZ - row * spacing` to `row * spacing - frontZ` in `CrowdController.Build`), the
agent adjusts and you re-verify.

---

## Notes for the implementer

- **Commit messages:** imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- **`dotnet test` scope:** only Task 1 (and Task 2's no-op check) move the test count — target is **73 green**. Unity-side tasks are not compiled by `dotnet test`; they're verified at Task 9.
- **One-frame coupling lag is intentional:** `HypeSystem` reads last frame's `FillFraction`; `CrowdController` reads last frame's `HypeFraction`. Both are clamped/ratcheted, so there is no instability.
- **Active is globally monotonic:** `Following` only ratchets up and each set starts `Active = Following`, so members only ever scale *in*; `Build` snapshots active members at full scale to avoid a rebuild flicker.
```
