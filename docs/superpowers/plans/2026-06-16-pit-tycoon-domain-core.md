# Pit Tycoon — Domain Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and unit-test the entire pure-C# Domain layer of Pit Tycoon (beat-window math, hype accumulation, economy, event bus, audio-analyzer contract) with zero Unity dependency, runnable via `dotnet test`.

**Architecture:** Domain code lives in `Assets/PitTycoon/Domain/` (so Unity adopts it later via an asmdef in Plan 2) but has no `UnityEngine` reference. A standalone .NET test project under `tests/` links those same source files and runs them with NUnit + `dotnet test` — fast, Editor-free TDD that matches a backend workflow. Unity (Plan 2) compiles the identical sources through its own asmdef; the two build systems never conflict because the test project lives outside `Assets/`.

**Tech Stack:** C# 9 (Unity 6 LTS language level), .NET 8 SDK (test host only), NUnit 4 (constraint-model assertions), `dotnet test`.

**Prerequisite:** .NET 8 SDK installed (`dotnet --version` ≥ 8.0). As an ASP.NET Core dev you almost certainly have this.

**Spec:** `docs/superpowers/specs/2026-06-16-pit-tycoon-m1-design.md` (sections 3.3, 3.5 partial, 6).

---

## File Structure

```
Assets/PitTycoon/Domain/            (plain .cs now; Unity asmdef added in Plan 2)
  BeatInfo.cs                       — readonly struct: beat timestamp (DSP clock) + strength
  BeatWindow.cs                     — static on-beat multiplier function
  HypeCalculator.cs                 — hype accumulation, ceiling, peak/avg tracking
  EconomyCalculator.cs              — cash banking + spend validation
  EventBus.cs                       — typed in-process pub/sub
  Events.cs                         — event payload structs (BeatDetected, AbilityFired, SetEnded, UpgradePurchased)
  IAudioAnalyzer.cs                 — analyzer contract consumed by Unity layer
tests/PitTycoon.Domain.Tests/
  PitTycoon.Domain.Tests.csproj     — links Domain sources, NUnit
  HarnessSmokeTests.cs
  BeatWindowTests.cs
  HypeCalculatorTests.cs
  EconomyCalculatorTests.cs
  EventBusTests.cs
PitTycoon.Domain.sln                (repo root; references the test project)
```

Each file has one responsibility. `Events.cs` groups the four small payload structs because they change together and are trivial. Everything else is one type per file.

---

### Task 1: Standalone test harness

**Files:**
- Create: `tests/PitTycoon.Domain.Tests/PitTycoon.Domain.Tests.csproj`
- Create: `tests/PitTycoon.Domain.Tests/HarnessSmokeTests.cs`
- Create: `PitTycoon.Domain.sln`
- Modify: `.gitignore` (un-ignore the hand-written project/solution that Unity's rules would otherwise hide)

- [ ] **Step 1: Verify the .NET SDK is present**

Run: `dotnet --version`
Expected: a version string `8.x.x` (or higher). If missing, install the .NET 8 SDK before continuing.

- [ ] **Step 2: Create the test project file**

Create `tests/PitTycoon.Domain.Tests/PitTycoon.Domain.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <!-- Compile the pure-C# Domain sources directly. Unity compiles the same files via its asmdef. -->
  <ItemGroup>
    <Compile Include="..\..\Assets\PitTycoon\Domain\**\*.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the smoke test**

Create `tests/PitTycoon.Domain.Tests/HarnessSmokeTests.cs`:

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class HarnessSmokeTests
    {
        [Test]
        public void Harness_Compiles_And_Runs()
        {
            Assert.That(true, Is.True);
        }
    }
}
```

- [ ] **Step 4: Create the solution and add the project**

Run from the repo root `C:\Users\afabu\Desktop\Personal Projects\FestivalGame`:

```bash
dotnet new sln -n PitTycoon.Domain
dotnet sln PitTycoon.Domain.sln add tests/PitTycoon.Domain.Tests/PitTycoon.Domain.Tests.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 5: Run the harness to verify it builds and the smoke test passes**

Run from the repo root:

```bash
dotnet test PitTycoon.Domain.sln
```

Expected: `Passed!  - Failed: 0, Passed: 1, Skipped: 0`

- [ ] **Step 6: Un-ignore the hand-written project files**

The Unity `.gitignore` ignores `*.csproj` and `*.sln` (those are Unity-generated at the root). Our test project is hand-written and must be tracked. Add these lines to the **end** of `.gitignore`:

```
# Hand-written standalone Domain test project (outside Assets, not Unity-generated)
!/PitTycoon.Domain.sln
!tests/**/*.csproj
```

- [ ] **Step 7: Commit**

```bash
git add .gitignore PitTycoon.Domain.sln tests/PitTycoon.Domain.Tests/PitTycoon.Domain.Tests.csproj tests/PitTycoon.Domain.Tests/HarnessSmokeTests.cs
git commit -m "$(printf 'test: add standalone .NET harness for Domain layer\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>')"
```

---

### Task 2: BeatInfo + BeatWindow (on-beat multiplier)

**Files:**
- Create: `Assets/PitTycoon/Domain/BeatInfo.cs`
- Create: `Assets/PitTycoon/Domain/BeatWindow.cs`
- Test: `tests/PitTycoon.Domain.Tests/BeatWindowTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/BeatWindowTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class BeatWindowTests
    {
        [Test]
        public void PerfectlyOnBeat_ReturnsMaxMultiplier()
        {
            float m = BeatWindow.Multiplier(beatDspTime: 10.0, fireDspTime: 10.0, toleranceSeconds: 0.1, maxMultiplier: 3f);
            Assert.That(m, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void AtToleranceEdge_ReturnsOne()
        {
            float m = BeatWindow.Multiplier(10.0, 10.1, 0.1, 3f);
            Assert.That(m, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void BeyondTolerance_ReturnsOne()
        {
            float m = BeatWindow.Multiplier(10.0, 10.5, 0.1, 3f);
            Assert.That(m, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HalfwayInsideWindow_ReturnsLinearMidpoint()
        {
            // delta = 0.05, tolerance 0.1 -> t = 0.5 -> 1 + 0.5*(3-1) = 2.0
            float m = BeatWindow.Multiplier(10.0, 10.05, 0.1, 3f);
            Assert.That(m, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void FiringBeforeBeat_IsSymmetric()
        {
            float m = BeatWindow.Multiplier(10.0, 9.95, 0.1, 3f);
            Assert.That(m, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void NonPositiveTolerance_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BeatWindow.Multiplier(0, 0, 0, 3f));
        }

        [Test]
        public void MaxMultiplierBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BeatWindow.Multiplier(0, 0, 0.1, 0.5f));
        }

        [Test]
        public void BeatInfo_StoresValues()
        {
            var b = new BeatInfo(12.5, 0.8f);
            Assert.That(b.DspTime, Is.EqualTo(12.5));
            Assert.That(b.Strength, Is.EqualTo(0.8f));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: build error / FAIL — `BeatWindow` and `BeatInfo` do not exist.

- [ ] **Step 3: Write the implementations**

Create `Assets/PitTycoon/Domain/BeatInfo.cs`:

```csharp
namespace PitTycoon.Domain
{
    /// <summary>
    /// A detected beat. <see cref="DspTime"/> is on the audio DSP clock
    /// (Unity: AudioSettings.dspTime), NOT the frame clock, so on-beat
    /// timing stays accurate independent of frame rate.
    /// </summary>
    public readonly struct BeatInfo
    {
        public double DspTime { get; }
        public float Strength { get; }

        public BeatInfo(double dspTime, float strength)
        {
            DspTime = dspTime;
            Strength = strength;
        }
    }
}
```

Create `Assets/PitTycoon/Domain/BeatWindow.cs`:

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Pure on-beat scoring. Returns a multiplier in [1, maxMultiplier]:
    /// maxMultiplier when the fire lands exactly on the beat, falling off
    /// linearly to 1.0 at the edge of the tolerance window, and 1.0 outside it.
    /// </summary>
    public static class BeatWindow
    {
        public static float Multiplier(double beatDspTime, double fireDspTime, double toleranceSeconds, float maxMultiplier)
        {
            if (toleranceSeconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(toleranceSeconds));
            if (maxMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(maxMultiplier));

            double delta = Math.Abs(fireDspTime - beatDspTime);
            if (delta >= toleranceSeconds) return 1f;

            double closeness = 1.0 - (delta / toleranceSeconds); // 1 at perfect, 0 at edge
            return 1f + (float)(closeness * (maxMultiplier - 1f));
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: `Failed: 0, Passed: 9` (8 new + 1 smoke).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/BeatInfo.cs Assets/PitTycoon/Domain/BeatWindow.cs tests/PitTycoon.Domain.Tests/BeatWindowTests.cs
git commit -m "$(printf 'feat: add BeatInfo and BeatWindow on-beat multiplier\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>')"
```

---

### Task 3: HypeCalculator

**Files:**
- Create: `Assets/PitTycoon/Domain/HypeCalculator.cs`
- Test: `tests/PitTycoon.Domain.Tests/HypeCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/HypeCalculatorTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class HypeCalculatorTests
    {
        [Test]
        public void NonPositiveCeiling_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypeCalculator(0f));
        }

        [Test]
        public void Tick_FillsProportionalToIntensityRateAndTime()
        {
            var h = new HypeCalculator(ceiling: 100f);
            h.Tick(deltaSeconds: 1f, intensity01: 0.5f, passiveRatePerSecond: 10f);
            Assert.That(h.Current, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void Tick_ClampsIntensityToOne()
        {
            var h = new HypeCalculator(100f);
            h.Tick(1f, 2f, 10f); // intensity treated as 1.0
            Assert.That(h.Current, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Current_ClampsAtCeiling()
        {
            var h = new HypeCalculator(20f);
            h.Tick(10f, 1f, 10f); // would be 100, clamps to 20
            Assert.That(h.Current, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void AddSpike_AddsAndTracksPeak()
        {
            var h = new HypeCalculator(100f);
            h.AddSpike(30f);
            Assert.That(h.Current, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(h.Peak, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void AddSpike_ClampsAtCeiling()
        {
            var h = new HypeCalculator(20f);
            h.AddSpike(50f);
            Assert.That(h.Current, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void NegativeSpike_Throws()
        {
            var h = new HypeCalculator(100f);
            Assert.Throws<ArgumentOutOfRangeException>(() => h.AddSpike(-1f));
        }

        [Test]
        public void Average_IsTimeWeightedAcrossTicks()
        {
            var h = new HypeCalculator(100f);
            h.Tick(1f, 1f, 10f); // current 10, accum 10
            h.Tick(1f, 1f, 10f); // current 20, accum 30, time 2 -> avg 15
            Assert.That(h.Average, Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void RaiseCeiling_AllowsMoreFill()
        {
            var h = new HypeCalculator(20f);
            h.AddSpike(50f); // clamps to 20
            h.RaiseCeiling(30f); // ceiling now 50
            h.AddSpike(50f); // clamps to 50
            Assert.That(h.Ceiling, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(h.Current, Is.EqualTo(50f).Within(0.0001f));
        }

        [Test]
        public void ResetForNewSet_ZeroesProgressButKeepsCeiling()
        {
            var h = new HypeCalculator(20f);
            h.RaiseCeiling(30f);
            h.Tick(1f, 1f, 10f);
            h.AddSpike(5f);
            h.ResetForNewSet();
            Assert.That(h.Current, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Peak, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Average, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Ceiling, Is.EqualTo(50f).Within(0.0001f));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: build error — `HypeCalculator` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/PitTycoon/Domain/HypeCalculator.cs`:

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Live hype for one set. Fills passively from intensity * rate over time,
    /// spikes from abilities, and clamps to a ceiling that upgrades can raise.
    /// Tracks peak and a time-weighted average for end-of-set cash banking.
    /// Pure C# — no UnityEngine, fully unit-testable.
    /// </summary>
    public sealed class HypeCalculator
    {
        private double _weightedSum; // sum of Current * dt
        private double _elapsed;     // sum of dt

        public float Ceiling { get; private set; }
        public float Current { get; private set; }
        public float Peak { get; private set; }
        public float Average => _elapsed > 0.0 ? (float)(_weightedSum / _elapsed) : 0f;

        public HypeCalculator(float ceiling)
        {
            if (ceiling <= 0f) throw new ArgumentOutOfRangeException(nameof(ceiling));
            Ceiling = ceiling;
        }

        public void Tick(float deltaSeconds, float intensity01, float passiveRatePerSecond)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            float intensity = Clamp01(intensity01);
            Add(passiveRatePerSecond * intensity * deltaSeconds);
            _weightedSum += Current * deltaSeconds;
            _elapsed += deltaSeconds;
        }

        public void AddSpike(float amount)
        {
            if (amount < 0f) throw new ArgumentOutOfRangeException(nameof(amount));
            Add(amount);
        }

        public void RaiseCeiling(float delta)
        {
            if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
            Ceiling += delta;
        }

        public void ResetForNewSet()
        {
            Current = 0f;
            Peak = 0f;
            _weightedSum = 0.0;
            _elapsed = 0.0;
        }

        private void Add(float amount)
        {
            Current = Math.Min(Ceiling, Current + amount);
            if (Current > Peak) Peak = Current;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: `Failed: 0, Passed: 19` (10 new + 9 prior).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/HypeCalculator.cs tests/PitTycoon.Domain.Tests/HypeCalculatorTests.cs
git commit -m "$(printf 'feat: add HypeCalculator with peak/avg tracking and ceiling\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>')"
```

---

### Task 4: EconomyCalculator

**Files:**
- Create: `Assets/PitTycoon/Domain/EconomyCalculator.cs`
- Test: `tests/PitTycoon.Domain.Tests/EconomyCalculatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/EconomyCalculatorTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class EconomyCalculatorTests
    {
        [Test]
        public void StartsWithGivenCash()
        {
            var e = new EconomyCalculator(startingCash: 50);
            Assert.That(e.Cash, Is.EqualTo(50));
        }

        [Test]
        public void NegativeStartingCash_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyCalculator(-1));
        }

        [Test]
        public void BankSet_AddsWeightedEarningsAndReturnsThem()
        {
            var e = new EconomyCalculator(0);
            int earned = e.BankSet(peakHype: 100f, avgHype: 50f, peakWeight: 0.5f, avgWeight: 0.5f);
            Assert.That(earned, Is.EqualTo(75)); // 100*0.5 + 50*0.5
            Assert.That(e.Cash, Is.EqualTo(75));
        }

        [Test]
        public void BankSet_RoundsAwayFromZero()
        {
            var e = new EconomyCalculator(0);
            int earned = e.BankSet(1f, 0f, 0.5f, 0f); // 0.5 -> 1
            Assert.That(earned, Is.EqualTo(1));
        }

        [Test]
        public void BankSet_NegativeHype_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.BankSet(-1f, 0f, 1f, 1f));
        }

        [Test]
        public void CanAfford_TrueWhenEnough_FalseWhenNot()
        {
            var e = new EconomyCalculator(100);
            Assert.That(e.CanAfford(100), Is.True);
            Assert.That(e.CanAfford(101), Is.False);
        }

        [Test]
        public void CanAfford_NegativeCost_IsFalse()
        {
            var e = new EconomyCalculator(100);
            Assert.That(e.CanAfford(-5), Is.False);
        }

        [Test]
        public void TrySpend_SuccessReducesCash()
        {
            var e = new EconomyCalculator(100);
            bool ok = e.TrySpend(40);
            Assert.That(ok, Is.True);
            Assert.That(e.Cash, Is.EqualTo(60));
        }

        [Test]
        public void TrySpend_InsufficientLeavesCashUnchanged()
        {
            var e = new EconomyCalculator(30);
            bool ok = e.TrySpend(40);
            Assert.That(ok, Is.False);
            Assert.That(e.Cash, Is.EqualTo(30));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: build error — `EconomyCalculator` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/PitTycoon/Domain/EconomyCalculator.cs`:

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Persistent currency across sets. Banks cash from a finished set's hype
    /// (weighted peak + average) and validates purchases. Pure C#.
    /// </summary>
    public sealed class EconomyCalculator
    {
        public int Cash { get; private set; }

        public EconomyCalculator(int startingCash = 0)
        {
            if (startingCash < 0) throw new ArgumentOutOfRangeException(nameof(startingCash));
            Cash = startingCash;
        }

        public int BankSet(float peakHype, float avgHype, float peakWeight, float avgWeight)
        {
            if (peakHype < 0f) throw new ArgumentOutOfRangeException(nameof(peakHype));
            if (avgHype < 0f) throw new ArgumentOutOfRangeException(nameof(avgHype));

            double raw = peakHype * peakWeight + avgHype * avgWeight;
            int earned = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            if (earned < 0) earned = 0;
            Cash += earned;
            return earned;
        }

        public bool CanAfford(int cost) => cost >= 0 && Cash >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Cash -= cost;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: `Failed: 0, Passed: 28` (9 new + 19 prior).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/EconomyCalculator.cs tests/PitTycoon.Domain.Tests/EconomyCalculatorTests.cs
git commit -m "$(printf 'feat: add EconomyCalculator for cash banking and spend validation\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>')"
```

---

### Task 5: EventBus, event payloads, and IAudioAnalyzer contract

**Files:**
- Create: `Assets/PitTycoon/Domain/EventBus.cs`
- Create: `Assets/PitTycoon/Domain/Events.cs`
- Create: `Assets/PitTycoon/Domain/IAudioAnalyzer.cs`
- Test: `tests/PitTycoon.Domain.Tests/EventBusTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/EventBusTests.cs`:

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class EventBusTests
    {
        [Test]
        public void Publish_InvokesSubscriberWithPayload()
        {
            var bus = new EventBus();
            int received = 0;
            bus.Subscribe<UpgradePurchased>(e => received++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void Publish_InvokesAllSubscribers()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            bus.Subscribe<UpgradePurchased>(e => a++);
            bus.Subscribe<UpgradePurchased>(e => b++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            void Handler(UpgradePurchased e) => count++;
            bus.Subscribe<UpgradePurchased>(Handler);
            bus.Unsubscribe<UpgradePurchased>(Handler);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new SetEnded(10f, 5f, 3)));
        }

        [Test]
        public void DifferentEventTypes_AreIsolated()
        {
            var bus = new EventBus();
            int upgrades = 0, setEnds = 0;
            bus.Subscribe<UpgradePurchased>(e => upgrades++);
            bus.Subscribe<SetEnded>(e => setEnds++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(upgrades, Is.EqualTo(1));
            Assert.That(setEnds, Is.EqualTo(0));
        }

        [Test]
        public void SetEnded_CarriesPayload()
        {
            var e = new SetEnded(peakHype: 100f, avgHype: 40f, cashEarned: 70);
            Assert.That(e.PeakHype, Is.EqualTo(100f));
            Assert.That(e.AvgHype, Is.EqualTo(40f));
            Assert.That(e.CashEarned, Is.EqualTo(70));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: build error — `EventBus`, `UpgradePurchased`, `SetEnded` do not exist.

- [ ] **Step 3: Write the implementations**

Create `Assets/PitTycoon/Domain/EventBus.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Minimal typed in-process pub/sub for 1:many domain events.
    /// Injected as an instance (not static) so each test/game gets a clean bus.
    /// Use plain C# events for simple 1:1 wiring; use this for broadcasts.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(T), out Delegate existing);
            _handlers[typeof(T)] = (Action<T>)existing + handler;
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                _handlers[typeof(T)] = (Action<T>)existing - handler;
            }
        }

        public void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                ((Action<T>)existing)?.Invoke(evt);
            }
        }
    }
}
```

Create `Assets/PitTycoon/Domain/Events.cs`:

```csharp
namespace PitTycoon.Domain
{
    /// <summary>Raised when the analyzer detects a beat.</summary>
    public readonly struct BeatDetected
    {
        public BeatInfo Beat { get; }
        public BeatDetected(BeatInfo beat) { Beat = beat; }
    }

    /// <summary>Raised when an ability fires, with its resolved on-beat multiplier.</summary>
    public readonly struct AbilityFired
    {
        public string AbilityId { get; }
        public float Multiplier { get; }
        public float HypeAdded { get; }
        public AbilityFired(string abilityId, float multiplier, float hypeAdded)
        {
            AbilityId = abilityId;
            Multiplier = multiplier;
            HypeAdded = hypeAdded;
        }
    }

    /// <summary>Raised when a set ends and hype banks to cash.</summary>
    public readonly struct SetEnded
    {
        public float PeakHype { get; }
        public float AvgHype { get; }
        public int CashEarned { get; }
        public SetEnded(float peakHype, float avgHype, int cashEarned)
        {
            PeakHype = peakHype;
            AvgHype = avgHype;
            CashEarned = cashEarned;
        }
    }

    /// <summary>Raised when a passive upgrade is bought during intermission.</summary>
    public readonly struct UpgradePurchased
    {
        public string UpgradeId { get; }
        public UpgradePurchased(string upgradeId) { UpgradeId = upgradeId; }
    }
}
```

Create `Assets/PitTycoon/Domain/IAudioAnalyzer.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace PitTycoon.Domain
{
    /// <summary>
    /// The only audio surface the game consumes. The Unity layer's FftAudioAnalyzer
    /// implements this over AudioSource; a future v2 implementation can read a
    /// user-supplied file. Consumers must tolerate noisy beats (dense genres like
    /// metal produce rougher detection). Never touch AudioSource outside the impl.
    /// </summary>
    public interface IAudioAnalyzer
    {
        /// <summary>Smoothed overall loudness/energy, 0..1.</summary>
        float Intensity01 { get; }

        /// <summary>Current per-band energies (low..high), engine-defined length.</summary>
        IReadOnlyList<float> Bands { get; }

        /// <summary>Fires when a beat onset is detected, timestamped on the DSP clock.</summary>
        event Action<BeatInfo> BeatDetected;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.sln`
Expected: `Failed: 0, Passed: 34` (6 new + 28 prior).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/EventBus.cs Assets/PitTycoon/Domain/Events.cs Assets/PitTycoon/Domain/IAudioAnalyzer.cs tests/PitTycoon.Domain.Tests/EventBusTests.cs
git commit -m "$(printf 'feat: add EventBus, domain event payloads, and IAudioAnalyzer contract\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>')"
```

---

## Definition of Done

- `dotnet test PitTycoon.Domain.sln` → `Failed: 0, Passed: 34`.
- `Assets/PitTycoon/Domain/` contains 7 source files, none referencing `UnityEngine`.
- All work committed.
- Ready for Plan 2 (Unity Greybox Integration): the Domain API (`HypeCalculator`, `EconomyCalculator`, `BeatWindow`, `EventBus`, `IAudioAnalyzer`, event payloads) is fixed and tested, so the Unity layer can be wired against a stable contract.
