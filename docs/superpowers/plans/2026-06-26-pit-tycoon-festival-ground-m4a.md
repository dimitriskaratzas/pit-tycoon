# Festival Ground (M4a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the single fixed stage into an open-air festival ground that visibly fills up — spending in the intermission raises distinct new structures at authored "build spots," each carrying an economic effect — laying a data-driven, reskinnable foundation.

**Architecture:** An additive build-spot layer parallel to the existing upgrade system. A data `VenueLayout` (ScriptableObject) lists build spots (prefab, pose, camera waypoint, cost, effect). `BuildSpotController` owns the spot geometry (raise on build, translucent ghost on preview); `BuildSystem` owns the one-shot purchase (cash check → effect via existing primitives → raise → `StructureBuilt`). The existing `ShopView`/ghost-preview/`CameraRig` machinery is reused: build-spots become a third shop category, and the intermission camera gains a wide survey pose plus a close live pose. Nothing in the working upgrade/crowd/economy/Domain code is modified except additive event + wiring.

**Tech Stack:** Unity 6 LTS (6000.4.11f1), URP, C# (PitTycoon.Unity / PitTycoon.Unity.UI / PitTycoon.Unity.Editor assemblies), plain-C# PitTycoon.Domain (netstandard2.1) with NUnit tests on net10.0. UGUI + TextMeshPro. New-Input-System only.

## Global Constraints

- **Commit messages:** imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- **No new Domain logic.** The only Domain change is one additive event struct (`StructureBuilt`) — no logic, no new tests. `dotnet test PitTycoon.Domain.slnx` must stay at **73 passed** throughout (regression guard; it is the only automated gate — Unity C# compiles only in-Editor).
- **No new effect primitives.** Build effects reuse `HypeSystem.RaiseRate(float)` and `CrowdController.RaiseCapacity(int)` only. New effect types (passive cash, multipliers, per-ability bonuses) are out of scope (M4c).
- **Additive only.** Do not modify the existing 4 upgrades, `VenueController`, the crowd organic-pit code, `EconomySystem`, or `HypeSystem` behavior. The only edits to existing files are: `UpgradePreviewController` (add build-preview + two camera poses), `ShopView`/`HudController`/`GameBootstrap` (wire the new system), `HudSetup` (add the Build container), and `Events.cs` (add `StructureBuilt`).
- **Systems communicate through `EventBus`/interfaces, not singletons or `FindObjectOfType` at runtime** (editor builders may use `FindFirstObjectByType`).
- **Every purchase is visible.** Each build raises an on-screen structure and its effect is already visualized (faster hype / denser crowd). No invisible stat bumps.
- **Greybox is fine.** M4a structures are primitive-based greybox prefabs; real art is M4d. Make them readable in silhouette.
- **New-Input-System only.** (Not directly relevant to M4a, but don't introduce `UnityEngine.Input` legacy calls.)
- All new runtime types live in namespace `PitTycoon.Unity`; UI types in `PitTycoon.Unity.UI`; editor types in `PitTycoon.Unity.EditorTools`.

---

## File Structure

**New (Unity runtime, `Assets/PitTycoon/Unity/`):**
- `VenueLayout.cs` — `VenueLayout` ScriptableObject + `BuildSpot` serializable struct + `BuildEffectKind` enum. Pure data.
- `BuildSpotController.cs` — owns build-spot geometry: built-state set, `Build(id)`, `PreviewSpot(id)`/`ClearPreview()`.
- `BuildSystem.cs` — one-shot purchase transaction: `Initialize(bus)`, `Spots`, `IsBuilt`, `CanAfford`, `TryBuild`.

**New (Editor, `Assets/PitTycoon/Unity/Editor/`):**
- `StructureGreyboxPrefabs.cs` — builds primitive greybox structure prefab assets (Second Stage, Camping Field, Entrance Gate).
- `FestivalGroundSetup.cs` — `Pit Tycoon → Build Festival Ground`: enlarges the ground, creates/loads the `VenueLayout` asset, adds + wires `BuildSpotController`/`BuildSystem`, seeds the survey/live camera poses + `buildSpots` ref on `UpgradePreviewController`, wires `GameBootstrap`.

**New (assets, created by the builders):**
- `Assets/PitTycoon/Art/Prefabs/Greybox/SecondStage.prefab`, `CampingField.prefab`, `EntranceGate.prefab`.
- `Assets/Settings/OpenAirLayout.asset` (a `VenueLayout`).

**Modified:**
- `Assets/PitTycoon/Domain/Events.cs` — add `StructureBuilt` event struct.
- `Assets/PitTycoon/Unity/UpgradePreviewController.cs` — add `buildSpots` ref, `BeginBuildPreview(BuildSpot)`, survey/live poses + `GoToSurvey()`/`GoToLive()`; `ClearGhost()` also clears build ghosts; `ReturnHome()` → survey; remove `ForceClear()`.
- `Assets/PitTycoon/Unity/UI/ShopView.cs` — third "Build" category (container, rows, select/buy), `StructureBuilt` subscription, `ReapplySelection` for builds, `Initialize` gains `BuildSystem`.
- `Assets/PitTycoon/Unity/UI/HudController.cs` — forward `BuildSystem` to `ShopView`; `SetStarted → GoToLive`, `SetEnded → GoToSurvey`.
- `Assets/PitTycoon/Unity/GameBootstrap.cs` — `buildSpots` + `builds` optional refs, `builds.Initialize`, pass `builds` to `hud.Initialize`.
- `Assets/PitTycoon/Unity/Editor/HudSetup.cs` — add the "Build" group container to the shop panel; wire `ShopView.buildContainer`.
- `SETUP.md` — Build Festival Ground step + verification checklist.

---

### Task 1: VenueLayout data types

**Files:**
- Create: `Assets/PitTycoon/Unity/VenueLayout.cs`

**Interfaces:**
- Produces:
  - `enum PitTycoon.Unity.BuildEffectKind { HypeRate, Capacity }`
  - `struct PitTycoon.Unity.BuildSpot` with public fields: `string id`, `string label`, `GameObject structurePrefab`, `Vector3 position`, `Vector3 euler`, `Vector3 cameraPosition`, `Vector3 cameraEuler`, `int cost`, `BuildEffectKind effect`, `float effectMagnitude`, `Color color`.
  - `class PitTycoon.Unity.VenueLayout : ScriptableObject` with `public BuildSpot[] spots;` and `[CreateAssetMenu]`.

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>What kind of effect a build spot grants when built. M4a reuses existing
    /// effect primitives only (hype rate, crowd capacity); new kinds are M4c.</summary>
    public enum BuildEffectKind
    {
        HypeRate,   // -> HypeSystem.RaiseRate(effectMagnitude)
        Capacity    // -> CrowdController.RaiseCapacity(round(effectMagnitude))
    }

    /// <summary>One authored build spot on the festival ground: a structure prefab that rises
    /// at a fixed pose, the camera pose to inspect it, its cost, and the effect it grants.
    /// Pure data — no behavior.</summary>
    [System.Serializable]
    public struct BuildSpot
    {
        public string id;                 // stable identity (used for built-state + StructureBuilt)
        public string label;              // shop row label
        public GameObject structurePrefab;
        public Vector3 position;          // WORLD position the structure/ghost is placed at
        public Vector3 euler;             // WORLD rotation (euler degrees)
        public Vector3 cameraPosition;    // camera fly-to pose for preview
        public Vector3 cameraEuler;
        public int cost;
        public BuildEffectKind effect;
        public float effectMagnitude;     // rate delta, or capacity (rounded to int)
        public Color color;               // optional row tint
    }

    /// <summary>The data-driven festival-ground layout: the set of build spots. One asset =
    /// one theme (M4a ships an open-air layout). Reskinning later = a new asset + prefabs,
    /// no code changes.</summary>
    [CreateAssetMenu(menuName = "Pit Tycoon/Venue Layout", fileName = "VenueLayout")]
    public sealed class VenueLayout : ScriptableObject
    {
        public BuildSpot[] spots;
    }
}
```

- [ ] **Step 2: Verify Domain tests still pass (regression guard)**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, Skipped: 0, Total: 73`

(This task adds no Domain code; the run confirms the repo is green before we build on it. Unity compile is verified in-Editor at the M4a checkpoint.)

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/VenueLayout.cs
git commit -m "feat(unity): add VenueLayout + BuildSpot data types for the festival ground"
```

---

### Task 2: BuildSpotController

**Files:**
- Create: `Assets/PitTycoon/Unity/BuildSpotController.cs`

**Interfaces:**
- Consumes (Task 1): `VenueLayout`, `BuildSpot`.
- Produces:
  - `[SerializeField] VenueLayout layout; [SerializeField] Material ghostMaterial;` (serialized field names `layout`, `ghostMaterial` — used by the editor builder).
  - `public VenueLayout Layout => layout;`
  - `public bool IsBuilt(string id)`
  - `public void Build(string id)` — instantiates the structure at the spot's world pose, marks built. Idempotent.
  - `public void PreviewSpot(string id)` — translucent ghost clone at the spot. Clears prior preview first.
  - `public void ClearPreview()`

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns the build-spot geometry on the festival ground. Reads a VenueLayout, raises the real
    /// structure at a spot on Build, and shows a translucent ghost clone on PreviewSpot (reusing
    /// the shared ghost material). Mirrors VenueController's ghost discipline: clear before
    /// re-preview, clear on destroy. Owns no economy — BuildSystem drives purchases.
    /// </summary>
    public sealed class BuildSpotController : MonoBehaviour
    {
        [SerializeField] private VenueLayout layout;
        [Tooltip("Translucent material for ghost-preview clones (wired by Build Festival Ground).")]
        [SerializeField] private Material ghostMaterial;

        private readonly HashSet<string> _built = new HashSet<string>();
        private readonly Dictionary<string, GameObject> _raised = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _ghosts = new List<GameObject>();

        public VenueLayout Layout => layout;
        public bool IsBuilt(string id) => _built.Contains(id);

        /// <summary>Raise the real structure at a spot. Idempotent (no-op if already built).</summary>
        public void Build(string id)
        {
            if (layout == null || _built.Contains(id)) return;
            if (!TryGet(id, out BuildSpot spot) || spot.structurePrefab == null) return;
            ClearPreview();
            GameObject go = Instantiate(spot.structurePrefab, transform);
            go.name = string.IsNullOrEmpty(spot.id) ? "Structure" : spot.id;
            go.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(spot.euler));
            _raised[id] = go;
            _built.Add(id);
        }

        /// <summary>Show a translucent ghost of the structure at an un-built spot. Idempotent:
        /// clears any prior preview first.</summary>
        public void PreviewSpot(string id)
        {
            if (layout == null || _built.Contains(id)) return;
            if (!TryGet(id, out BuildSpot spot) || spot.structurePrefab == null) return;
            ClearPreview();
            GameObject ghost = Instantiate(spot.structurePrefab, transform);
            ghost.name = spot.id + " (ghost)";
            ghost.transform.SetPositionAndRotation(spot.position, Quaternion.Euler(spot.euler));
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            if (ghostMaterial != null)
                foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;
            _ghosts.Add(ghost);
        }

        /// <summary>Destroy any active ghost clones.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
        }

        private void OnDestroy() => ClearPreview();

        private bool TryGet(string id, out BuildSpot spot)
        {
            if (layout != null && layout.spots != null)
                foreach (var s in layout.spots)
                    if (s.id == id) { spot = s; return true; }
            spot = default;
            return false;
        }
    }
}
```

- [ ] **Step 2: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 3: Self-review checklist**

Confirm: `Build` and `PreviewSpot` set **world** pose via `SetPositionAndRotation`; ghost strips colliders + swaps `sharedMaterial`; `ClearPreview` is called at the start of both `Build` and `PreviewSpot` and on destroy (no orphan ghosts); built spots are tracked by `id`.

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/BuildSpotController.cs
git commit -m "feat(unity): BuildSpotController raises structures + ghost-previews build spots"
```

---

### Task 3: StructureBuilt event + BuildSystem

**Files:**
- Modify: `Assets/PitTycoon/Domain/Events.cs` (append one struct)
- Create: `Assets/PitTycoon/Unity/BuildSystem.cs`

**Interfaces:**
- Consumes (Tasks 1–2): `BuildSpot`, `BuildSpotController` (`Layout`, `IsBuilt`, `Build`); existing `EconomySystem` (`CanAfford(int)`, `TrySpend(int)`), `HypeSystem` (`RaiseRate(float)`), `CrowdController` (`RaiseCapacity(int)`), `EventBus` (`Publish`).
- Produces:
  - `struct PitTycoon.Domain.StructureBuilt { string SpotId }`
  - `BuildSystem` with `public void Initialize(EventBus bus)`, `public IReadOnlyList<BuildSpot> Spots`, `public bool IsBuilt(BuildSpot spot)`, `public bool CanAfford(BuildSpot spot)`, `public bool TryBuild(BuildSpot spot)`. Serialized field names: `economy`, `hype`, `crowd`, `spots`.

- [ ] **Step 1: Append the event to `Events.cs`**

Add this struct inside `namespace PitTycoon.Domain` (after `AbilityUnlocked`, before the closing brace):

```csharp
    /// <summary>Raised when a build-spot structure is built during intermission.</summary>
    public readonly struct StructureBuilt
    {
        public string SpotId { get; }
        public StructureBuilt(string spotId) { SpotId = spotId; }
    }
```

- [ ] **Step 2: Create `BuildSystem.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// One-shot purchase transaction for build spots (mirrors UpgradeSystem, but a spot is built
    /// once — not leveled). On TryBuild: spends via EconomySystem, applies the spot's effect via
    /// existing primitives (HypeSystem.RaiseRate / CrowdController.RaiseCapacity), asks
    /// BuildSpotController to raise the structure, and publishes StructureBuilt. Owns no geometry.
    /// </summary>
    public sealed class BuildSystem : MonoBehaviour
    {
        [SerializeField] private EconomySystem economy;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private BuildSpotController spots;

        private EventBus _bus;

        public void Initialize(EventBus bus) { _bus = bus; }

        public IReadOnlyList<BuildSpot> Spots =>
            (spots != null && spots.Layout != null && spots.Layout.spots != null)
                ? spots.Layout.spots
                : System.Array.Empty<BuildSpot>();

        public bool IsBuilt(BuildSpot spot) => spots != null && spots.IsBuilt(spot.id);
        public bool CanAfford(BuildSpot spot) => economy != null && economy.CanAfford(spot.cost);

        /// <summary>Attempt to build a spot; applies its effect + raises the structure on success.</summary>
        public bool TryBuild(BuildSpot spot)
        {
            if (economy == null || spots == null) return false;
            if (spots.IsBuilt(spot.id)) return false;
            if (!economy.TrySpend(spot.cost)) return false;

            switch (spot.effect)
            {
                case BuildEffectKind.HypeRate: hype?.RaiseRate(spot.effectMagnitude); break;
                case BuildEffectKind.Capacity: crowd?.RaiseCapacity(Mathf.RoundToInt(spot.effectMagnitude)); break;
            }

            spots.Build(spot.id);
            _bus?.Publish(new StructureBuilt(spot.id));
            return true;
        }
    }
}
```

- [ ] **Step 3: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...` (the new event struct has no logic and no test; the count is unchanged.)

- [ ] **Step 4: Self-review checklist**

Confirm: `TryBuild` re-checks `IsBuilt` and only spends via `TrySpend` (no double-spend); effect routes through existing primitives only (`RaiseRate`/`RaiseCapacity`); `StructureBuilt` published after the build; serialized field names match the wiring expected by Task 9 (`economy`, `hype`, `crowd`, `spots`).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/Events.cs Assets/PitTycoon/Unity/BuildSystem.cs
git commit -m "feat: add StructureBuilt event + BuildSystem one-shot build purchase"
```

---

### Task 4: UpgradePreviewController — build-preview + two camera poses

**Files:**
- Modify: `Assets/PitTycoon/Unity/UpgradePreviewController.cs`

**Interfaces:**
- Consumes (Tasks 1–2): `BuildSpot`, `BuildSpotController` (`PreviewSpot`, `ClearPreview`).
- Produces (used by Tasks 5/6/9):
  - `public void BeginBuildPreview(BuildSpot spot)`
  - `public void GoToSurvey()` / `public void GoToLive()`
  - `public void ReturnHome()` (now flies to the survey pose)
  - New serialized field names for the editor builder: `buildSpots`, `surveyCamPosition`, `surveyCamEuler`, `liveCamPosition`, `liveCamEuler`.
  - **Removed:** `ForceClear()` (its only caller, `HudController.OnSetStarted`, switches to `GoToLive` in Task 6).

- [ ] **Step 1: Add the `buildSpots` serialized field**

After the existing `[SerializeField] private AbilitySystem abilities;` line, add:

```csharp
        [SerializeField] private BuildSpotController buildSpots;
```

- [ ] **Step 2: Add the survey/live camera pose fields**

After the existing `[SerializeField] private float flyDuration = 0.6f;` line, add:

```csharp

        [Header("Resting camera poses (filled by Build Festival Ground, tune in play)")]
        [Tooltip("Wide overview of the whole ground — the intermission resting pose.")]
        [SerializeField] private Vector3 surveyCamPosition = new Vector3(0f, 45f, -55f);
        [SerializeField] private Vector3 surveyCamEuler = new Vector3(40f, 0f, 0f);
        [Tooltip("Close to the stage/crowd — the live-set resting pose (= the camera's start pose).")]
        [SerializeField] private Vector3 liveCamPosition = new Vector3(0f, 9f, -11f);
        [SerializeField] private Vector3 liveCamEuler = new Vector3(20f, 0f, 0f);
```

- [ ] **Step 3: Add `BeginBuildPreview`**

Add this method after `BeginAbilityPreview`:

```csharp
        public void BeginBuildPreview(BuildSpot spot)
        {
            ClearGhost();
            if (buildSpots != null) buildSpots.PreviewSpot(spot.id);
            if (rig != null) rig.FlyTo(spot.cameraPosition, Quaternion.Euler(spot.cameraEuler), flyDuration);
        }
```

- [ ] **Step 4: Replace `ReturnHome` and `ForceClear` with the two-pose methods**

Replace this block:

```csharp
        /// <summary>Clear the preview and tween the camera back to the overview.</summary>
        public void ReturnHome()
        {
            ClearGhost();
            if (rig != null) rig.ReturnHome();
        }

        /// <summary>Clear the preview and snap the camera home instantly (set start).</summary>
        public void ForceClear()
        {
            ClearGhost();
            if (rig != null) rig.SnapHome();
        }
```

with:

```csharp
        /// <summary>Clear the preview and fly to the wide survey pose (⌂ Overview, intermission start).</summary>
        public void ReturnHome() => GoToSurvey();

        /// <summary>Fly to the wide survey pose (intermission). Clears any active ghost.</summary>
        public void GoToSurvey()
        {
            ClearGhost();
            if (rig != null) rig.FlyTo(surveyCamPosition, Quaternion.Euler(surveyCamEuler), flyDuration);
        }

        /// <summary>Fly to the close live pose (set start). Clears any active ghost so no set begins mid-ghost.</summary>
        public void GoToLive()
        {
            ClearGhost();
            if (rig != null) rig.FlyTo(liveCamPosition, Quaternion.Euler(liveCamEuler), flyDuration);
        }
```

- [ ] **Step 5: Make `ClearGhost` also clear build ghosts**

In `ClearGhost`, add the build-spots line:

```csharp
        private void ClearGhost()
        {
            if (venue != null) venue.ClearPreview();
            if (crowd != null) crowd.ClearPreview();
            if (buildSpots != null) buildSpots.ClearPreview();
        }
```

- [ ] **Step 6: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 7: Self-review checklist**

Confirm: `BeginBuildPreview` clears prior ghost before showing the new one and flies via `FlyTo` (re-targetable); `GoToLive`/`GoToSurvey` both clear the ghost; `ForceClear` is fully removed (grep the repo — only `HudController` referenced it, updated in Task 6); `CameraRig` is **not** modified.

- [ ] **Step 8: Commit**

```bash
git add Assets/PitTycoon/Unity/UpgradePreviewController.cs
git commit -m "feat(unity): UpgradePreviewController build-spot preview + survey/live camera poses"
```

---

### Task 5: ShopView — Build category

**Files:**
- Modify: `Assets/PitTycoon/Unity/UI/ShopView.cs`

**Interfaces:**
- Consumes (Tasks 1/3/4): `BuildSpot`, `BuildSystem` (`Spots`, `IsBuilt`, `CanAfford`, `TryBuild`), `UpgradePreviewController.BeginBuildPreview`, `StructureBuilt` event.
- Produces (used by Task 6): `Initialize(EventBus, EconomySystem, SetController, AbilitySystem, UpgradeSystem, UpgradePreviewController, BuildSystem)` — `BuildSystem` appended last. New serialized field name for the editor builder: `buildContainer`.

- [ ] **Step 1: Add the build container field**

After `[SerializeField] private RectTransform abilityContainer;` add:

```csharp
        [SerializeField] private RectTransform buildContainer;
```

- [ ] **Step 2: Add the `_builds` field, the build-row map, and the build selection id**

After `private UpgradePreviewController _preview;` add:

```csharp
        private BuildSystem _builds;
```

After `private readonly Dictionary<ShopRowWidget, AbilityDefinition> _rowAbility = ...;` add:

```csharp
        private readonly Dictionary<ShopRowWidget, BuildSpot> _rowBuild = new Dictionary<ShopRowWidget, BuildSpot>();
```

After `private AbilityDefinition _selectedAbility;` add:

```csharp
        private string _selectedBuildId;   // builds are structs -> track selection by id
```

- [ ] **Step 3: Extend `Initialize` to take and store `BuildSystem`, and subscribe to `StructureBuilt`**

Replace the `Initialize` signature + body's assignments and subscriptions:

```csharp
        public void Initialize(EventBus bus, EconomySystem economy, SetController set,
                               AbilitySystem abilities, UpgradeSystem upgrades,
                               UpgradePreviewController preview, BuildSystem builds)
        {
            _bus = bus; _economy = economy; _set = set; _abilities = abilities;
            _upgrades = upgrades; _preview = preview; _builds = builds;

            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (startNextSetButton != null) startNextSetButton.onClick.AddListener(OnStartNextSet);
            if (returnHomeButton != null) returnHomeButton.onClick.AddListener(OnReturnHome);

            _bus.Subscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Subscribe<AbilityUnlocked>(OnAbilityUnlocked);
            _bus.Subscribe<StructureBuilt>(OnStructureBuilt);
        }
```

In `OnDestroy`, add the matching unsubscribe after the `AbilityUnlocked` line:

```csharp
            _bus.Unsubscribe<StructureBuilt>(OnStructureBuilt);
```

- [ ] **Step 4: Add the `StructureBuilt` handler**

After `private void OnAbilityUnlocked(AbilityUnlocked e) => RefreshIfVisible();` add:

```csharp
        private void OnStructureBuilt(StructureBuilt e) => RefreshIfVisible();
```

- [ ] **Step 5: Reset the build selection in `ClearSelectionState`**

Replace the body of `ClearSelectionState`:

```csharp
        private void ClearSelectionState()
        {
            if (_selected != null && _selected.Actions != null) _selected.Actions.SetActive(false);
            _selected = null; _selectedUpgrade = null; _selectedAbility = null; _selectedBuildId = null;
        }
```

- [ ] **Step 6: Clear the build-row map in `Rebuild` and add the build loop**

In `Rebuild`, add `_rowBuild.Clear();` next to the other `.Clear()` calls:

```csharp
            _rowUpgrade.Clear();
            _rowAbility.Clear();
            _rowBuild.Clear();
```

After the abilities loop (the `if (_abilities != null ...)` block) and **before** `ReapplySelection();`, add:

```csharp
            if (_builds != null && buildContainer != null && rowTemplate != null)
            {
                foreach (var s in _builds.Spots)
                {
                    if (_builds.IsBuilt(s)) continue;
                    bool afford = _builds.CanAfford(s);
                    ShopRowWidget row = MakeRow(buildContainer, s.label, $"${s.cost}", afford);
                    _rowBuild[row] = s;
                    BuildSpot captured = s;
                    if (row.Button != null) row.Button.onClick.AddListener(() => SelectBuild(row, captured));
                    if (row.BuyButton != null) row.BuyButton.onClick.AddListener(() => BuyBuild(captured));
                    if (row.CancelButton != null) row.CancelButton.onClick.AddListener(OnCancelButton);
                }
            }
```

- [ ] **Step 7: Add `SelectBuild` and `BuyBuild`**

After `SelectAbility`, add:

```csharp
        private void SelectBuild(ShopRowWidget row, BuildSpot spot)
        {
            if (_builds == null) return;
            SetSelected(row);
            _selectedBuildId = spot.id; _selectedUpgrade = null; _selectedAbility = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _builds.CanAfford(spot);
            _preview?.BeginBuildPreview(spot);
        }
```

After `BuyAbility`, add:

```csharp
        private void BuyBuild(BuildSpot spot)
        {
            // TryBuild publishes StructureBuilt -> Rebuild; the now-built spot drops from the list,
            // so ReapplySelection clears the selection and the preview (real structure already raised).
            _builds?.TryBuild(spot);
        }
```

- [ ] **Step 8: Handle build re-selection in `ReapplySelection`**

Replace `ReapplySelection` with the version that also handles the build id:

```csharp
        private void ReapplySelection()
        {
            if (_selectedUpgrade != null)
            {
                ShopRowWidget row = FindRow(_rowUpgrade, _selectedUpgrade);
                if (row != null) SelectUpgrade(row, _selectedUpgrade);
                else ClearSelectionState();
            }
            else if (_selectedAbility != null)
            {
                ShopRowWidget row = FindRow(_rowAbility, _selectedAbility);
                if (row != null) SelectAbility(row, _selectedAbility);
                else { ClearSelectionState(); _preview?.Cancel(); }
            }
            else if (_selectedBuildId != null)
            {
                ShopRowWidget row = FindBuildRow(_selectedBuildId);
                if (row != null) SelectBuild(row, _rowBuild[row]);
                else { ClearSelectionState(); _preview?.Cancel(); }
            }
        }

        private ShopRowWidget FindBuildRow(string id)
        {
            foreach (var kv in _rowBuild) if (kv.Value.id == id) return kv.Key;
            return null;
        }
```

- [ ] **Step 9: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 10: Self-review checklist**

Confirm: build rows reuse `MakeRow` + the select→Buy/Cancel flow; selection survives `Rebuild` via `_selectedBuildId`; after a build the spot leaves the list and `ReapplySelection` clears selection + cancels the preview (no orphan ghost); `_builds`/`buildContainer` are null-guarded everywhere (shop still works before `Build Festival Ground` runs); the `Initialize` signature matches what Task 6 passes.

- [ ] **Step 11: Commit**

```bash
git add Assets/PitTycoon/Unity/UI/ShopView.cs
git commit -m "feat(unity): ShopView Build category — select/preview/buy build spots"
```

---

### Task 6: HudController + GameBootstrap wiring

**Files:**
- Modify: `Assets/PitTycoon/Unity/UI/HudController.cs`
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

**Interfaces:**
- Consumes (Tasks 3/4/5): `BuildSystem`, `BuildSpotController`, `UpgradePreviewController.GoToLive/GoToSurvey`, `ShopView.Initialize(...,BuildSystem)`.
- Produces: `HudController.Initialize(EventBus, HypeSystem, EconomySystem, SetController, AbilitySystem, UpgradeSystem, UpgradePreviewController, BuildSystem)` — `BuildSystem` appended last. `GameBootstrap` serialized field names `buildSpots`, `builds` (optional).

- [ ] **Step 1: Update `HudController.Initialize` and the set-lifecycle camera**

Replace the `Initialize` signature + the `shopView.Initialize` forward:

```csharp
        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities, UpgradeSystem upgrades,
                               UpgradePreviewController preview, BuildSystem builds)
        {
            _bus = bus;
            _preview = preview;

            if (liveView != null) liveView.Initialize(bus, hype, economy, set, abilities);
            if (shopView != null) shopView.Initialize(bus, economy, set, abilities, upgrades, preview, builds);

            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);

            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Hide();
        }
```

Replace `OnSetStarted` and `OnSetEnded`:

```csharp
        private void OnSetStarted(SetStarted e)
        {
            _preview?.GoToLive();          // clears any ghost + flies the camera down to the stage/crowd
            if (shopView != null) shopView.Hide();
            if (liveView != null) liveView.Show();
        }

        private void OnSetEnded(SetEnded e)
        {
            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Show(e.CashEarned);
            _preview?.GoToSurvey();        // pull back to the wide overview for building
        }
```

- [ ] **Step 2: Add the optional refs + init + forward in `GameBootstrap`**

After `[SerializeField] private UpgradePreviewController preview;` add:

```csharp
        [Tooltip("Optional — wired by Build Festival Ground. Null = no build-spot system.")]
        [SerializeField] private BuildSystem builds;
```

In `Awake`, after `upgrades.Initialize(Bus);` add:

```csharp
            builds?.Initialize(Bus);
```

Replace the `hud.Initialize(...)` call to pass `builds`:

```csharp
            hud.Initialize(Bus, hype, economy, setController, abilities, upgrades, preview, builds);
```

(Do **not** add `builds` to the null-guard `if (analyzer == null || ...)` block — it is optional so the game runs before `Build Festival Ground` wires it, consistent with `preview`. `BuildSpotController` needs no `GameBootstrap` ref: it is found by the editor builder via `FindFirstObjectByType` and referenced by `BuildSystem.spots`.)

- [ ] **Step 3: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 4: Self-review checklist**

Confirm: `Initialize` arg order matches `ShopView.Initialize` (Task 5) and the `GameBootstrap` call; `GoToLive`/`GoToSurvey` replace the old `ForceClear`; `builds`/`buildSpots` are optional (not in the null-guard); the game still boots with them unset.

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Unity/UI/HudController.cs Assets/PitTycoon/Unity/GameBootstrap.cs
git commit -m "feat(unity): wire BuildSystem through HudController + GameBootstrap; set-lifecycle camera poses"
```

---

### Task 7: HudSetup — Build group container

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/HudSetup.cs`

**Context:** `HudSetup.BuildShop` builds the shop panel: an `UPGRADES` header + `Upgrades` `VerticalLayoutGroup` container, then an `ABILITIES` header + `Abilities` container, capturing both `RectTransform`s into the `ShopRefs` struct (`r.upgrades`, `r.abilities`); `WireShop` assigns them to `ShopView.upgradeContainer`/`abilityContainer`. This task adds a `BUILD` header + `Build` container the same way (using the file's `NewUI`/`AddText`/`VerticalLayoutGroup`/`ContentSizeFitter` idiom) and wires `ShopView.buildContainer`. The code below matches `HudSetup.cs` as it stands; re-read the `BuildShop`/`ShopRefs`/`WireShop` region first if it has changed.

**Interfaces:**
- Consumes (Task 5): `ShopView` serialized field `buildContainer`.

- [ ] **Step 1: Add `builds` to the `ShopRefs` struct**

Add a `builds` `RectTransform` field next to `abilities`:

```csharp
        private struct ShopRefs
        {
            public TMP_Text cash; public TMP_Text banked; public RectTransform upgrades;
            public RectTransform abilities; public RectTransform builds; public ShopRowWidget template; public Button start;
            public Button returnHome;
        }
```

- [ ] **Step 2: Build the Build section container in `BuildShop`**

Immediately **after** the Abilities block (the line `abilities.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;`) and **before** `r.template = BuildShopRowTemplate(go.transform);`, insert:

```csharp
            var bdHead = NewUI("BuildLabel", go.transform);
            bdHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(bdHead, "BUILD", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var builds = NewUI("Build", go.transform);
            r.builds = builds.GetComponent<RectTransform>();
            var bl = builds.AddComponent<VerticalLayoutGroup>();
            bl.spacing = 6f; bl.childControlWidth = true; bl.childControlHeight = false;
            bl.childForceExpandWidth = true; bl.childForceExpandHeight = false;
            builds.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
```

- [ ] **Step 3: Wire `ShopView.buildContainer` in `WireShop`**

Add before `so.ApplyModifiedPropertiesWithoutUndo();`:

```csharp
            so.FindProperty("buildContainer").objectReferenceValue = r.builds;
```

- [ ] **Step 4: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 5: Self-review checklist**

Confirm: the Build section uses the same `NewUI`/`AddText`/`VerticalLayoutGroup`/`ContentSizeFitter` calls as Abilities (shop order Upgrades → Abilities → Build); `ShopRefs.builds` is captured; `WireShop` sets `"buildContainer"` (matches the `ShopView` field from Task 5); no other section's wiring changed.

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/HudSetup.cs
git commit -m "feat(editor): HudSetup builds + wires the shop Build category container"
```

---

### Task 8: Greybox structure prefabs builder

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/StructureGreyboxPrefabs.cs`

**Context:** Build primitive-based greybox prefab assets so M4a needs no Blender/user step (real art is M4d). Each prefab is a parent empty with primitive children, all using the existing `StructureMat`. Readable in silhouette.

**Interfaces:**
- Produces: `public static class StructureGreyboxPrefabs` with `public static GameObject EnsureSecondStage()`, `EnsureCampingField()`, `EnsureEntranceGate()` — each returns the prefab asset (loads if present, else builds + saves). Used by Task 9.

- [ ] **Step 1: Create the file**

```csharp
using UnityEditor;
using UnityEngine;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds primitive greybox structure prefabs (Second Stage / Camping Field / Entrance Gate)
    /// for the festival ground. Idempotent: loads the prefab if it already exists, else constructs
    /// it from primitives + StructureMat and saves it. Real art is a later milestone (M4d).
    /// </summary>
    public static class StructureGreyboxPrefabs
    {
        private const string PrefabDir = "Assets/PitTycoon/Art/Prefabs/Greybox";
        private const string StructureMatPath = "Assets/PitTycoon/Art/Materials/StructureMat.mat";

        public static GameObject EnsureSecondStage() => EnsurePrefab("SecondStage", BuildSecondStage);
        public static GameObject EnsureCampingField() => EnsurePrefab("CampingField", BuildCampingField);
        public static GameObject EnsureEntranceGate() => EnsurePrefab("EntranceGate", BuildEntranceGate);

        private static GameObject EnsurePrefab(string name, System.Func<Material, GameObject> build)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            EnsureDir(PrefabDir);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(StructureMatPath);
            GameObject root = build(mat);
            root.name = name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // A wide low deck + back wall + two side speaker stacks.
        private static GameObject BuildSecondStage(Material mat)
        {
            var root = new GameObject("SecondStage");
            Box(root, mat, "Deck",     new Vector3(0f, 0.4f, 0f),  new Vector3(8f, 0.8f, 5f));
            Box(root, mat, "Backwall", new Vector3(0f, 2.5f, 2.2f), new Vector3(8f, 4f, 0.4f));
            Box(root, mat, "SpkL",     new Vector3(-3.6f, 1.2f, -1.5f), new Vector3(1f, 2.4f, 1f));
            Box(root, mat, "SpkR",     new Vector3(3.6f, 1.2f, -1.5f),  new Vector3(1f, 2.4f, 1f));
            return root;
        }

        // A cluster of tents (cubes rotated 45 deg read as pitched tents) of varied size.
        private static GameObject BuildCampingField(Material mat)
        {
            var root = new GameObject("CampingField");
            var rng = new System.Random(12345);
            for (int i = 0; i < 9; i++)
            {
                float x = (i % 3) * 3f - 3f + (float)(rng.NextDouble() - 0.5);
                float z = (i / 3) * 3f - 3f + (float)(rng.NextDouble() - 0.5);
                var t = Box(root, mat, $"Tent{i}", new Vector3(x, 0.7f, z), new Vector3(1.6f, 1.6f, 1.6f));
                t.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            return root;
        }

        // Two tall pillars + a top lintel = an arch.
        private static GameObject BuildEntranceGate(Material mat)
        {
            var root = new GameObject("EntranceGate");
            Box(root, mat, "PillarL", new Vector3(-2.5f, 2.5f, 0f), new Vector3(0.8f, 5f, 0.8f));
            Box(root, mat, "PillarR", new Vector3(2.5f, 2.5f, 0f),  new Vector3(0.8f, 5f, 0.8f));
            Box(root, mat, "Lintel",  new Vector3(0f, 5.2f, 0f),    new Vector3(6f, 0.8f, 0.8f));
            return root;
        }

        private static GameObject Box(GameObject parent, Material mat, string name, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (mat != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            return go;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
```

- [ ] **Step 2: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 3: Self-review checklist**

Confirm: each `Ensure*` loads-then-creates (idempotent); prefabs use `StructureMat` if present (null-tolerant); the temp scene object is `DestroyImmediate`d after `SaveAsPrefabAsset`; `EnsureDir` creates `Greybox` under the existing `Prefabs` folder.

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/StructureGreyboxPrefabs.cs
git commit -m "feat(editor): primitive greybox prefabs for festival-ground structures"
```

---

### Task 9: FestivalGroundSetup builder

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs`

**Context:** The `Pit Tycoon → Build Festival Ground` menu. Enlarges the ground, creates/loads the `VenueLayout` asset (referencing the Task 8 prefabs), adds + wires `BuildSpotController`/`BuildSystem` on the Systems object, seeds the survey/live camera poses + `buildSpots` ref on `UpgradePreviewController`, and wires `GameBootstrap`. Idempotent; mirrors `PreviewSetup`. Run **after** Build HUD + Build Upgrade Preview.

**Interfaces:**
- Consumes: Tasks 1 (`VenueLayout`/`BuildSpot`/`BuildEffectKind`), 2 (`BuildSpotController` fields `layout`, `ghostMaterial`), 3 (`BuildSystem` fields `economy`, `hype`, `crowd`, `spots`), 4 (`UpgradePreviewController` fields `buildSpots`, `surveyCamPosition/Euler`, `liveCamPosition/Euler`), 6 (`GameBootstrap` fields `buildSpots`, `builds`), 8 (`StructureGreyboxPrefabs.Ensure*`).

- [ ] **Step 1: Create the file**

```csharp
using UnityEditor;
using UnityEngine;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds + wires the festival ground (M4a): enlarges the ground plane, creates/loads the
    /// open-air VenueLayout with greybox build spots, adds BuildSpotController + BuildSystem on the
    /// Systems object, seeds survey/live camera poses + the buildSpots ref on
    /// UpgradePreviewController, and wires GameBootstrap. Run AFTER Build HUD + Build Upgrade
    /// Preview. Idempotent.
    /// </summary>
    public static class FestivalGroundSetup
    {
        private const string LayoutPath = "Assets/Settings/OpenAirLayout.asset";
        private const string GhostMatPath = "Assets/PitTycoon/Art/Materials/GhostMat.mat";

        [MenuItem("Pit Tycoon/Build Festival Ground")]
        public static void Build()
        {
            var venue = Object.FindFirstObjectByType<VenueController>();
            var crowd = Object.FindFirstObjectByType<CrowdController>();
            var hype = Object.FindFirstObjectByType<HypeSystem>();
            var economy = Object.FindFirstObjectByType<EconomySystem>();
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var controller = Object.FindFirstObjectByType<UpgradePreviewController>();
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (venue == null || crowd == null || hype == null || economy == null || cam == null)
            {
                Debug.LogError("FestivalGroundSetup: missing VenueController/CrowdController/HypeSystem/" +
                               "EconomySystem/Camera. Run Build Greybox + Build Festival Scene + Build HUD + " +
                               "Build Upgrade Preview first.");
                return;
            }

            // 1. Enlarge the ground plane (Plane = 10u; scale 12 -> ~120u open field).
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.transform.localScale = new Vector3(12f, 1f, 12f);
            else Debug.LogWarning("FestivalGroundSetup: no 'Ground' object found to enlarge.");

            // 2. Greybox structure prefabs.
            var secondStage = StructureGreyboxPrefabs.EnsureSecondStage();
            var camping = StructureGreyboxPrefabs.EnsureCampingField();
            var gate = StructureGreyboxPrefabs.EnsureEntranceGate();

            // 3. VenueLayout asset (open-air theme). Positions/costs/effects are tuned in play.
            var layout = LoadOrCreateLayout();
            layout.spots = new[]
            {
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
            };
            EditorUtility.SetDirty(layout);

            // 4. BuildSpotController + BuildSystem on the Systems object (where VenueController lives).
            var systems = venue.gameObject;
            var spotsCtl = systems.GetComponent<BuildSpotController>();
            if (spotsCtl == null) spotsCtl = systems.AddComponent<BuildSpotController>();
            var buildSys = systems.GetComponent<BuildSystem>();
            if (buildSys == null) buildSys = systems.AddComponent<BuildSystem>();

            var ghost = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);

            // 5. Wire BuildSpotController.
            SetRef(spotsCtl, "layout", layout);
            SetRef(spotsCtl, "ghostMaterial", ghost);

            // 6. Wire BuildSystem.
            SetRef(buildSys, "economy", economy);
            SetRef(buildSys, "hype", hype);
            SetRef(buildSys, "crowd", crowd);
            SetRef(buildSys, "spots", spotsCtl);

            // 7. UpgradePreviewController: buildSpots ref + live pose (= current camera pose) + survey pose.
            if (controller != null)
            {
                SetRef(controller, "buildSpots", spotsCtl);
                var so = new SerializedObject(controller);
                so.FindProperty("liveCamPosition").vector3Value = cam.transform.position;
                so.FindProperty("liveCamEuler").vector3Value = cam.transform.eulerAngles;
                so.FindProperty("surveyCamPosition").vector3Value = new Vector3(0f, 45f, -55f);
                so.FindProperty("surveyCamEuler").vector3Value = new Vector3(40f, 0f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
            else Debug.LogWarning("FestivalGroundSetup: no UpgradePreviewController found. Run Build Upgrade Preview first.");

            // 8. GameBootstrap refs.
            if (boot != null)
            {
                SetRef(boot, "buildSpots", spotsCtl);
                SetRef(boot, "builds", buildSys);
            }
            else Debug.LogWarning("FestivalGroundSetup: no GameBootstrap found in scene.");

            EditorUtility.SetDirty(spotsCtl);
            EditorUtility.SetDirty(buildSys);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: Festival Ground built. Tune build-spot poses/costs/effects on " +
                      "OpenAirLayout and survey/live poses on UpgradePreviewController (Systems).");
        }

        private static BuildSpot MakeSpot(string id, string label, GameObject prefab,
                                          Vector3 pos, Vector3 euler, Vector3 camPos, Vector3 camEuler,
                                          int cost, BuildEffectKind effect, float magnitude)
        {
            return new BuildSpot
            {
                id = id, label = label, structurePrefab = prefab,
                position = pos, euler = euler, cameraPosition = camPos, cameraEuler = camEuler,
                cost = cost, effect = effect, effectMagnitude = magnitude, color = Color.white,
            };
        }

        private static VenueLayout LoadOrCreateLayout()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VenueLayout>(LayoutPath);
            if (existing != null) return existing;
            var layout = ScriptableObject.CreateInstance<VenueLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"FestivalGroundSetup: field '{field}' not found on {target.GetType().Name}.");
        }
    }
}
```

- [ ] **Step 2: Verify Domain tests still pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 3: Self-review checklist**

Confirm: menu path `Pit Tycoon/Build Festival Ground`; idempotent (LoadOrCreate layout; GetComponent-or-Add for both components; `Ensure*` prefabs); `SetRef` field-name strings exactly match the serialized fields from Tasks 2/3/4/6 (`layout`, `ghostMaterial`, `economy`, `hype`, `crowd`, `spots`, `buildSpots`, `liveCamPosition`, `liveCamEuler`, `surveyCamPosition`, `surveyCamEuler`, `builds`); live pose is captured from the current camera; missing-component guard logs + returns; ground enlargement is null-tolerant.

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs
git commit -m "feat(editor): add FestivalGroundSetup — enlarges ground, builds + wires build spots"
```

---

### Task 10: SETUP.md section

**Files:**
- Modify: `SETUP.md` (append a section at the end)

**Interfaces:** none (docs).

- [ ] **Step 1: Append the section**

Add at the end of `SETUP.md` (keep all existing content; add a blank line before the new heading):

```markdown
## Festival ground (M4a — open ground, build spots)

**Prereq:** run **Build HUD** and **Build Upgrade Preview** first (this step wires into the shop + the camera rig).

1. Pull + let Unity recompile (no console errors — new `VenueLayout`/`BuildSpotController`/`BuildSystem`/editor scripts).
2. Run **Pit Tycoon → Build Festival Ground**. This enlarges the ground, creates greybox structure prefabs under `Assets/PitTycoon/Art/Prefabs/Greybox/`, creates `Assets/Settings/OpenAirLayout.asset`, adds `BuildSpotController` + `BuildSystem` to the **Systems** object, seeds the survey/live camera poses + the `buildSpots` ref on `UpgradePreviewController`, and wires `GameBootstrap`.
3. Commit the generated `.meta` files (new scripts + prefabs + `OpenAirLayout.asset`) and the modified `Greybox.unity` scene.
4. Press Play and verify:
   - Reach intermission — the camera pulls back to a **wide survey** of the open ground; the shop shows a **Build** category alongside Upgrades / Abilities.
   - Click a Build row — the camera flies to that spot and a **translucent ghost** of the structure appears.
   - **Buy** raises the real structure (ghost → real); the effect shows next set (a Second Stage / Gate → hype builds faster; Camping → more crowd fills in); the spot leaves the Build list; the camera stays.
   - **Cancel** clears the ghost; the camera stays. **⌂ Overview** returns to the survey pose.
   - **Start Next Set** flies the camera down to the live (close) pose and starts cleanly (no lingering ghost); **built structures persist**.
   - F1 dev overlay and M1/M2/M3 behavior (abilities, VFX, coins, venue scaling, crowd fill, upgrade ghost-preview) all intact.

**Tuning:** build-spot poses, costs, and effect magnitudes live on `OpenAirLayout`; the wide **survey** pose, the close **live** pose, and per-spot camera waypoints live on `UpgradePreviewController` (Systems). Ghost alpha is on `GhostMat`. Tune all of these in Play from what you see.
```

- [ ] **Step 2: Verify Domain tests still pass (sanity)**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed! - Failed: 0, Passed: 73, ...`

- [ ] **Step 3: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP festival ground — build steps, verification, tuning"
```

---

### Task 11: Manual Unity checkpoint (user)

**Files:** none (in-Editor verification by the dev).

This task cannot be automated (the agent cannot operate the Unity Editor). The implementer should **stop and hand off** with the precise steps below, then await the dev's report.

- [ ] **Step 1: Open the project**, let Unity recompile — expect **no console errors** (new runtime scripts + editor builders).
- [ ] **Step 2:** Run **Pit Tycoon → Build Festival Ground** (after Build HUD + Build Upgrade Preview). Expect the "Festival Ground built" log; the **Systems** object gains `BuildSpotController` + `BuildSystem`; `OpenAirLayout.asset` + greybox prefabs are created.
- [ ] **Step 3:** Commit the generated `.meta` files + `OpenAirLayout.asset` + the modified scene (the agent will help once the dev lists `git status`).
- [ ] **Step 4: Play-test the full checklist** from `SETUP.md` (survey on intermission; Build category; ghost on select; Buy raises + effect visible; Cancel reverts; ⌂ Overview; Start Next Set → live pose + persistence; F1 + M1/M2/M3 intact).
- [ ] **Step 5: Feel pass** — tune survey/live poses, per-spot camera waypoints, structure poses, costs, and effect magnitudes in the Inspector. Report anything off (clipping structures, awkward framing, fly duration) for adjustment.

---

## Notes for the implementer

- Confirm against source before use (already verified for this plan, but re-check if the files changed): `EconomySystem.CanAfford/TrySpend`, `HypeSystem.RaiseRate`, `CrowdController.RaiseCapacity`, `UpgradeSystem.TryPurchase` pattern, `ShopView` group rendering + `ShopRowWidget` fields, `UpgradePreviewController` ghost/camera flow, `CameraRig.FlyTo`, `PreviewSetup`/`HudSetup` builder patterns.
- Unity C# is **not** compilable outside the Editor here. Each task's automated gate is `dotnet test PitTycoon.Domain.slnx` staying at 73 (regression) plus the self-review checklist; functional verification is the Task 11 checkpoint. Be rigorous in self-review — a typo'd `SerializedObject` field string only surfaces at build time in-Editor.
- Keep `BuildSpotController` focused on geometry/state and `BuildSystem` on the transaction; neither reaches into the other's internals or into crowd/hype internals beyond the public effect primitives.
- Ghost clones: no colliders, parented under the controller, single `Destroy` teardown — mirror the existing `VenueController` ghost code.
```
