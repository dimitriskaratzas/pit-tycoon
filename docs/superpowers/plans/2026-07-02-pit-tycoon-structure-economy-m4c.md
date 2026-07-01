# Structure Roster + Economy (M4c) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flesh out the festival ground to a full ~11-structure roster and add the three new economic effect types — passive cash income, cash multiplier, and targeted per-ability spike bonus — with the numeric logic in the pure Domain layer behind unit tests.

**Architecture:** `EconomyCalculator` gains `PassiveIncome`/`CashMultiplier` folded into `BankSet`; `Ability` gains a mutable `SpikeBonus` applied in `Fire` — both pure C#, TDD. The Unity side is thin: two `EconomySystem` passthroughs, one `AbilitySystem.AddSpikeBonus(id, pct)` lookup, three new `BuildEffectKind` values + one `targetAbilityId` field on `BuildSpot`, and three new cases in `BuildSystem.TryBuild`'s switch. `StructureGreyboxPrefabs` gains 8 primitive builders and `FestivalGroundSetup` authors the full roster.

**Tech Stack:** Unity 6 LTS (URP), C#; `PitTycoon.Domain` (netstandard2.1, NUnit-tested); editor scripts under `Assets/PitTycoon/Unity/Editor/`.

## Global Constraints

- **Domain purity:** no UnityEngine in `Assets/PitTycoon/Domain/`. All new numeric logic (passive income, multiplier, spike bonus) lives there, unit-tested.
- **Only automated gate:** `dotnet test PitTycoon.Domain.slnx` — **85 passing** today; **92** after Task 1; **96** after Task 2; 0 failed throughout. Existing tests must not be modified — the defaults (multiplier 1, passive 0, spike bonus 1) must reproduce today's behavior exactly. Unity C# compiles only in-Editor (manual checkpoint, Task 7) — transcribe carefully.
- **Payout formula (spec-locked):** `earned = round((peakHype·peakWeight + avgHype·avgWeight) · CashMultiplier) + PassiveIncome`, clamped ≥ 0. Multipliers and spike bonuses accumulate **additively** (two +0.15 → ×1.30).
- **Effect model:** one effect per spot; `BuildSpot` gains exactly one new field `targetAbilityId` (used only by `AbilitySpike`). `BankSet`'s signature is unchanged. No `ShopView` change.
- **Ability ids (verbatim, lowercase, from `Assets/Settings/Ability_*.asset`):** `woofer`, `whirlpool`, `lightburst`. Unknown ids in `AddSpikeBonus` warn + no-op.
- **`TryBuild` ordering preserved:** `TrySpend` → apply effect → `spots.Build(id)` → publish `StructureBuilt`.
- **Serialized field name EXACTLY `abilities` on `BuildSystem`** (Task 5 wires it by string).
- **Commits:** imperative present tense, **no `Co-Authored-By` trailer**. New-file `.meta`s are generated + committed at the Task 7 checkpoint.

---

### Task 1: `EconomyCalculator` — passive income + cash multiplier (Domain, TDD)

**Files:**
- Modify: `Assets/PitTycoon/Domain/EconomyCalculator.cs`
- Test: `tests/PitTycoon.Domain.Tests/EconomyCalculatorTests.cs` (append tests; do not modify existing ones)

**Interfaces:**
- Consumes: nothing.
- Produces: `public int PassiveIncome { get; }`, `public float CashMultiplier { get; }`, `public void AddPassiveIncome(int delta)`, `public void AddCashMultiplier(float pct)` (both throw `ArgumentOutOfRangeException` on negative input). `BankSet` signature unchanged. Consumed by Task 3's `EconomySystem` passthroughs.

- [ ] **Step 1: Append the failing tests**

Inside the existing `EconomyCalculatorTests` class (before its closing brace), append:

```csharp
        // ---- M4c: passive income + cash multiplier ----

        [Test]
        public void AddPassiveIncome_AddsFlatAmountToBankedCash()
        {
            var e = new EconomyCalculator(0);
            e.AddPassiveIncome(25);
            int earned = e.BankSet(100f, 0f, 1f, 0f);   // round(100*1) + 25
            Assert.That(earned, Is.EqualTo(125));
            Assert.That(e.Cash, Is.EqualTo(125));
            Assert.That(e.PassiveIncome, Is.EqualTo(25));
        }

        [Test]
        public void AddCashMultiplier_ScalesHypeEarnings()
        {
            var e = new EconomyCalculator(0);
            e.AddCashMultiplier(0.5f);                   // multiplier 1.5
            int earned = e.BankSet(100f, 0f, 1f, 0f);    // round(100*1.5)
            Assert.That(earned, Is.EqualTo(150));
        }

        [Test]
        public void PassiveAndMultiplier_Stack_MultiplierDoesNotScalePassive()
        {
            var e = new EconomyCalculator(0);
            e.AddPassiveIncome(10);
            e.AddCashMultiplier(0.5f);
            int earned = e.BankSet(100f, 0f, 1f, 0f);    // round(100*1.5) + 10, NOT round(110*1.5)
            Assert.That(earned, Is.EqualTo(160));
        }

        [Test]
        public void CashMultiplier_AccumulatesAdditively()
        {
            var e = new EconomyCalculator(0);
            e.AddCashMultiplier(0.15f);
            e.AddCashMultiplier(0.15f);
            Assert.That(e.CashMultiplier, Is.EqualTo(1.3f).Within(1e-4f));
            int earned = e.BankSet(100f, 0f, 1f, 0f);
            Assert.That(earned, Is.EqualTo(130));
        }

        [Test]
        public void Defaults_ReproduceOldFormula()
        {
            var e = new EconomyCalculator(0);            // no passive, multiplier 1
            int earned = e.BankSet(100f, 50f, 0.5f, 0.5f);
            Assert.That(earned, Is.EqualTo(75));         // identical to the pre-M4c behavior
        }

        [Test]
        public void AddPassiveIncome_Negative_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.AddPassiveIncome(-1));
        }

        [Test]
        public void AddCashMultiplier_Negative_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.AddCashMultiplier(-0.1f));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL — compile error (`PassiveIncome`, `AddPassiveIncome`, `AddCashMultiplier` do not exist).

- [ ] **Step 3: Implement**

In `Assets/PitTycoon/Domain/EconomyCalculator.cs`, add the two properties + methods and fold them into `BankSet`. Final class body (constructor, `CanAfford`, `TrySpend` unchanged):

```csharp
        public int Cash { get; private set; }

        /// <summary>Flat cash added on top of every set-end bank (structures: Food Court, Bar).</summary>
        public int PassiveIncome { get; private set; }

        /// <summary>Multiplier applied to the hype portion of a bank (structures: VIP Lounge,
        /// Sponsor Banner). Starts at 1; bonuses accumulate additively.</summary>
        public float CashMultiplier { get; private set; } = 1f;

        public EconomyCalculator(int startingCash = 0)
        {
            if (startingCash < 0) throw new ArgumentOutOfRangeException(nameof(startingCash));
            Cash = startingCash;
        }

        public int BankSet(float peakHype, float avgHype, float peakWeight, float avgWeight)
        {
            if (peakHype < 0f) throw new ArgumentOutOfRangeException(nameof(peakHype));
            if (avgHype < 0f) throw new ArgumentOutOfRangeException(nameof(avgHype));

            double raw = (peakHype * peakWeight + avgHype * avgWeight) * CashMultiplier;
            int earned = (int)Math.Round(raw, MidpointRounding.AwayFromZero) + PassiveIncome;
            if (earned < 0) earned = 0;
            Cash += earned;
            return earned;
        }

        public void AddPassiveIncome(int delta)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            PassiveIncome += delta;
        }

        public void AddCashMultiplier(float pct)
        {
            if (pct < 0f) throw new ArgumentOutOfRangeException(nameof(pct));
            CashMultiplier += pct;
        }

        public bool CanAfford(int cost) => cost >= 0 && Cash >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Cash -= cost;
            return true;
        }
```

Also update the class doc comment to mention passive income + multiplier.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 92` (85 prior + 7 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/EconomyCalculator.cs tests/PitTycoon.Domain.Tests/EconomyCalculatorTests.cs
git commit -m "feat(domain): EconomyCalculator passive income + cash multiplier with tests"
```

---

### Task 2: `Ability` — spike bonus (Domain, TDD)

**Files:**
- Modify: `Assets/PitTycoon/Domain/Ability.cs`
- Test: `tests/PitTycoon.Domain.Tests/AbilityTests.cs` (append tests; do not modify existing ones)

**Interfaces:**
- Consumes: nothing.
- Produces: `public float SpikeBonus { get; }` (default 1), `public void AddSpikeBonus(float pct)` (throws `ArgumentOutOfRangeException` on negative). `Fire` now returns `hypeAdded = baseSpike · beatMultiplier · SpikeBonus`. Consumed by Task 3's `AbilitySystem.AddSpikeBonus`.

- [ ] **Step 1: Append the failing tests**

Inside the existing `AbilityTests` class (before its closing brace; the `Make` helper is `baseSpike: 4, maxMult: 6`, so an on-beat fire yields `4·6 = 24` hype), append:

```csharp
        // ---- M4c: structure spike bonus ----

        [Test]
        public void SpikeBonus_DefaultsToOne_FireUnchanged()
        {
            var a = Make();
            Assert.That(a.SpikeBonus, Is.EqualTo(1f).Within(1e-4));
            var r = a.Fire(2.0, 2.0);
            Assert.That(r.HypeAdded, Is.EqualTo(24f).Within(1e-3)); // 4 * 6 * 1
        }

        [Test]
        public void AddSpikeBonus_ScalesFireHype()
        {
            var a = Make();
            a.AddSpikeBonus(0.5f);                        // +50%
            var r = a.Fire(2.0, 2.0);
            Assert.That(r.HypeAdded, Is.EqualTo(36f).Within(1e-3)); // 4 * 6 * 1.5
        }

        [Test]
        public void AddSpikeBonus_AccumulatesAdditively()
        {
            var a = Make();
            a.AddSpikeBonus(0.5f);
            a.AddSpikeBonus(0.5f);
            Assert.That(a.SpikeBonus, Is.EqualTo(2f).Within(1e-4));
            var r = a.Fire(2.0, 2.0);
            Assert.That(r.HypeAdded, Is.EqualTo(48f).Within(1e-3)); // 4 * 6 * 2
        }

        [Test]
        public void AddSpikeBonus_Negative_Throws()
        {
            var a = Make();
            Assert.That(() => a.AddSpikeBonus(-0.1f),
                        Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL — compile error (`SpikeBonus`/`AddSpikeBonus` do not exist).

- [ ] **Step 3: Implement**

In `Assets/PitTycoon/Domain/Ability.cs`:

Add after the `CanFire` property:

```csharp
        /// <summary>Structure effect (M4c): multiplier on the hype spike, default 1. Bonuses
        /// accumulate additively (two +0.5 structures => x2).</summary>
        public float SpikeBonus { get; private set; } = 1f;

        public void AddSpikeBonus(float pct)
        {
            if (pct < 0f) throw new ArgumentOutOfRangeException(nameof(pct));
            SpikeBonus += pct;
        }
```

In `Fire`, change the hype line to:

```csharp
            float hypeAdded = _baseSpike * mult * SpikeBonus;
```

Everything else in `Fire` (multiplier, quality, cooldown) is unchanged.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 96` (92 + 4 new).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/Ability.cs tests/PitTycoon.Domain.Tests/AbilityTests.cs
git commit -m "feat(domain): Ability spike bonus with tests"
```

---

### Task 3: Unity effect plumbing — enum + data field + passthroughs + `BuildSystem` cases

**Files:**
- Modify: `Assets/PitTycoon/Unity/VenueLayout.cs`
- Modify: `Assets/PitTycoon/Unity/EconomySystem.cs`
- Modify: `Assets/PitTycoon/Unity/AbilitySystem.cs`
- Modify: `Assets/PitTycoon/Unity/BuildSystem.cs`

**Interfaces:**
- Consumes: `EconomyCalculator.AddPassiveIncome/AddCashMultiplier` (Task 1); `Ability.AddSpikeBonus` and `Ability.Id` (Task 2; `Id` already exists).
- Produces: `BuildEffectKind.PassiveCash/CashMultiplier/AbilitySpike`; `BuildSpot.targetAbilityId` (string); `EconomySystem.AddPassiveIncome(int)/AddCashMultiplier(float)`; `AbilitySystem.AddSpikeBonus(string abilityId, float pct)`; `BuildSystem` serialized field named exactly `abilities` (wired by Task 5).

- [ ] **Step 1: Extend `VenueLayout.cs`**

Replace the enum:

```csharp
    /// <summary>What kind of effect a build spot grants when built.</summary>
    public enum BuildEffectKind
    {
        HypeRate,       // -> HypeSystem.RaiseRate(effectMagnitude)
        Capacity,       // -> CrowdController.RaiseCapacity(round(effectMagnitude))
        PassiveCash,    // -> EconomySystem.AddPassiveIncome(round(effectMagnitude)): flat cash per set-end
        CashMultiplier, // -> EconomySystem.AddCashMultiplier(effectMagnitude): +pct on hype->cash
        AbilitySpike    // -> AbilitySystem.AddSpikeBonus(targetAbilityId, effectMagnitude): +pct spike
    }
```

In `BuildSpot`, after `public float effectMagnitude;`, add:

```csharp
        public string targetAbilityId;    // AbilitySpike only: the Ability.Id to boost (e.g. "woofer"); empty otherwise
```

- [ ] **Step 2: Add `EconomySystem` passthroughs**

After `TrySpend` in `Assets/PitTycoon/Unity/EconomySystem.cs`, add:

```csharp
        /// <summary>Structure effect (M4c): flat cash added on top of every set-end bank.</summary>
        public void AddPassiveIncome(int delta) => _calc.AddPassiveIncome(delta);

        /// <summary>Structure effect (M4c): additively raise the hype->cash multiplier.</summary>
        public void AddCashMultiplier(float pct) => _calc.AddCashMultiplier(pct);
```

- [ ] **Step 3: Add `AbilitySystem.AddSpikeBonus`**

After `TryUnlock` in `Assets/PitTycoon/Unity/AbilitySystem.cs`, add:

```csharp
        /// <summary>Structure effect (M4c): permanently boost one ability's hype spike by pct
        /// (0.5 = +50%). Unknown ids warn and no-op (guards a typo'd VenueLayout).</summary>
        public void AddSpikeBonus(string abilityId, float pct)
        {
            foreach (var a in _abilities)
                if (a.Id == abilityId) { a.AddSpikeBonus(pct); return; }
            Debug.LogWarning($"AbilitySystem: no ability with id '{abilityId}' for spike bonus.", this);
        }
```

- [ ] **Step 4: Extend `BuildSystem`**

In `Assets/PitTycoon/Unity/BuildSystem.cs`, add a serialized ref after `crowd`:

```csharp
        [SerializeField] private AbilitySystem abilities;
```

Extend the `TryBuild` switch to:

```csharp
            switch (spot.effect)
            {
                case BuildEffectKind.HypeRate: hype?.RaiseRate(spot.effectMagnitude); break;
                case BuildEffectKind.Capacity: crowd?.RaiseCapacity(Mathf.RoundToInt(spot.effectMagnitude)); break;
                case BuildEffectKind.PassiveCash: economy.AddPassiveIncome(Mathf.RoundToInt(spot.effectMagnitude)); break;
                case BuildEffectKind.CashMultiplier: economy.AddCashMultiplier(spot.effectMagnitude); break;
                case BuildEffectKind.AbilitySpike: abilities?.AddSpikeBonus(spot.targetAbilityId, spot.effectMagnitude); break;
            }
```

(`economy` is guaranteed non-null by the method's existing early guard; `abilities` is null-tolerant like `hype`/`crowd`.) Update the class doc comment to mention the new effects. Do NOT change the spend → effect → `spots.Build(id)` → publish ordering.

- [ ] **Step 5: Sanity-run the Domain suite (regression guard — these files aren't compiled by it)**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 96` (unchanged; Editor compile verified at Task 7).

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/VenueLayout.cs Assets/PitTycoon/Unity/EconomySystem.cs Assets/PitTycoon/Unity/AbilitySystem.cs Assets/PitTycoon/Unity/BuildSystem.cs
git commit -m "feat(unity): new build effects — passive cash, cash multiplier, targeted ability spike"
```

---

### Task 4: Greybox prefabs for the new roster

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/StructureGreyboxPrefabs.cs`

**Interfaces:**
- Consumes: the file's existing `EnsurePrefab(name, build)` + `Box(parent, mat, name, localPos, size)` helpers (unchanged).
- Produces: `public static GameObject EnsureGrandstand()`, `EnsureFoodCourt()`, `EnsureBar()`, `EnsureVipLounge()`, `EnsureSponsorBanner()`, `EnsureAmpStack()`, `EnsureStrobeRig()`, `EnsureSpeakerWall()` — consumed by Task 5.

- [ ] **Step 1: Add the 8 public entry points**

After `EnsureEntranceGate()`:

```csharp
        public static GameObject EnsureGrandstand() => EnsurePrefab("Grandstand", BuildGrandstand);
        public static GameObject EnsureFoodCourt() => EnsurePrefab("FoodCourt", BuildFoodCourt);
        public static GameObject EnsureBar() => EnsurePrefab("Bar", BuildBar);
        public static GameObject EnsureVipLounge() => EnsurePrefab("VipLounge", BuildVipLounge);
        public static GameObject EnsureSponsorBanner() => EnsurePrefab("SponsorBanner", BuildSponsorBanner);
        public static GameObject EnsureAmpStack() => EnsurePrefab("AmpStack", BuildAmpStack);
        public static GameObject EnsureStrobeRig() => EnsurePrefab("StrobeRig", BuildStrobeRig);
        public static GameObject EnsureSpeakerWall() => EnsurePrefab("SpeakerWall", BuildSpeakerWall);
```

- [ ] **Step 2: Add the 8 builders**

After `BuildEntranceGate`, add (silhouette-readable primitive compositions, same idiom):

```csharp
        // Three stepped seating tiers facing the stage.
        private static GameObject BuildGrandstand(Material mat)
        {
            var root = new GameObject("Grandstand");
            Box(root, mat, "Tier1", new Vector3(0f, 0.5f, 0f),  new Vector3(10f, 1f, 2f));
            Box(root, mat, "Tier2", new Vector3(0f, 1f, -2f),   new Vector3(10f, 2f, 2f));
            Box(root, mat, "Tier3", new Vector3(0f, 1.5f, -4f), new Vector3(10f, 3f, 2f));
            return root;
        }

        // A row of three roofed stalls.
        private static GameObject BuildFoodCourt(Material mat)
        {
            var root = new GameObject("FoodCourt");
            for (int i = 0; i < 3; i++)
            {
                float x = i * 3.5f - 3.5f;
                Box(root, mat, $"Stall{i}", new Vector3(x, 1f, 0f),    new Vector3(2.6f, 2f, 2.6f));
                Box(root, mat, $"Roof{i}",  new Vector3(x, 2.3f, 0f),  new Vector3(3.2f, 0.3f, 3.2f));
            }
            return root;
        }

        // A long counter with a back shelf under a post-held canopy.
        private static GameObject BuildBar(Material mat)
        {
            var root = new GameObject("Bar");
            Box(root, mat, "Counter", new Vector3(0f, 0.6f, 0f),       new Vector3(6f, 1.2f, 1.2f));
            Box(root, mat, "Shelf",   new Vector3(0f, 1.5f, 1.8f),     new Vector3(6f, 3f, 0.5f));
            Box(root, mat, "Canopy",  new Vector3(0f, 3.2f, 0.8f),     new Vector3(7f, 0.3f, 3.5f));
            Box(root, mat, "PostL",   new Vector3(-3.2f, 1.6f, -0.6f), new Vector3(0.3f, 3.2f, 0.3f));
            Box(root, mat, "PostR",   new Vector3(3.2f, 1.6f, -0.6f),  new Vector3(0.3f, 3.2f, 0.3f));
            return root;
        }

        // A raised platform with a front rail and two sofa blocks.
        private static GameObject BuildVipLounge(Material mat)
        {
            var root = new GameObject("VipLounge");
            Box(root, mat, "Platform", new Vector3(0f, 0.75f, 0f),     new Vector3(6f, 1.5f, 5f));
            Box(root, mat, "Rail",     new Vector3(0f, 1.9f, -2.3f),   new Vector3(6f, 0.8f, 0.2f));
            Box(root, mat, "SofaL",    new Vector3(-1.6f, 1.9f, 1.4f), new Vector3(2f, 0.8f, 1f));
            Box(root, mat, "SofaR",    new Vector3(1.6f, 1.9f, 1.4f),  new Vector3(2f, 0.8f, 1f));
            return root;
        }

        // Two poles holding a wide thin banner panel.
        private static GameObject BuildSponsorBanner(Material mat)
        {
            var root = new GameObject("SponsorBanner");
            Box(root, mat, "PoleL",  new Vector3(-4f, 3f, 0f),   new Vector3(0.4f, 6f, 0.4f));
            Box(root, mat, "PoleR",  new Vector3(4f, 3f, 0f),    new Vector3(0.4f, 6f, 0.4f));
            Box(root, mat, "Banner", new Vector3(0f, 4.5f, 0f),  new Vector3(8.4f, 2.4f, 0.15f));
            return root;
        }

        // A 2x3 stack of speaker cabinets.
        private static GameObject BuildAmpStack(Material mat)
        {
            var root = new GameObject("AmpStack");
            for (int col = 0; col < 2; col++)
                for (int row = 0; row < 3; row++)
                    Box(root, mat, $"Amp{col}{row}",
                        new Vector3(col * 1.3f - 0.65f, row * 1.1f + 0.55f, 0f),
                        new Vector3(1.2f, 1f, 1f));
            return root;
        }

        // Two poles + a crossbar hung with four strobe boxes.
        private static GameObject BuildStrobeRig(Material mat)
        {
            var root = new GameObject("StrobeRig");
            Box(root, mat, "PoleL", new Vector3(-2.5f, 2.25f, 0f), new Vector3(0.3f, 4.5f, 0.3f));
            Box(root, mat, "PoleR", new Vector3(2.5f, 2.25f, 0f),  new Vector3(0.3f, 4.5f, 0.3f));
            Box(root, mat, "Bar",   new Vector3(0f, 4.4f, 0f),     new Vector3(5.6f, 0.3f, 0.3f));
            for (int i = 0; i < 4; i++)
                Box(root, mat, $"Strobe{i}", new Vector3(i * 1.3f - 1.95f, 3.9f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            return root;
        }

        // A 4x3 wall of speaker cabinets.
        private static GameObject BuildSpeakerWall(Material mat)
        {
            var root = new GameObject("SpeakerWall");
            for (int col = 0; col < 4; col++)
                for (int row = 0; row < 3; row++)
                    Box(root, mat, $"Spk{col}{row}",
                        new Vector3(col * 1.6f - 2.4f, row * 1.4f + 0.7f, 0f),
                        new Vector3(1.5f, 1.3f, 1f));
            return root;
        }
```

Update the class doc comment to list the full roster. Existing three builders and helpers unchanged.

- [ ] **Step 3: Sanity-run the Domain suite**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 96` (editor code isn't compiled by the runner; in-Editor compile is Task 7).

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/StructureGreyboxPrefabs.cs
git commit -m "feat(editor): greybox prefabs for the M4c structure roster"
```

---

### Task 5: `FestivalGroundSetup` — author the full roster + wire `BuildSystem.abilities`

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs`

**Interfaces:**
- Consumes: `StructureGreyboxPrefabs.Ensure*` (Task 4); `BuildEffectKind.PassiveCash/CashMultiplier/AbilitySpike` + `BuildSpot.targetAbilityId` (Task 3); `BuildSystem`'s serialized `abilities` field (Task 3); the file's existing `SetRef` helper and `MakeSpot`.
- Produces: nothing (terminal editor wiring). Ability ids used verbatim: `woofer`, `lightburst`, `whirlpool`.

- [ ] **Step 1: Resolve the `AbilitySystem` and harden the guard**

Add to the lookups at the top of `Build()` (after `var economy = ...`):

```csharp
            var abilitySys = Object.FindFirstObjectByType<AbilitySystem>();
```

Extend the missing-component guard condition with `|| abilitySys == null` and add `AbilitySystem` to its error message.

- [ ] **Step 2: Ensure the new prefabs**

After the three existing `Ensure*` calls (step 2 of the method), add:

```csharp
            var grandstand = StructureGreyboxPrefabs.EnsureGrandstand();
            var foodCourt = StructureGreyboxPrefabs.EnsureFoodCourt();
            var bar = StructureGreyboxPrefabs.EnsureBar();
            var vipLounge = StructureGreyboxPrefabs.EnsureVipLounge();
            var sponsorBanner = StructureGreyboxPrefabs.EnsureSponsorBanner();
            var ampStack = StructureGreyboxPrefabs.EnsureAmpStack();
            var strobeRig = StructureGreyboxPrefabs.EnsureStrobeRig();
            var speakerWall = StructureGreyboxPrefabs.EnsureSpeakerWall();
```

- [ ] **Step 3: Replace the spot array with the full roster**

Replace the whole `layout.spots = new[] { ... };` block with (M4a's three unchanged; positions spread across the ~120u ground inside the M4b ±58 pan rect; poses/costs tuned in play):

```csharp
            layout.spots = new[]
            {
                // --- M4a originals (unchanged) ---
                MakeSpot("second_stage", "Second Stage", secondStage,
                         pos: new Vector3(-22f, 0f, 0f),  euler: new Vector3(0f, 35f, 0f),
                         camPos: new Vector3(-14f, 6f, -8f), camEuler: new Vector3(18f, 30f, 0f),
                         cost: 120, BuildEffectKind.HypeRate, 3f),
                MakeSpot("camping", "Camping Field", camping,
                         pos: new Vector3(16f, 0f, -22f), euler: new Vector3(0f, -20f, 0f),
                         camPos: new Vector3(10f, 7f, -34f), camEuler: new Vector3(22f, -15f, 0f),
                         cost: 90, BuildEffectKind.Capacity, 40f),
                MakeSpot("gate", "Entrance Gate", gate,
                         pos: new Vector3(24f, 0f, -10f), euler: new Vector3(0f, -50f, 0f),
                         camPos: new Vector3(16f, 5f, -18f), camEuler: new Vector3(12f, -40f, 0f),
                         cost: 60, BuildEffectKind.HypeRate, 1.5f),
                // --- M4c roster ---
                MakeSpot("grandstand", "Grandstand", grandstand,
                         pos: new Vector3(-14f, 0f, -26f), euler: new Vector3(0f, 20f, 0f),
                         camPos: new Vector3(-8f, 7f, -36f), camEuler: new Vector3(20f, -30f, 0f),
                         cost: 150, BuildEffectKind.Capacity, 30f),
                MakeSpot("food_court", "Food Court", foodCourt,
                         pos: new Vector3(-26f, 0f, -16f), euler: new Vector3(0f, 60f, 0f),
                         camPos: new Vector3(-18f, 6f, -24f), camEuler: new Vector3(18f, -45f, 0f),
                         cost: 100, BuildEffectKind.PassiveCash, 25f),
                MakeSpot("bar", "Bar", bar,
                         pos: new Vector3(14f, 0f, 14f), euler: new Vector3(0f, -30f, 0f),
                         camPos: new Vector3(8f, 5f, 6f), camEuler: new Vector3(14f, 35f, 0f),
                         cost: 70, BuildEffectKind.PassiveCash, 15f),
                MakeSpot("vip_lounge", "VIP Lounge", vipLounge,
                         pos: new Vector3(-16f, 0f, 12f), euler: new Vector3(0f, 40f, 0f),
                         camPos: new Vector3(-8f, 6f, 4f), camEuler: new Vector3(16f, -45f, 0f),
                         cost: 200, BuildEffectKind.CashMultiplier, 0.15f),
                MakeSpot("sponsor_banner", "Sponsor Banner", sponsorBanner,
                         pos: new Vector3(0f, 0f, -32f), euler: new Vector3(0f, 0f, 0f),
                         camPos: new Vector3(0f, 6f, -22f), camEuler: new Vector3(14f, 180f, 0f),
                         cost: 80, BuildEffectKind.CashMultiplier, 0.1f),
                MakeSpot("amp_stack", "Amp Stack", ampStack,
                         pos: new Vector3(10f, 0f, 5f), euler: new Vector3(0f, -60f, 0f),
                         camPos: new Vector3(5f, 4f, -2f), camEuler: new Vector3(14f, 35f, 0f),
                         cost: 140, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "woofer"),
                MakeSpot("strobe_rig", "Strobe Rig", strobeRig,
                         pos: new Vector3(-10f, 0f, 5f), euler: new Vector3(0f, 60f, 0f),
                         camPos: new Vector3(-5f, 4f, -2f), camEuler: new Vector3(14f, -35f, 0f),
                         cost: 110, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "lightburst"),
                MakeSpot("speaker_wall", "Speaker Wall", speakerWall,
                         pos: new Vector3(30f, 0f, 2f), euler: new Vector3(0f, -90f, 0f),
                         camPos: new Vector3(22f, 5f, -4f), camEuler: new Vector3(14f, 53f, 0f),
                         cost: 160, BuildEffectKind.AbilitySpike, 0.5f, targetAbilityId: "whirlpool"),
            };
```

- [ ] **Step 4: Extend `MakeSpot` with the target id**

Replace `MakeSpot` with:

```csharp
        private static BuildSpot MakeSpot(string id, string label, GameObject prefab,
                                          Vector3 pos, Vector3 euler, Vector3 camPos, Vector3 camEuler,
                                          int cost, BuildEffectKind effect, float magnitude,
                                          string targetAbilityId = "")
        {
            return new BuildSpot
            {
                id = id, label = label, structurePrefab = prefab,
                position = pos, euler = euler, cameraPosition = camPos, cameraEuler = camEuler,
                cost = cost, effect = effect, effectMagnitude = magnitude,
                targetAbilityId = targetAbilityId, color = Color.white,
            };
        }
```

- [ ] **Step 5: Wire `BuildSystem.abilities`**

In the "Wire BuildSystem" step (after `SetRef(buildSys, "spots", spotsCtl);`), add:

```csharp
            SetRef(buildSys, "abilities", abilitySys);
```

Also update the class doc comment + final `Debug.Log` to mention the full roster.

- [ ] **Step 6: Sanity-run the Domain suite**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 96`.

- [ ] **Step 7: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs
git commit -m "feat(editor): author full festival-ground roster + wire BuildSystem.abilities"
```

---

### Task 6: SETUP.md — M4c section

**Files:**
- Modify: `SETUP.md` (append at the end)

**Interfaces:** none.

- [ ] **Step 1: Append the section**

```markdown
## Structure roster + economy (M4c — full roster, new effects)

Grows the festival ground to ~11 buildable structures and adds three new effect types:
passive cash (flat per set-end), cash multiplier (+pct on hype->cash), and targeted
ability spike (+pct on one ability's hype). All numeric logic is in Domain behind tests.

**Build steps**
1. Pull the branch and let Unity recompile (Domain: EconomyCalculator/Ability; Unity:
   VenueLayout/EconomySystem/AbilitySystem/BuildSystem; Editor: greybox prefabs + setup).
2. Run **Pit Tycoon → Build Festival Ground** (after Build HUD + Build Upgrade Preview, as
   before). It ensures the 8 new greybox prefabs, rewrites OpenAirLayout with the full
   roster, and wires BuildSystem.abilities.
3. Commit the new prefab + .meta files, the updated OpenAirLayout.asset, and the scene.

**Play-test verification**
- Build **Food Court** or **Bar** → the next set-end banks visibly more cash (bigger coin
  burst) even at similar hype; the bonus persists across sets.
- Build **VIP Lounge** / **Sponsor Banner** → the same hype yields more cash; both together
  stack (x1.25).
- Build **Amp Stack** → the Woofer's hype spike is visibly bigger; **Strobe Rig** → Light-burst;
  **Speaker Wall** → Whirlpool. Abilities you didn't boost are unchanged.
- All ~11 spots appear as Build rows, ghost-preview with a camera fly-to, rise on purchase,
  and persist (structures + effects) across sets.
- Regression: M1–M4b intact — upgrades, abilities, crowd, free-look camera, F1 overlay.

**Tuning** (OpenAirLayout asset, live in the Inspector): per-spot cost, effectMagnitude,
targetAbilityId, world pose, camera pose. Passive/multiplier magnitudes are deliberately
raw starting values — the tycoon curve rework is a later milestone.
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP structure roster + economy — build steps, verification, tuning"
```

---

### Task 7: Manual Unity checkpoint (needs the user)

**Files:** none (Editor + play-test).

- [ ] **Step 1: Recompile** — open the project; no compile errors expected.
- [ ] **Step 2: Run** `Pit Tycoon → Build Festival Ground` (HUD + Upgrade Preview builders already ran in M4a/M4b; re-run them first if starting from a fresh clone).
- [ ] **Step 3: Play-test** the SETUP.md M4c checklist (passive cash, multiplier stacking, targeted spikes, all rows/previews/persistence, M1–M4b regression).
- [ ] **Step 4: Tune** spot poses/costs/magnitudes on `OpenAirLayout` to taste.
- [ ] **Step 5: Commit** the generated artifacts:

```bash
git add -A
git commit -m "feat(checkpoint): M4c structure roster wired in-scene + prefabs + .meta files"
```

---

## Self-Review

**Spec coverage:** Domain economy (passive/multiplier) → Task 1; Ability spike → Task 2; enum + `targetAbilityId` + passthroughs + `AddSpikeBonus` + `BuildSystem` cases/ref → Task 3; 8 greybox prefabs → Task 4; full roster + `abilities` wiring + verbatim ability ids → Task 5; SETUP.md → Task 6; visibility + verification → Tasks 6–7. Payout formula, additive stacking, unchanged `BankSet` signature, no-ShopView-change, and raw-tuning notes all match the spec. No gaps.

**Placeholder scan:** none — every code step has complete code; every run step has command + expected counts (85→92→96).

**Type consistency:** `AddPassiveIncome(int)`/`AddCashMultiplier(float)` identical across Tasks 1/3; `AddSpikeBonus(float)` on `Ability` (Task 2) vs `AddSpikeBonus(string, float)` on `AbilitySystem` (Task 3) — distinct by design, consumed correctly in `BuildSystem`; `targetAbilityId` field name matches between Task 3 (`BuildSpot`) and Task 5 (`MakeSpot`); serialized field `abilities` (Task 3) matches `SetRef(buildSys, "abilities", ...)` (Task 5); ability ids `woofer`/`lightburst`/`whirlpool` match the committed `Ability_*.asset` values verbatim.
