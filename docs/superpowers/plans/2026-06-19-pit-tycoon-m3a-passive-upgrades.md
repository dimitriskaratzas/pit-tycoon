# Pit Tycoon M3a — Passive Upgrade Roster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the one hardcoded upgrade into the brief's four (Grounds/Stage/Lighting/PA), each buyable repeatedly at an escalating cost and each visibly scaling the M2b venue.

**Architecture:** A pure-C# `UpgradePricing` (Domain, TDD) computes escalating cost. A generalized `UpgradeSystem` holds a list of `UpgradeDefinition`s, tracks each one's level, applies numeric effects (crowd/hype), and delegates the visible step-change to a new `VenueController` that scales the stage/PA/lights.

**Tech Stack:** .NET 10 (Domain tests, NUnit 4 constraint style), Unity 6 / URP 17.4. PC-first.

> **Verification model:** `UpgradePricing` is real TDD via `dotnet test PitTycoon.Domain.slnx`. The Unity layer is verified at the manual checkpoint (Task 10). The Domain suite (56) must stay green and grow. Grounds keeps the current direct `crowd.Grow`; the organic hype-driven crowd model is deferred (spec §9).

---

## File structure

```
Assets/PitTycoon/Domain/
  UpgradePricing.cs     (new) pure cost-at-level formula
tests/PitTycoon.Domain.Tests/
  UpgradePricingTests.cs (new) TDD
Assets/PitTycoon/Unity/
  UpgradeDefinition.cs  (modify) UpgradeKind enum + BaseCost/CostGrowth/deltas
  UpgradeSystem.cs      (modify) list + per-upgrade level + pricing + venue
  HypeSystem.cs         (modify) + RaiseRate + runtime rate bonus
  VenueController.cs     (new) visible scaling of stage/PA/lights
  DebugHud.cs           (modify) intermission lists all four upgrades
  Editor/PitTycoonSetup.cs   (modify) create 4 upgrade assets + wire list
  Editor/FestivalSceneSetup.cs (modify) create VenueController + wire instances + UpgradeSystem.venue
SETUP.md                (modify) + M3a section
```

---

### Task 1: Domain `UpgradePricing` (TDD)

**Files:**
- Create: `tests/PitTycoon.Domain.Tests/UpgradePricingTests.cs`
- Create: `Assets/PitTycoon/Domain/UpgradePricing.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class UpgradePricingTests
    {
        [Test]
        public void Level0_IsBaseCost()
            => Assert.That(UpgradePricing.CostAtLevel(60, 1.6f, 0), Is.EqualTo(60));

        [Test]
        public void Level1_RoundsBaseTimesGrowth()
            => Assert.That(UpgradePricing.CostAtLevel(60, 1.6f, 1), Is.EqualTo(96)); // 60 * 1.6

        [Test]
        public void Level2_CompoundsAndRounds()
            => Assert.That(UpgradePricing.CostAtLevel(60, 1.6f, 2), Is.EqualTo(154)); // 60 * 2.56 = 153.6 -> 154

        [Test]
        public void IncreasesWithLevel()
            => Assert.That(UpgradePricing.CostAtLevel(60, 1.6f, 3),
                           Is.GreaterThan(UpgradePricing.CostAtLevel(60, 1.6f, 2)));

        [Test]
        public void GrowthOne_StaysFlat()
            => Assert.That(UpgradePricing.CostAtLevel(50, 1f, 5), Is.EqualTo(50));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL (compile error — `UpgradePricing` not defined).

- [ ] **Step 3: Implement**

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>Escalating upgrade pricing: cost grows geometrically per purchased level.</summary>
    public static class UpgradePricing
    {
        /// <summary>Cost of the next purchase at the given already-owned level (level 0 = first buy = base).</summary>
        public static int CostAtLevel(int baseCost, float growth, int level)
        {
            if (level <= 0) return baseCost;
            double cost = baseCost * Math.Pow(growth, level);
            return (int)Math.Round(cost, MidpointRounding.AwayFromZero);
        }
    }
}
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS (61 tests — 56 + 5).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Domain/UpgradePricing.cs" "tests/PitTycoon.Domain.Tests/UpgradePricingTests.cs"
git commit -m "feat(domain): UpgradePricing escalating cost (TDD, M3a)"
```

---

### Task 2: Extend UpgradeDefinition

**Files:**
- Modify: `Assets/PitTycoon/Unity/UpgradeDefinition.cs`

- [ ] **Step 1: Replace the file**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>Which venue system a passive upgrade scales.</summary>
    public enum UpgradeKind { Grounds, Stage, Lighting, PA }

    /// <summary>
    /// Data-driven passive upgrade. Bought repeatedly at an escalating cost
    /// (BaseCost * CostGrowth^level); each purchase applies its per-level deltas and
    /// a visible step on the venue (via VenueController, by Kind).
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Pit Tycoon/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id = "grounds";
        public string DisplayName = "Grounds Expansion";
        public UpgradeKind Kind = UpgradeKind.Grounds;
        [Min(0)] public int BaseCost = 60;
        [Min(1f)] public float CostGrowth = 1.6f;

        [Header("Per-level effects (set the ones relevant to this kind)")]
        [Tooltip("Crowd columns added per purchase (Grounds).")]
        public int AddColumns = 0;
        [Tooltip("Crowd rows added per purchase (Grounds).")]
        public int AddRows = 0;
        [Tooltip("Hype ceiling added per purchase (Stage, Grounds).")]
        public float CeilingDelta = 0f;
        [Tooltip("Passive hype/sec added per purchase (Lighting, PA).")]
        public float RateDelta = 0f;
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: a compile error in `UpgradeSystem`/`DebugHud` (they use the old `Cost`/`CapacityUpgrade`) — fixed in Tasks 5–6. The SO itself compiles.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/UpgradeDefinition.cs"
git commit -m "feat(unity): UpgradeDefinition kind + escalating-cost + effect deltas (M3a)"
```

---

### Task 3: HypeSystem.RaiseRate

**Files:**
- Modify: `Assets/PitTycoon/Unity/HypeSystem.cs`

- [ ] **Step 1: Add the runtime rate bonus field**

After the `passiveRatePerSecond` field, add:

```csharp
        private float _rateBonus;
```

- [ ] **Step 2: Add RaiseRate next to RaiseCeiling**

After the `RaiseCeiling` method, add:

```csharp
        /// <summary>Permanently raise the passive hype/sec (lighting/PA upgrades).</summary>
        public void RaiseRate(float delta) => _rateBonus += delta;
```

- [ ] **Step 3: Use the bonus in Tick**

Replace the `Tick` call line with:

```csharp
            _calc.Tick(Time.deltaTime, analyzer.Intensity01, passiveRatePerSecond + _rateBonus);
```

- [ ] **Step 4: Verify compile + Domain green**

Expected: HypeSystem compiles. `dotnet test PitTycoon.Domain.slnx` → PASS (61).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/HypeSystem.cs"
git commit -m "feat(unity): HypeSystem.RaiseRate runtime passive-rate bonus (M3a)"
```

---

### Task 4: VenueController

**Files:**
- Create: `Assets/PitTycoon/Unity/VenueController.cs`

- [ ] **Step 1: Write the component**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Applies the visible step-change of a passive upgrade to the M2b venue geometry:
    /// scales the stage, scales the PA stacks, brightens the accent lights — by level.
    /// Captures the base values lazily on first Apply so repeated levels stay absolute.
    /// </summary>
    public sealed class VenueController : MonoBehaviour
    {
        [SerializeField] private Transform stage;
        [SerializeField] private Transform paLeft;
        [SerializeField] private Transform paRight;
        [SerializeField] private Light[] accentLights;
        [SerializeField] private float stageStep = 0.08f;
        [SerializeField] private float paStep = 0.10f;
        [SerializeField] private float lightStep = 0.6f;

        private Vector3 _stageBase, _paLeftBase, _paRightBase;
        private float[] _lightBase;
        private bool _captured;

        private void Capture()
        {
            if (_captured) return;
            if (stage != null) _stageBase = stage.localScale;
            if (paLeft != null) _paLeftBase = paLeft.localScale;
            if (paRight != null) _paRightBase = paRight.localScale;
            if (accentLights != null)
            {
                _lightBase = new float[accentLights.Length];
                for (int i = 0; i < accentLights.Length; i++)
                    _lightBase[i] = accentLights[i] != null ? accentLights[i].intensity : 0f;
            }
            _captured = true;
        }

        public void Apply(UpgradeKind kind, int level)
        {
            Capture();
            switch (kind)
            {
                case UpgradeKind.Stage: ApplyStage(level); break;
                case UpgradeKind.Lighting: ApplyLighting(level); break;
                case UpgradeKind.PA: ApplyPa(level); break;
            }
        }

        private void ApplyStage(int level)
        {
            if (stage != null) stage.localScale = _stageBase * (1f + stageStep * level);
        }

        private void ApplyPa(int level)
        {
            float f = 1f + paStep * level;
            if (paLeft != null) paLeft.localScale = _paLeftBase * f;
            if (paRight != null) paRight.localScale = _paRightBase * f;
        }

        private void ApplyLighting(int level)
        {
            if (accentLights == null || _lightBase == null) return;
            for (int i = 0; i < accentLights.Length; i++)
                if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: no Console errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/VenueController.cs"
git commit -m "feat(unity): VenueController visible upgrade scaling (M3a)"
```

---

### Task 5: Generalize UpgradeSystem

**Files:**
- Modify: `Assets/PitTycoon/Unity/UpgradeSystem.cs` (replace the file)

- [ ] **Step 1: Replace the file**

```csharp
using System.Collections.Generic;
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns the passive-upgrade roster. Each upgrade is buyable repeatedly at an escalating
    /// cost (UpgradePricing); a purchase spends via EconomySystem, applies the per-level
    /// numeric effects (crowd grow / hype ceiling / hype rate), and asks the VenueController
    /// for the visible step-change, then broadcasts UpgradePurchased.
    /// </summary>
    public sealed class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] private List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        [SerializeField] private EconomySystem economy;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private VenueController venue;

        private readonly Dictionary<UpgradeDefinition, int> _levels = new Dictionary<UpgradeDefinition, int>();
        private EventBus _bus;

        public IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;
        public int LevelOf(UpgradeDefinition u) => (u != null && _levels.TryGetValue(u, out int l)) ? l : 0;
        public int CurrentCost(UpgradeDefinition u) => u == null ? 0 : UpgradePricing.CostAtLevel(u.BaseCost, u.CostGrowth, LevelOf(u));
        public bool CanAfford(UpgradeDefinition u) => u != null && economy != null && economy.CanAfford(CurrentCost(u));

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _levels.Clear();
            foreach (var u in upgrades)
                if (u != null && !_levels.ContainsKey(u)) _levels[u] = 0;
        }

        /// <summary>Attempt to buy the next level; applies effects + visible scaling on success.</summary>
        public bool TryPurchase(UpgradeDefinition u)
        {
            if (u == null || economy == null) return false;
            int cost = CurrentCost(u);
            if (!economy.TrySpend(cost)) return false;

            int level = LevelOf(u) + 1;
            _levels[u] = level;

            if (u.AddColumns > 0 || u.AddRows > 0) crowd?.Grow(u.AddColumns, u.AddRows);
            if (u.CeilingDelta > 0f) hype?.RaiseCeiling(u.CeilingDelta);
            if (u.RateDelta > 0f) hype?.RaiseRate(u.RateDelta);
            venue?.Apply(u.Kind, level);

            _bus?.Publish(new UpgradePurchased(u.Id));
            return true;
        }
    }
}
```

- [ ] **Step 2: Verify compile**

Expected: compiles; remaining error only in `DebugHud` (uses the old `CapacityUpgrade`/`Cost`) — fixed next.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/UpgradeSystem.cs"
git commit -m "feat(unity): UpgradeSystem roster — levels, escalating cost, venue scaling (M3a)"
```

---

### Task 6: DebugHud — list all four upgrades

**Files:**
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs`

- [ ] **Step 1: Replace the intermission panel block**

Replace the entire `if (setController != null && setController.Current == SetController.Phase.Intermission ...)` block (the INTERMISSION box: the single capacity buy, the ability buys, and the Start Next Set button) with:

```csharp
            if (setController != null && setController.Current == SetController.Phase.Intermission)
            {
                const float px = 12, pw = 320;
                float py = 290;

                int upgradeRows = upgrades != null ? upgrades.Upgrades.Count : 0;
                int abilityRows = 0;
                if (abilities != null)
                    foreach (var a in abilities.Abilities) if (!a.Owned) abilityRows++;
                float boxH = 44f + (upgradeRows + abilityRows + 1) * 30f;
                GUI.Box(new Rect(px, py, pw, boxH), "INTERMISSION");

                float y = py + 26f;
                if (upgrades != null)
                {
                    foreach (var u in upgrades.Upgrades)
                    {
                        int cost = upgrades.CurrentCost(u);
                        int lvl = upgrades.LevelOf(u);
                        bool afford = upgrades.CanAfford(u);
                        GUI.enabled = afford;
                        if (GUI.Button(new Rect(px + 10, y, pw - 20, 26), $"{u.DisplayName}  Lv{lvl} (${cost})"))
                            upgrades.TryPurchase(u);
                        GUI.enabled = true;
                        y += 30f;
                    }
                }
                if (abilities != null)
                {
                    foreach (var a in abilities.Abilities)
                    {
                        if (a.Owned) continue;
                        var def = abilities.DefinitionOf(a);
                        bool afford = abilities.CanAfford(def);
                        GUI.enabled = afford;
                        if (GUI.Button(new Rect(px + 10, y, pw - 20, 26), $"Buy {def.DisplayName} (${def.Cost})"))
                            abilities.TryUnlock(def);
                        GUI.enabled = true;
                        y += 30f;
                    }
                }
                if (GUI.Button(new Rect(px + 10, y, pw - 20, 28), "Start Next Set ▶"))
                    setController.StartNextSet();
            }
```

- [ ] **Step 2: Verify compile**

Expected: the whole project compiles (no more `CapacityUpgrade`/old `Cost` references).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/DebugHud.cs"
git commit -m "feat(unity): DebugHud lists the four upgrades with escalating cost + level (M3a)"
```

---

### Task 7: PitTycoonSetup — create 4 upgrade assets + wire the list

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs`

- [ ] **Step 1: Replace the single upgrade asset creation**

Remove the `UpgradePath` constant line, and replace `var upgradeDef = LoadOrCreate<UpgradeDefinition>(UpgradePath);` with:

```csharp
            var groundsDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Grounds.asset");
            ConfigureUpgrade(groundsDef, "grounds", "Grounds Expansion", UpgradeKind.Grounds, 60, 1.6f,
                addCols: 4, addRows: 2, ceilingDelta: 15f, rateDelta: 0f);
            var stageDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Stage.asset");
            ConfigureUpgrade(stageDef, "stage", "Stage", UpgradeKind.Stage, 90, 1.7f,
                addCols: 0, addRows: 0, ceilingDelta: 40f, rateDelta: 0f);
            var lightingDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_Lighting.asset");
            ConfigureUpgrade(lightingDef, "lighting", "Lighting Rig", UpgradeKind.Lighting, 75, 1.6f,
                addCols: 0, addRows: 0, ceilingDelta: 0f, rateDelta: 2.5f);
            var paDef = LoadOrCreate<UpgradeDefinition>("Assets/Settings/Upgrade_PA.asset");
            ConfigureUpgrade(paDef, "pa", "PA / Speakers", UpgradeKind.PA, 110, 1.7f,
                addCols: 0, addRows: 0, ceilingDelta: 0f, rateDelta: 3.5f);
```

- [ ] **Step 2: Replace the upgrade wiring**

Replace the `WireRefs(upgrades, ("capacityUpgrade", upgradeDef), ("economy", economy), ("hype", hype), ("crowd", crowd));` line with:

```csharp
            WireUpgradeList(upgrades, new[] { groundsDef, stageDef, lightingDef, paDef });
            WireRefs(upgrades, ("economy", economy), ("hype", hype), ("crowd", crowd));
```

(`venue` is wired later by `FestivalSceneSetup`, which creates the `VenueController`.)

- [ ] **Step 3: Add the helper methods**

Add to the class (next to `WireAbilityList`):

```csharp
        private static void ConfigureUpgrade(UpgradeDefinition def, string id, string name, UpgradeKind kind,
            int baseCost, float growth, int addCols, int addRows, float ceilingDelta, float rateDelta)
        {
            var so = new SerializedObject(def);
            so.FindProperty("Id").stringValue = id;
            so.FindProperty("DisplayName").stringValue = name;
            so.FindProperty("Kind").enumValueIndex = (int)kind;
            so.FindProperty("BaseCost").intValue = baseCost;
            so.FindProperty("CostGrowth").floatValue = growth;
            so.FindProperty("AddColumns").intValue = addCols;
            so.FindProperty("AddRows").intValue = addRows;
            so.FindProperty("CeilingDelta").floatValue = ceilingDelta;
            so.FindProperty("RateDelta").floatValue = rateDelta;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        private static void WireUpgradeList(Object target, UpgradeDefinition[] defs)
        {
            var so = new SerializedObject(target);
            var list = so.FindProperty("upgrades");
            list.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
```

- [ ] **Step 4: Verify compile**

Expected: editor compiles; the menu builds the scene with four upgrade assets wired into `UpgradeSystem`.

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/PitTycoonSetup.cs"
git commit -m "feat(editor): create the four upgrade assets + wire UpgradeSystem list (M3a)"
```

---

### Task 8: FestivalSceneSetup — create VenueController + wire it

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs`

- [ ] **Step 1: Capture the PA instances**

In `BuildFestivalScene`, the PA placements currently aren't captured. Change the two PA `PlaceModel` calls to keep their references:

```csharp
            var paLeft = PlaceModel("PASpeaker.fbx", "PA Left", new Vector3(-6f, 0f, 9f), structureMat);
            var paRight = PlaceModel("PASpeaker.fbx", "PA Right", new Vector3(6f, 0f, 9f), structureMat);
```

- [ ] **Step 2: Call a venue-wiring helper**

Immediately after the accent-light reparenting block (before `WireBeatVfx(lit, stage);`), add:

```csharp
            WireVenue(stage, paLeft, paRight);
```

- [ ] **Step 3: Add the helper method**

Add to the `FestivalSceneSetup` class:

```csharp
        private static void WireVenue(GameObject stage, GameObject paLeft, GameObject paRight)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            var venue = systems.GetComponent<VenueController>();
            if (venue == null) venue = systems.AddComponent<VenueController>();

            var lights = new System.Collections.Generic.List<Light>();
            foreach (var name in new[] { "Accent Amber", "Accent Magenta", "Accent Cyan" })
            {
                var go = GameObject.Find(name);
                if (go != null) { var l = go.GetComponent<Light>(); if (l != null) lights.Add(l); }
            }

            var vso = new SerializedObject(venue);
            SetRef(vso, "stage", stage != null ? stage.transform : null);
            SetRef(vso, "paLeft", paLeft != null ? paLeft.transform : null);
            SetRef(vso, "paRight", paRight != null ? paRight.transform : null);
            var arr = vso.FindProperty("accentLights");
            arr.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            vso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(venue);

            var upgrades = Object.FindAnyObjectByType<UpgradeSystem>();
            if (upgrades != null)
            {
                var uso = new SerializedObject(upgrades);
                SetRef(uso, "venue", venue);
                uso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(upgrades);
            }
        }
```

(`SetRef` already exists in this file from M2c.)

- [ ] **Step 4: Verify compile**

Expected: editor compiles.

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/FestivalSceneSetup.cs"
git commit -m "feat(editor): create + wire VenueController, link to UpgradeSystem (M3a)"
```

---

### Task 9: SETUP.md M3a section

**Files:**
- Modify: `SETUP.md` (append)

- [ ] **Step 1: Append**

```markdown
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
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP M3a — upgrade roster + venue scaling + tuning"
```

---

### Task 10: In-editor checkpoint (user)

**No file changes.** Manual gate.

- [ ] **Step 1: Rebuild**

Pull `feat/m3a-passive-upgrades`, recompile. Run **Build Greybox Scene**, **Apply Comic Look**, **Build Festival Scene**.

- [ ] **Step 2: Verify against the checklist**

Play → bank cash → intermission, then confirm:
- All four upgrades show as buy buttons with `Lv N ($cost)`; cost **escalates** each purchase.
- Each visibly scales its part next set/now: Grounds = denser crowd, **Stage grows**, **lights brighten**, **PA stacks grow**.
- Hype ceiling rises (Stage/Grounds); passive hype gain feels faster (Lighting/PA).
- M1 loop + M2 art + M3b abilities all intact.

- [ ] **Step 3: Tune & report**

Adjust costs/growth/deltas + `VenueController` steps to taste; report + a clip.

---

### Task 11: Commit generated assets + open PR

**After Task 10 confirms.**

- [ ] **Step 1: Stage**

```bash
git status --short
git add "Assets/Settings/Upgrade_Grounds.asset" "Assets/Settings/Upgrade_Grounds.asset.meta" \
        "Assets/Settings/Upgrade_Stage.asset" "Assets/Settings/Upgrade_Stage.asset.meta" \
        "Assets/Settings/Upgrade_Lighting.asset" "Assets/Settings/Upgrade_Lighting.asset.meta" \
        "Assets/Settings/Upgrade_PA.asset" "Assets/Settings/Upgrade_PA.asset.meta" \
        "Assets/PitTycoon/Domain/UpgradePricing.cs.meta" "Assets/PitTycoon/Unity/VenueController.cs.meta" \
        "Assets/Scenes/Greybox.unity"
git status --short
```
Discard line-ending-only churn (materials/prefab/ShaderGraphSettings) with `git checkout -- <file>`.

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(unity): generated upgrade roster assets + venue/scene wiring (M3a)"
```

- [ ] **Step 3: Push + PR**

```bash
git push -u origin feat/m3a-passive-upgrades
gh pr create --base master --head feat/m3a-passive-upgrades \
  --title "M3a: passive upgrade roster (Grounds, Stage, Lighting, PA)" \
  --body "Generalizes the one hardcoded upgrade into the brief's four, each buyable repeatedly at an escalating cost (Domain UpgradePricing, TDD — suite now 61 green) and each visibly scaling the M2b venue via a new VenueController (stage grows, lights brighten, PA grows; Grounds densifies the crowd). HypeSystem gains RaiseRate for the Lighting/PA effect. No regression; M1/M2/M3b intact. The organic hype-driven crowd model (Grounds reworked into a capacity raise) is deferred per spec §9. Spec: docs/superpowers/specs/2026-06-19-pit-tycoon-m3a-passive-upgrades-design.md."
```

---

## Self-Review

**1. Spec coverage:**
- Escalating cost → Task 1 (Domain `UpgradePricing`, TDD). ✓
- `UpgradeDefinition` kind + cost + deltas → Task 2. ✓
- Generalized `UpgradeSystem` (list, level, pricing, effects, venue) → Task 5. ✓
- `HypeSystem.RaiseRate` → Task 3. ✓
- `VenueController` visible scaling (stage/lights/PA) → Task 4. ✓
- Four upgrade assets, per-kind effects → Task 7. ✓
- HUD lists all four w/ escalating cost + level → Task 6. ✓
- Wiring (UpgradeSystem list; VenueController + link) → Tasks 7, 8. ✓
- Grounds keeps direct `crowd.Grow`; organic crowd deferred → Task 5 (`AddColumns/AddRows`), noted. ✓
- Verification (Domain TDD + manual) → Task 1 step 4, Task 10. ✓

**2. Placeholder scan:** No TBD/TODO; every code step is complete.

**3. Type/name consistency:** `UpgradePricing.CostAtLevel(int,float,int)` matches Task 1 + the `UpgradeSystem.CurrentCost` call (Task 5) + tests. `UpgradeKind { Grounds, Stage, Lighting, PA }` (Task 2) matches `VenueController.Apply` switch (Task 4), `UpgradeSystem` `venue.Apply(u.Kind, level)` (Task 5), and `ConfigureUpgrade`'s `(int)kind` (Task 7). Serialized names — `upgrades`/`economy`/`hype`/`crowd`/`venue` on UpgradeSystem (Task 5) match `WireUpgradeList`/`WireRefs`/`WireVenue` (Tasks 7, 8); `stage`/`paLeft`/`paRight`/`accentLights` on VenueController (Task 4) match `WireVenue` (Task 8). `Upgrades`/`CurrentCost`/`LevelOf`/`CanAfford`/`TryPurchase` used by DebugHud (Task 6) all exist on the new UpgradeSystem (Task 5). `EconomySystem.CanAfford(int)`/`TrySpend(int)` and `HypeSystem.RaiseCeiling`/`RaiseRate` and `CrowdController.Grow` all match existing/Task-3 signatures.
