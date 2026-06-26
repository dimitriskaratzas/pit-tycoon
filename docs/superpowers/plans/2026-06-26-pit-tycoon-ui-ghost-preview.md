# Upgrade Ghost-Preview (sub-project B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the intermission shop's immediate buy-on-click into a select → preview → confirm flow where selecting an upgrade flies the camera to the venue spot and shows a ghost of the change (Buy pays, Cancel reverts), with ability rows previewing a one-shot VFX demo.

**Architecture:** A `CameraRig` tweens the Main Camera between an overview "home" and per-upgrade waypoints. An `UpgradePreviewController` orchestrates preview/cancel and delegates the ghost to each system via new preview hooks (`VenueController`, `CrowdController`, `AbilitySystem`). `ShopView` becomes a two-step flow (row expands to Buy/Cancel) and drives the controller. Everything is wired by a new editor builder (`Pit Tycoon → Build Upgrade Preview`).

**Tech Stack:** Unity 6 LTS (6000.4.11f1), URP, C#, new Input System, UGUI + TextMeshPro. No new Domain logic.

## Global Constraints

- **No new Domain logic.** All code is in the Unity layer. `dotnet test PitTycoon.Domain.slnx` must stay green (**73** tests) — it is the regression guard after every task.
- **Unity assemblies compile only in the Editor.** Subagents cannot compile C# here; per-task verification is (a) `dotnet test` stays at 73 green and (b) code review against the interfaces below. Functional verification is the in-Editor checkpoint (Task 12).
- **Commit messages:** imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- **Decoupling:** systems communicate via the `EventBus` and interfaces, never concrete cross-references beyond the serialized refs the editor wires. The `UpgradePreviewController` calls only the new public hooks — it never reaches into `VenueController`/`CrowdController` internals.
- **Shop stays docked right, no scene dimming** — the camera flies through the visible scene behind it.
- Only the analyzer touches `AudioSource`; nothing here reads audio.

---

### Task 1: VenueController preview hooks

**Files:**
- Modify: `Assets/PitTycoon/Unity/VenueController.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `public void Preview(UpgradeKind kind, int level)` — show the ghost/preview for the given kind at the given absolute level. Stage/PA spawn a translucent ghost clone at the target scale; Lighting ramps the real lights to the target intensity.
  - `public void ClearPreview()` — destroy ghost clones; restore lighting to the last committed level.
  - serialized field `Material ghostMaterial` (wired later by `PreviewSetup`).

- [ ] **Step 1: Replace the file with the version below**

This keeps the existing lazy base-capture and `Apply` (the commit path), tracks the committed lighting level so `ClearPreview` can restore it exactly, and adds the ghost clone + preview hooks.

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Applies the visible step-change of a passive upgrade to the M2b venue geometry:
    /// scales the stage, scales the PA stacks, brightens the accent lights — by level.
    /// Captures the base values lazily on first Apply so repeated levels stay absolute.
    /// Also provides non-committing Preview/ClearPreview for the upgrade ghost-preview:
    /// Stage/PA show a translucent ghost clone at the target scale (real object untouched);
    /// Lighting ramps the real lights and restores them on clear.
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
        [Tooltip("Translucent material used for ghost-preview clones (wired by Build Upgrade Preview).")]
        [SerializeField] private Material ghostMaterial;

        private Vector3 _stageBase, _paLeftBase, _paRightBase;
        private float[] _lightBase;
        private bool _captured;
        private int _lightLevel;                 // last committed lighting level (for exact revert)
        private readonly List<GameObject> _ghosts = new List<GameObject>();

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
            _lightLevel = level;
            if (accentLights == null || _lightBase == null) return;
            for (int i = 0; i < accentLights.Length; i++)
                if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
        }

        // ---- Ghost preview (non-committing) --------------------------------

        /// <summary>Show the preview for an upgrade kind at an absolute level. Idempotent:
        /// clears any prior preview first.</summary>
        public void Preview(UpgradeKind kind, int level)
        {
            Capture();
            ClearPreview();
            switch (kind)
            {
                case UpgradeKind.Stage:
                    if (stage != null) _ghosts.Add(MakeGhost(stage, _stageBase * (1f + stageStep * level)));
                    break;
                case UpgradeKind.PA:
                    float f = 1f + paStep * level;
                    if (paLeft != null) _ghosts.Add(MakeGhost(paLeft, _paLeftBase * f));
                    if (paRight != null) _ghosts.Add(MakeGhost(paRight, _paRightBase * f));
                    break;
                case UpgradeKind.Lighting:
                    // No mesh to ghost — the brightness IS the preview. Ramp the real lights.
                    if (accentLights != null && _lightBase != null)
                        for (int i = 0; i < accentLights.Length; i++)
                            if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
                    break;
            }
        }

        /// <summary>Destroy ghost clones and restore lighting to the last committed level.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
            // restore real lights to the committed level (absolute, so exact)
            if (accentLights != null && _lightBase != null)
                for (int i = 0; i < accentLights.Length; i++)
                    if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * _lightLevel;
        }

        private GameObject MakeGhost(Transform src, Vector3 targetScale)
        {
            var ghost = Instantiate(src.gameObject, src.parent);
            ghost.name = src.name + " (ghost)";
            ghost.transform.localPosition = src.localPosition;
            ghost.transform.localRotation = src.localRotation;
            ghost.transform.localScale = targetScale;
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            if (ghostMaterial != null)
                foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;
            return ghost;
        }
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/VenueController.cs
git commit -m "feat(unity): VenueController ghost-preview hooks (stage/PA clone, lighting ramp)"
```

---

### Task 2: CrowdController preview hooks

**Files:**
- Modify: `Assets/PitTycoon/Unity/CrowdController.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `public void PreviewCapacity(int delta)` — spawn `delta` translucent ghost members in the slots that the next capacity expansion would occupy (rows past the current `Capacity`), without touching the fill.
  - `public void ClearPreview()` — destroy the ghost members.
  - serialized field `Material ghostMaterial` (wired later by `PreviewSetup`).

- [ ] **Step 1: Add the serialized ghost-material field**

In `Assets/PitTycoon/Unity/CrowdController.cs`, after the existing `memberPrefab` field (line ~26), add:

```csharp
        [Tooltip("Translucent material for ghost-preview members (wired by Build Upgrade Preview).")]
        [SerializeField] private Material ghostMaterial;
```

- [ ] **Step 2: Add the ghost-member list field**

After the existing private rendering fields (the `_pop` / `_live` block, line ~39), add:

```csharp
        private readonly System.Collections.Generic.List<GameObject> _ghosts =
            new System.Collections.Generic.List<GameObject>();
```

- [ ] **Step 3: Add the preview hooks**

Add these two methods right after the existing `Build()` method (after its closing brace, ~line 153). They reuse the exact layout math from `Build()` so ghost slots line up with real ones.

```csharp
        /// <summary>Show ghost members in the slots a capacity expansion of <paramref name="delta"/>
        /// would add (rows past the current Capacity). Does not touch the fill. Idempotent.</summary>
        public void PreviewCapacity(int delta)
        {
            if (_fill == null || delta <= 0) return;
            ClearPreview();

            int from = _fill.Capacity;
            int to = from + delta;

            int startRows = Mathf.CeilToInt((float)startingCapacity / columns);
            float frontZ = (startRows - 1) * spacing * 0.5f;
            float offsetX = (columns - 1) * spacing * 0.5f;

            for (int i = from; i < to; i++)
            {
                int row = i / columns;
                int col = i % columns;

                GameObject go = memberPrefab != null
                    ? Instantiate(memberPrefab)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = $"GhostCrowd_{row}_{col}";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                if (ghostMaterial != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;

                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(col * spacing - offsetX, 0f, frontZ - row * spacing);
                go.transform.localScale = Vector3.one;
                _ghosts.Add(go);
            }
        }

        /// <summary>Destroy any ghost-preview members.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
        }
```

- [ ] **Step 4: Tear down ghosts on destroy**

In the existing `OnDestroy()` method, add a `ClearPreview();` call as the first line of the method body so ghosts never leak.

- [ ] **Step 5: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/CrowdController.cs
git commit -m "feat(unity): CrowdController ghost-preview members for new capacity rows"
```

---

### Task 3: AbilitySystem.PlayDemo

**Files:**
- Modify: `Assets/PitTycoon/Unity/AbilitySystem.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `public void PlayDemo(AbilityDefinition def)` — fire the ability's VFX once (and a crowd pop) with no cooldown, ownership, economy, or bus effects. A visual taster for the shop.

- [ ] **Step 1: Add the `PlayDemo` method**

Add this method to `AbilitySystem` right after `TryFire` (after its closing brace, ~line 119). It mirrors the `switch (def.Vfx)` block in `TryFire` but commits nothing.

```csharp
        /// <summary>Preview an ability's VFX once (shop ghost-preview). No cooldown/ownership/
        /// economy/bus side effects — purely visual.</summary>
        public void PlayDemo(AbilityDefinition def)
        {
            if (def == null) return;
            Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
            switch (def.Vfx)
            {
                case VfxKind.Whirlpool: WhirlpoolVfx.Spawn(pos, 1f); break;
                case VfxKind.Woofer: ShockwaveVfx.Spawn(pos, wooferRadius); break;
                case VfxKind.LightBurst: LightBurstVfx.Flash(def.HudColor); break;
            }
            if (crowd != null) crowd.Pop(crowdPopStrength);
        }
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/AbilitySystem.cs
git commit -m "feat(unity): AbilitySystem.PlayDemo — one-shot VFX taster for the shop"
```

---

### Task 4: CameraRig

**Files:**
- Create: `Assets/PitTycoon/Unity/CameraRig.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (on the Main Camera GameObject):
  - `public void FlyTo(Vector3 position, Quaternion rotation)` — tween over the default duration.
  - `public void FlyTo(Vector3 position, Quaternion rotation, float duration)` — tween over a given duration.
  - `public void ReturnHome()` — tween back to the captured home pose.
  - `public void SnapHome()` — instantly jump to home (no tween).
  - Captures the camera's world pose as "home" in `Awake`.

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Drives the camera the rig is attached to between an overview "home" pose (captured at
    /// startup) and arbitrary target poses, with a smoothstep ease. Used by the upgrade
    /// ghost-preview to fly to a venue spot and (only on request) back home. Knows nothing
    /// about upgrades — it just moves the camera.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("Default seconds for a fly-to / return-home tween.")]
        [SerializeField] private float defaultDuration = 0.6f;

        private Vector3 _homePos;
        private Quaternion _homeRot;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _elapsed, _duration;
        private bool _tweening;

        private void Awake()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

        public void FlyTo(Vector3 position, Quaternion rotation) => FlyTo(position, rotation, defaultDuration);

        public void FlyTo(Vector3 position, Quaternion rotation, float duration)
        {
            _fromPos = transform.position;
            _fromRot = transform.rotation;
            _toPos = position;
            _toRot = rotation;
            _elapsed = 0f;
            _duration = Mathf.Max(0.0001f, duration);
            _tweening = true;
        }

        public void ReturnHome() => FlyTo(_homePos, _homeRot, defaultDuration);

        public void SnapHome()
        {
            _tweening = false;
            transform.SetPositionAndRotation(_homePos, _homeRot);
        }

        private void Update()
        {
            if (!_tweening) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float e = t * t * (3f - 2f * t);          // smoothstep ease
            transform.SetPositionAndRotation(
                Vector3.Lerp(_fromPos, _toPos, e),
                Quaternion.Slerp(_fromRot, _toRot, e));
            if (t >= 1f) _tweening = false;
        }
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/CameraRig.cs
git commit -m "feat(unity): add CameraRig — smoothstep fly-to / return-home for the camera"
```

---

### Task 5: UpgradePreviewController

**Files:**
- Create: `Assets/PitTycoon/Unity/UpgradePreviewController.cs`

**Interfaces:**
- Consumes: `CameraRig.FlyTo/ReturnHome/SnapHome` (Task 4); `VenueController.Preview/ClearPreview` (Task 1); `CrowdController.PreviewCapacity/ClearPreview` (Task 2); `AbilitySystem.PlayDemo` (Task 3); `UpgradeDefinition.Kind/AddCapacity` and `AbilityDefinition`.
- Produces:
  - `public void BeginUpgradePreview(UpgradeDefinition def, int nextLevel)` — clear prior ghost, show this upgrade's ghost at `nextLevel`, fly to its waypoint.
  - `public void BeginAbilityPreview(AbilityDefinition def)` — clear prior ghost, play the ability demo, fly to the ability waypoint.
  - `public void Cancel()` — clear the ghost; camera stays put.
  - `public void ReturnHome()` — clear the ghost; tween the camera home.
  - `public void ForceClear()` — clear the ghost; snap the camera home (used on set start).
  - serialized refs `CameraRig rig`, `VenueController venue`, `CrowdController crowd`, `AbilitySystem abilities`; serialized `PreviewWaypoint[] waypoints`, `Vector3 abilityCamPosition`, `Vector3 abilityCamEuler`, `float flyDuration` (all wired/filled by `PreviewSetup`).

> **Note on re-arm (vs the spec's `Confirm`):** the spec described a `Confirm()` that re-shows the next level's ghost. In this plan that re-arm is driven by `ShopView`, which simply calls `BeginUpgradePreview(def, newNextLevel)` again after a successful purchase. The waypoint is unchanged, so `CameraRig.FlyTo` to the same pose completes instantly (no visible motion) — the camera "stays put" exactly as specified. No separate `Confirm()` method is needed.

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Orchestrates the intermission upgrade ghost-preview: flies the camera to a per-kind
    /// waypoint and asks the owning system to show its ghost. Owns no ghost geometry itself —
    /// VenueController/CrowdController/AbilitySystem each render their own. Driven entirely by
    /// ShopView (no event subscriptions). Camera only moves on a new selection or an explicit
    /// ReturnHome; Cancel leaves the camera where it is.
    /// </summary>
    public sealed class UpgradePreviewController : MonoBehaviour
    {
        [System.Serializable]
        public struct PreviewWaypoint
        {
            public UpgradeKind kind;
            public Vector3 position;
            public Vector3 euler;
        }

        [SerializeField] private CameraRig rig;
        [SerializeField] private VenueController venue;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private AbilitySystem abilities;

        [Header("Camera waypoints (filled by Build Upgrade Preview, tune in play)")]
        [SerializeField] private PreviewWaypoint[] waypoints;
        [SerializeField] private Vector3 abilityCamPosition = new Vector3(0f, 9f, -11f);
        [SerializeField] private Vector3 abilityCamEuler = new Vector3(30f, 0f, 0f);
        [SerializeField] private float flyDuration = 0.6f;

        public void BeginUpgradePreview(UpgradeDefinition def, int nextLevel)
        {
            if (def == null) return;
            ClearGhost();
            ShowGhost(def, nextLevel);
            FlyToKind(def.Kind);
        }

        public void BeginAbilityPreview(AbilityDefinition def)
        {
            if (def == null) return;
            ClearGhost();
            if (abilities != null) abilities.PlayDemo(def);
            if (rig != null) rig.FlyTo(abilityCamPosition, Quaternion.Euler(abilityCamEuler), flyDuration);
        }

        /// <summary>Back out of the current preview without moving the camera.</summary>
        public void Cancel() => ClearGhost();

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

        private void OnDestroy() => ClearGhost();

        private void ShowGhost(UpgradeDefinition def, int level)
        {
            if (def.Kind == UpgradeKind.Grounds)
            {
                if (crowd != null) crowd.PreviewCapacity(def.AddCapacity);
            }
            else
            {
                if (venue != null) venue.Preview(def.Kind, level);
            }
        }

        private void ClearGhost()
        {
            if (venue != null) venue.ClearPreview();
            if (crowd != null) crowd.ClearPreview();
        }

        private void FlyToKind(UpgradeKind kind)
        {
            if (rig == null || waypoints == null) return;
            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i].kind == kind)
                {
                    rig.FlyTo(waypoints[i].position, Quaternion.Euler(waypoints[i].euler), flyDuration);
                    return;
                }
        }
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/UpgradePreviewController.cs
git commit -m "feat(unity): add UpgradePreviewController — state + camera/ghost orchestration"
```

---

### Task 6: ShopRowWidget Buy/Cancel fields

**Files:**
- Modify: `Assets/PitTycoon/Unity/UI/ShopRowWidget.cs`

**Interfaces:**
- Produces: public fields `GameObject Actions`, `Button BuyButton`, `Button CancelButton` on `ShopRowWidget` (in addition to the existing `Button`, `Icon`, `Label`, `Cost`, `Group`).

- [ ] **Step 1: Replace the file with the version below**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one shop row (an upgrade or an ability unlock). The editor builder
    /// fills these public fields on a disabled template; ShopView clones it per row. The row's
    /// Button selects/previews; the Actions sub-panel (Buy/Cancel) shows only while selected.
    /// </summary>
    public sealed class ShopRowWidget : MonoBehaviour
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Label;
        public TMP_Text Cost;
        public CanvasGroup Group;       // alpha 1 = affordable, 0.5 = greyed
        public GameObject Actions;      // Buy/Cancel container, hidden until the row is selected
        public Button BuyButton;
        public Button CancelButton;
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/UI/ShopRowWidget.cs
git commit -m "feat(unity): ShopRowWidget gains Buy/Cancel actions sub-panel refs"
```

---

### Task 7: ShopView two-step flow

**Files:**
- Modify: `Assets/PitTycoon/Unity/UI/ShopView.cs`

**Interfaces:**
- Consumes: `UpgradePreviewController.BeginUpgradePreview/BeginAbilityPreview/Cancel/ReturnHome/ForceClear` (Task 5); `ShopRowWidget.Actions/BuyButton/CancelButton` (Task 6); existing `UpgradeSystem`/`AbilitySystem`/`SetController` APIs.
- Produces: `public void Initialize(EventBus bus, EconomySystem economy, SetController set, AbilitySystem abilities, UpgradeSystem upgrades, UpgradePreviewController preview)` (new `preview` param); serialized field `Button returnHomeButton`.

- [ ] **Step 1: Replace the file with the version below**

`_preview` is null-guarded everywhere, so the shop still works (two-step Buy/Cancel, no camera/ghost) if `Build Upgrade Preview` hasn't been run yet. Selection identity is tracked by definition (not widget) so it survives the rebuild that a purchase triggers — which is exactly how the preview re-arms at the next level after a Buy.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Intermission side panel (docked right, no scene dimming). Two-step flow: clicking a row
    /// selects it (expands Buy/Cancel) and drives the UpgradePreviewController to fly the camera
    /// and show a ghost; Buy commits via the existing system APIs, Cancel backs out (camera stays).
    /// A Return Home button flies the camera back to the overview. Rows rebuild on show and on
    /// UpgradePurchased/AbilityUnlocked; selection is re-applied after a rebuild so the preview
    /// re-arms at the new level.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private TMP_Text bankedText;
        [SerializeField] private RectTransform upgradeContainer;
        [SerializeField] private RectTransform abilityContainer;
        [SerializeField] private ShopRowWidget rowTemplate;
        [SerializeField] private Button startNextSetButton;
        [SerializeField] private Button returnHomeButton;

        private EventBus _bus;
        private EconomySystem _economy;
        private SetController _set;
        private AbilitySystem _abilities;
        private UpgradeSystem _upgrades;
        private UpgradePreviewController _preview;

        private readonly List<ShopRowWidget> _rows = new List<ShopRowWidget>();
        private readonly Dictionary<ShopRowWidget, UpgradeDefinition> _rowUpgrade = new Dictionary<ShopRowWidget, UpgradeDefinition>();
        private readonly Dictionary<ShopRowWidget, AbilityDefinition> _rowAbility = new Dictionary<ShopRowWidget, AbilityDefinition>();
        private ShopRowWidget _selected;
        private UpgradeDefinition _selectedUpgrade;
        private AbilityDefinition _selectedAbility;

        public void Initialize(EventBus bus, EconomySystem economy, SetController set,
                               AbilitySystem abilities, UpgradeSystem upgrades,
                               UpgradePreviewController preview)
        {
            _bus = bus; _economy = economy; _set = set; _abilities = abilities;
            _upgrades = upgrades; _preview = preview;

            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (startNextSetButton != null) startNextSetButton.onClick.AddListener(OnStartNextSet);
            if (returnHomeButton != null) returnHomeButton.onClick.AddListener(OnReturnHome);

            _bus.Subscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Subscribe<AbilityUnlocked>(OnAbilityUnlocked);
        }

        private void OnDestroy()
        {
            if (startNextSetButton != null) startNextSetButton.onClick.RemoveListener(OnStartNextSet);
            if (returnHomeButton != null) returnHomeButton.onClick.RemoveListener(OnReturnHome);
            if (_bus == null) return;
            _bus.Unsubscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Unsubscribe<AbilityUnlocked>(OnAbilityUnlocked);
        }

        public void Show(int bankedAmount)
        {
            gameObject.SetActive(true);
            if (bankedText != null) bankedText.text = bankedAmount > 0 ? $"banked +${bankedAmount}" : string.Empty;
            ClearSelectionState();
            Rebuild();
        }

        public void Hide()
        {
            ClearSelectionState();
            gameObject.SetActive(false);
        }

        private void OnStartNextSet() { if (_set != null) _set.StartNextSet(); }   // HudController ForceClears on SetStarted
        private void OnReturnHome() { ClearSelectionState(); _preview?.ReturnHome(); }
        private void OnUpgradePurchased(UpgradePurchased e) => RefreshIfVisible();
        private void OnAbilityUnlocked(AbilityUnlocked e) => RefreshIfVisible();
        private void RefreshIfVisible() { if (gameObject.activeSelf) Rebuild(); }

        private void ClearSelectionState()
        {
            if (_selected != null && _selected.Actions != null) _selected.Actions.SetActive(false);
            _selected = null; _selectedUpgrade = null; _selectedAbility = null;
        }

        private void Rebuild()
        {
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();
            _rowUpgrade.Clear();
            _rowAbility.Clear();

            if (cashText != null && _economy != null) cashText.text = $"${_economy.Cash}";

            if (_upgrades != null && upgradeContainer != null && rowTemplate != null)
            {
                foreach (var u in _upgrades.Upgrades)
                {
                    int cost = _upgrades.CurrentCost(u);
                    int lvl = _upgrades.LevelOf(u);
                    bool afford = _upgrades.CanAfford(u);
                    ShopRowWidget row = MakeRow(upgradeContainer, $"{u.DisplayName}  Lv{lvl}", $"${cost}", afford);
                    _rowUpgrade[row] = u;
                    UpgradeDefinition captured = u;
                    if (row.Button != null) row.Button.onClick.AddListener(() => SelectUpgrade(row, captured));
                    if (row.BuyButton != null) row.BuyButton.onClick.AddListener(() => BuyUpgrade(captured));
                    if (row.CancelButton != null) row.CancelButton.onClick.AddListener(OnCancelButton);
                }
            }

            if (_abilities != null && abilityContainer != null && rowTemplate != null)
            {
                foreach (var a in _abilities.Abilities)
                {
                    if (a.Owned) continue;
                    AbilityDefinition def = _abilities.DefinitionOf(a);
                    bool afford = _abilities.CanAfford(def);
                    ShopRowWidget row = MakeRow(abilityContainer, def.DisplayName, $"${def.Cost}", afford);
                    _rowAbility[row] = def;
                    AbilityDefinition captured = def;
                    if (row.Button != null) row.Button.onClick.AddListener(() => SelectAbility(row, captured));
                    if (row.BuyButton != null) row.BuyButton.onClick.AddListener(() => BuyAbility(captured));
                    if (row.CancelButton != null) row.CancelButton.onClick.AddListener(OnCancelButton);
                }
            }

            ReapplySelection();
        }

        private ShopRowWidget MakeRow(RectTransform parent, string label, string cost, bool afford)
        {
            ShopRowWidget row = Instantiate(rowTemplate, parent);
            row.gameObject.SetActive(true);
            if (row.Label != null) row.Label.text = label;
            if (row.Cost != null) row.Cost.text = cost;
            if (row.Group != null) row.Group.alpha = afford ? 1f : 0.5f;
            if (row.Actions != null) row.Actions.SetActive(false);
            _rows.Add(row);
            return row;
        }

        private void SelectUpgrade(ShopRowWidget row, UpgradeDefinition def)
        {
            SetSelected(row);
            _selectedUpgrade = def; _selectedAbility = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _upgrades.CanAfford(def);
            _preview?.BeginUpgradePreview(def, _upgrades.LevelOf(def) + 1);
        }

        private void SelectAbility(ShopRowWidget row, AbilityDefinition def)
        {
            SetSelected(row);
            _selectedAbility = def; _selectedUpgrade = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _abilities.CanAfford(def);
            _preview?.BeginAbilityPreview(def);
        }

        private void SetSelected(ShopRowWidget row)
        {
            if (_selected != null && _selected != row && _selected.Actions != null) _selected.Actions.SetActive(false);
            _selected = row;
            if (row.Actions != null) row.Actions.SetActive(true);
        }

        private void BuyUpgrade(UpgradeDefinition def)
        {
            // TryPurchase publishes UpgradePurchased -> Rebuild -> ReapplySelection re-arms at the new level.
            _upgrades?.TryPurchase(def);
        }

        private void BuyAbility(AbilityDefinition def)
        {
            // TryUnlock publishes AbilityUnlocked -> Rebuild; the now-owned ability drops from the
            // list, so ReapplySelection clears the selection and the preview.
            _abilities?.TryUnlock(def);
        }

        private void OnCancelButton()
        {
            ClearSelectionState();
            _preview?.Cancel();
        }

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
        }

        private static ShopRowWidget FindRow<T>(Dictionary<ShopRowWidget, T> map, T value) where T : Object
        {
            foreach (var kv in map) if (kv.Value == value) return kv.Key;
            return null;
        }
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/UI/ShopView.cs
git commit -m "feat(unity): ShopView select->preview->Buy/Cancel flow + Return Home"
```

---

### Task 8: HudController + GameBootstrap wiring

**Files:**
- Modify: `Assets/PitTycoon/Unity/UI/HudController.cs`
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

**Interfaces:**
- Consumes: `ShopView.Initialize(..., UpgradePreviewController)` (Task 7); `UpgradePreviewController.ForceClear` (Task 5).
- Produces: `HudController.Initialize(EventBus, HypeSystem, EconomySystem, SetController, AbilitySystem, UpgradeSystem, UpgradePreviewController)` (new last param). `GameBootstrap` gains an optional serialized `UpgradePreviewController preview` passed into `hud.Initialize`.

- [ ] **Step 1: Replace `HudController.cs` with the version below**

Adds the `preview` param, forwards it to `ShopView`, and force-clears the preview (snap camera home + clear ghost) whenever a set starts.

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Top-level HUD root. Initializes the two views, then toggles Live <-> Shop on the set
    /// lifecycle: SetStarted shows the live HUD (and force-clears any open upgrade preview so a
    /// set never begins mid-ghost), SetEnded shows the intermission shop with the banked amount.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private LiveHudView liveView;
        [SerializeField] private ShopView shopView;

        private EventBus _bus;
        private UpgradePreviewController _preview;

        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities, UpgradeSystem upgrades,
                               UpgradePreviewController preview)
        {
            _bus = bus;
            _preview = preview;

            if (liveView != null) liveView.Initialize(bus, hype, economy, set, abilities);
            if (shopView != null) shopView.Initialize(bus, economy, set, abilities, upgrades, preview);

            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);

            // Hidden until the first SetStarted (SetController.Start fires after all Awakes).
            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Hide();
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
        }

        private void OnSetStarted(SetStarted e)
        {
            _preview?.ForceClear();
            if (shopView != null) shopView.Hide();
            if (liveView != null) liveView.Show();
        }

        private void OnSetEnded(SetEnded e)
        {
            if (liveView != null) liveView.Hide();
            if (shopView != null) shopView.Show(e.CashEarned);
        }
    }
}
```

- [ ] **Step 2: Add the `preview` field to `GameBootstrap`**

In `Assets/PitTycoon/Unity/GameBootstrap.cs`, after the `hud` field (line 24), add:

```csharp
        [Tooltip("Optional — wired by Build Upgrade Preview. Null = no camera/ghost preview.")]
        [SerializeField] private UpgradePreviewController preview;
```

- [ ] **Step 3: Pass `preview` into `hud.Initialize`**

In `GameBootstrap.Awake`, change the existing `hud.Initialize(...)` call (line 51) to:

```csharp
            hud.Initialize(Bus, hype, economy, setController, abilities, upgrades, preview);
```

(Do **not** add `preview` to the null-guard — it is optional so the game still runs before `Build Upgrade Preview` is run.)

- [ ] **Step 4: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Unity/UI/HudController.cs Assets/PitTycoon/Unity/GameBootstrap.cs
git commit -m "feat(unity): wire UpgradePreviewController through HudController + GameBootstrap"
```

---

### Task 9: HudSetup — Buy/Cancel row UI + Return Home button

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/HudSetup.cs`

**Interfaces:**
- Consumes: `ShopRowWidget.Actions/BuyButton/CancelButton` (Task 6); `ShopView` serialized field `returnHomeButton` (Task 7).
- Produces: the shop-row template now contains an Actions sub-panel (Buy + Cancel); the shop panel has a Return Home ("⌂ Overview") button; both wired via `SerializedObject`.

- [ ] **Step 1: Extend the shop-row template with the Actions sub-panel**

In `HudSetup.cs`, in `BuildShopRowTemplate`, immediately before `return widget;` (the last line of that method, ~line 289), insert:

```csharp
            // Actions sub-panel (Buy / Cancel) — hidden until the row is selected.
            var actions = NewUI("Actions", go.transform);
            var art = actions.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(1f, 1f);
            art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
            var alay = actions.AddComponent<HorizontalLayoutGroup>();
            alay.spacing = 6f; alay.childAlignment = TextAnchor.MiddleRight;
            alay.padding = new RectOffset(0, 6, 4, 4);
            alay.childControlWidth = false; alay.childControlHeight = false;
            alay.childForceExpandWidth = false; alay.childForceExpandHeight = false;

            var buy = NewUI("Buy", actions.transform);
            buy.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 24f);
            var buyImg = AddImage(buy, HypeOrange); AddOutline(buy);
            widget.BuyButton = buy.AddComponent<Button>(); widget.BuyButton.targetGraphic = buyImg;
            var buyText = NewUI("Text", buy.transform); Stretch(buyText.GetComponent<RectTransform>());
            AddText(buyText, "BUY", 12, TextAlignmentOptions.Center);

            var cancel = NewUI("Cancel", actions.transform);
            cancel.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 24f);
            var cancelImg = AddImage(cancel, Bar); AddOutline(cancel);
            widget.CancelButton = cancel.AddComponent<Button>(); widget.CancelButton.targetGraphic = cancelImg;
            var cancelText = NewUI("Text", cancel.transform); Stretch(cancelText.GetComponent<RectTransform>());
            AddText(cancelText, "CANCEL", 12, TextAlignmentOptions.Center);

            widget.Actions = actions;
            actions.SetActive(false);
```

- [ ] **Step 2: Add the Return Home button to the shop panel**

In `BuildShop`, immediately after the `banked` block (after the line `r.banked.color = new Color(0.62f, 0.88f, 0.8f);`, ~line 229), insert:

```csharp
            var home = NewUI("ReturnHome", go.transform);
            home.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
            var homeImg = AddImage(home, Bar); AddOutline(home);
            r.returnHome = home.AddComponent<Button>(); r.returnHome.targetGraphic = homeImg;
            var homeText = NewUI("Text", home.transform); Stretch(homeText.GetComponent<RectTransform>());
            AddText(homeText, "⌂ Overview", 12, TextAlignmentOptions.Center);
```

- [ ] **Step 3: Add `returnHome` to the `ShopRefs` struct**

In `HudSetup.cs`, in the `ShopRefs` struct definition, add a field:

```csharp
            public Button returnHome;
```

- [ ] **Step 4: Wire `returnHomeButton` in `WireShop`**

In `WireShop`, before `so.ApplyModifiedPropertiesWithoutUndo();`, add:

```csharp
            so.FindProperty("returnHomeButton").objectReferenceValue = r.returnHome;
```

- [ ] **Step 5: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 6: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/HudSetup.cs
git commit -m "feat(editor): HudSetup builds row Buy/Cancel actions + Return Home button"
```

---

### Task 10: PreviewSetup editor builder + ghost material

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/PreviewSetup.cs`

**Interfaces:**
- Consumes: `CameraRig` (Task 4), `UpgradePreviewController` + its `PreviewWaypoint` struct (Task 5), the `ghostMaterial` serialized fields on `VenueController`/`CrowdController` (Tasks 1–2), `GameBootstrap.preview` (Task 8). (ShopView receives the controller at runtime via `hud.Initialize` — it is **not** serialized, so PreviewSetup does not wire it directly.)
- Produces: menu `Pit Tycoon → Build Upgrade Preview` — creates the ghost material, adds `CameraRig` to the Main Camera and `UpgradePreviewController` to the Systems object, fills default waypoints, wires the controller's refs + the venue/crowd ghost material + `GameBootstrap.preview`. Idempotent.

- [ ] **Step 1: Create the file**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PitTycoon.Unity;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds and wires the upgrade ghost-preview (sub-project B): a ghost material, a CameraRig
    /// on the Main Camera, an UpgradePreviewController on the Systems object with default
    /// waypoints, the venue/crowd ghost-material refs, and GameBootstrap.preview (which delivers
    /// the controller to ShopView at runtime via hud.Initialize). Run AFTER Build HUD so the shop
    /// rows have Buy/Cancel. Idempotent.
    /// </summary>
    public static class PreviewSetup
    {
        private const string GhostMatPath = "Assets/PitTycoon/Art/Materials/GhostMat.mat";

        [MenuItem("Pit Tycoon/Build Upgrade Preview")]
        public static void Build()
        {
            Material ghost = LoadOrCreateGhostMaterial();

            var venue = Object.FindFirstObjectByType<VenueController>();
            var crowd = Object.FindFirstObjectByType<CrowdController>();
            var abilities = Object.FindFirstObjectByType<AbilitySystem>();
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            if (venue == null || crowd == null || abilities == null || cam == null)
            {
                Debug.LogError("PreviewSetup: missing VenueController/CrowdController/AbilitySystem/Camera. " +
                               "Run Build Greybox + Build Festival Scene first.");
                return;
            }

            // CameraRig on the Main Camera.
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();

            // UpgradePreviewController on the Systems object (where VenueController lives).
            var controller = venue.GetComponent<UpgradePreviewController>();
            if (controller == null) controller = venue.gameObject.AddComponent<UpgradePreviewController>();

            // Ghost material into venue + crowd.
            SetRef(venue, "ghostMaterial", ghost);
            SetRef(crowd, "ghostMaterial", ghost);

            // Controller refs + default waypoints.
            WireController(controller, rig, venue, crowd, abilities);

            // GameBootstrap.preview — delivered to HudController/ShopView at runtime via
            // hud.Initialize (ShopView's preview ref is not serialized). Run Build HUD first so
            // the shop rows carry Buy/Cancel.
            if (boot != null) SetRef(boot, "preview", controller);
            else Debug.LogWarning("PreviewSetup: no GameBootstrap found in scene.");

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(venue);
            EditorUtility.SetDirty(crowd);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: Upgrade Preview built. Tune camera waypoints on " +
                      "UpgradePreviewController (Systems) and ghost alpha on GhostMat.");
        }

        private static void WireController(UpgradePreviewController c, CameraRig rig,
                                           VenueController venue, CrowdController crowd, AbilitySystem abilities)
        {
            var so = new SerializedObject(c);
            so.FindProperty("rig").objectReferenceValue = rig;
            so.FindProperty("venue").objectReferenceValue = venue;
            so.FindProperty("crowd").objectReferenceValue = crowd;
            so.FindProperty("abilities").objectReferenceValue = abilities;
            so.FindProperty("flyDuration").floatValue = 0.6f;

            // Default waypoints (world poses derived from the M2b scene; tune in play).
            // Stage z=9, PA at x=+/-6 z=9, truss lights y=4 z=9, crowd in front of stage.
            var wps = so.FindProperty("waypoints");
            wps.arraySize = 4;
            SetWaypoint(wps.GetArrayElementAtIndex(0), (int)UpgradeKind.Stage,    new Vector3(0f, 6f, 2f),   new Vector3(20f, 0f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(1), (int)UpgradeKind.PA,       new Vector3(4f, 4f, 3f),   new Vector3(16f, -22f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(2), (int)UpgradeKind.Lighting, new Vector3(0f, 5f, 2f),   new Vector3(-4f, 0f, 0f));
            SetWaypoint(wps.GetArrayElementAtIndex(3), (int)UpgradeKind.Grounds,  new Vector3(0f, 7f, -10f), new Vector3(28f, 0f, 0f));

            so.FindProperty("abilityCamPosition").vector3Value = new Vector3(0f, 9f, -11f);
            so.FindProperty("abilityCamEuler").vector3Value = new Vector3(30f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWaypoint(SerializedProperty wp, int kind, Vector3 pos, Vector3 euler)
        {
            wp.FindPropertyRelative("kind").enumValueIndex = kind;
            wp.FindPropertyRelative("position").vector3Value = pos;
            wp.FindPropertyRelative("euler").vector3Value = euler;
        }

        private static Material LoadOrCreateGhostMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
            if (existing != null) return existing;

            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh) { name = "GhostMat" };
            m.SetColor("_BaseColor", new Color(0.45f, 0.85f, 1f, 0.32f));
            m.SetFloat("_Surface", 1f);                       // 0 opaque, 1 transparent
            m.SetFloat("_Blend", 0f);                         // alpha blend
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, GhostMatPath);
            AssetDatabase.SaveAssets();
            return m;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"PreviewSetup: field '{field}' not found on {target.GetType().Name}.");
        }
    }
}
```

- [ ] **Step 2: Run the Domain regression guard**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: `Passed!  - Failed:     0, Passed:    73`

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/PreviewSetup.cs
git commit -m "feat(editor): add PreviewSetup — builds + wires camera rig, ghost mat, controller"
```

---

### Task 11: SETUP.md section

**Files:**
- Modify: `SETUP.md`

**Interfaces:** none (docs).

- [ ] **Step 1: Append the section below to `SETUP.md`** (after the "UI foundation" section, at the end of the file)

```markdown
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
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP upgrade ghost-preview — build steps, verification, tuning"
```

---

### Task 12: Manual Unity checkpoint (needs the dev)

**Files:** none (in-Editor verification by the dev).

This is the first real compile of the new C# and the only functional verification. The implementing agent cannot do this — hand it to the dev with the checklist below.

- [ ] **Step 1: Open the project; let Unity recompile.** Watch the Console — expect **no errors** (new scripts + preview hooks + editor builders compile).

- [ ] **Step 2: Run the builders in order.** `Pit Tycoon → Build HUD`, then `Pit Tycoon → Build Upgrade Preview`. Expect the success logs; no warnings about missing ShopView/GameBootstrap.

- [ ] **Step 3: Commit the generated `.meta` files** Unity creates for the new scripts + `GhostMat.mat`, plus the modified `Greybox.unity`:

```bash
git add "Assets/PitTycoon/Unity/CameraRig.cs.meta" \
        "Assets/PitTycoon/Unity/UpgradePreviewController.cs.meta" \
        "Assets/PitTycoon/Unity/Editor/PreviewSetup.cs.meta" \
        "Assets/PitTycoon/Art/Materials/GhostMat.mat" "Assets/PitTycoon/Art/Materials/GhostMat.mat.meta" \
        "Assets/Scenes/Greybox.unity"
git commit -m "chore(unity): meta files + built scene for upgrade ghost-preview"
```

- [ ] **Step 4: Play-test the checklist:**
  - [ ] Reach intermission — camera at the wide overview; shop docked right, scene visible (no dimming).
  - [ ] Click **Stage** → camera flies head-on to the stage; a translucent **bigger stage** ghost appears; row shows BUY/CANCEL.
  - [ ] Click **PA**, **Lighting**, **Grounds** → camera flies to each; PA shows bigger ghost stacks, Lighting brightens, Grounds shows ghost figures in new back rows.
  - [ ] **Buy** an upgrade → ghost becomes real, row bumps Lv/cost, preview re-arms for the next level, camera stays.
  - [ ] **Cancel** → ghost reverts, camera stays put.
  - [ ] **⌂ Overview** → camera returns home.
  - [ ] An **ability** row → one-shot VFX demo fires; **Buy** unlocks it (row disappears, preview clears).
  - [ ] **Start Next Set** → camera snaps home, set starts; no lingering ghost.
  - [ ] **F1** dev overlay toggles; M1/M2/M3 (abilities, VFX, coins, venue scaling, crowd fill) intact.
  - [ ] `dotnet test PitTycoon.Domain.slnx` → 73 passed.

- [ ] **Step 5: Report results.** Note anything off (framing, ghost opacity, fly speed) for a tuning pass; otherwise the branch is ready for a PR.

---

## Notes

- **Build order matters:** `Build HUD` before `Build Upgrade Preview` (the latter wires `ShopView.preview`). Documented in SETUP.
- **Graceful degradation:** `_preview` is null-guarded throughout `ShopView`, so before `Build Upgrade Preview` is run the shop still works as a two-step Buy/Cancel without camera/ghost — the game never hard-breaks on a missing preview controller.
- **Re-arm:** after a Buy, `ShopView.Rebuild` → `ReapplySelection` re-selects the same upgrade, calling `BeginUpgradePreview(def, newLevel)`; the camera flies to the same (unchanged) waypoint, so it visually stays put while the ghost advances to the next level.
