# UI Foundation (sub-project A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the IMGUI `DebugHud` player UI with a UGUI + TextMeshPro live-set HUD and intermission shop, built by an editor script and bound to the existing systems.

**Architecture:** Three runtime view MonoBehaviours (`HudController` toggles `LiveHudView` ↔ `ShopView`) under `Assets/PitTycoon/Unity/UI/`, plus two tiny widget ref-holders cloned at runtime. A `HudSetup` editor script constructs and wires the entire canvas. `GameBootstrap` initializes the UI alongside the other systems. `DebugHud` is stripped to an F1 dev overlay. No new Domain logic.

**Tech Stack:** Unity 6 LTS / URP, C#; UGUI (`com.unity.ugui` 2.0) + TextMeshPro; new Input System. Domain tested with `dotnet test PitTycoon.Domain.slnx` (regression guard only — no new unit tests this milestone).

## Global Constraints

- Commit messages: imperative present tense, **no `Co-Authored-By` trailer** (hard project rule).
- This project is **new-Input-System-only**: UGUI `EventSystem` must use `InputSystemUIInputModule`, never `StandaloneInputModule`.
- Systems communicate via `EventBus` / public read APIs — views never reach into another system's internals. Only the analyzer touches `AudioSource`; beat feedback consumes `BeatDetected` on the bus.
- **No automated tests for the UGUI layer** (it is not compiled by `dotnet test`). Each code task is verified by a self-check against the spec; `dotnet test` is run as a guard to confirm the **73** Domain tests stay green; real correctness is the manual Unity checkpoint in Task 10.
- New runtime UI code lives in namespace `PitTycoon.Unity.UI`; widget/view files each carry one MonoBehaviour whose class name matches the file name (Unity requirement).

**Existing system APIs the views consume (verified against source — do not modify):**
- `HypeSystem` (MonoBehaviour): `float Current`, `float Ceiling`, `float Peak`, `float HypeFraction`.
- `EconomySystem`: `int Cash`.
- `SetController`: `enum Phase { Live, Intermission }`, `Phase Current`, `int SetNumber`, `int LastCashEarned`, `void StartNextSet()`.
- `AbilitySystem`: `IReadOnlyList<Ability> Abilities`, `AbilityDefinition DefinitionOf(Ability)`, `HitQuality LastQuality`, `bool CanAfford(AbilityDefinition)`, `bool TryFire(Ability)`, `bool TryUnlock(AbilityDefinition)`.
- `Ability` (Domain): `bool Owned`, `bool CanFire`, `float CooldownRemaining`.
- `AbilityDefinition`: `string DisplayName`, `int Cost`, `Color HudColor`, `float Cooldown`, `AbilityTrigger Trigger` (`Spacebar`/`Button`).
- `UpgradeSystem`: `IReadOnlyList<UpgradeDefinition> Upgrades`, `int LevelOf(u)`, `int CurrentCost(u)`, `bool CanAfford(u)`, `bool TryPurchase(u)`.
- `UpgradeDefinition`: `string DisplayName`, `UpgradeKind Kind`.
- `EventBus`: `Subscribe<T>(Action<T>)`, `Unsubscribe<T>(Action<T>)`. Events: `SetStarted{int SetNumber}`, `SetEnded{float PeakHype, float AvgHype, int CashEarned}`, `AbilityFired{...}`, `AbilityUnlocked{...}`, `UpgradePurchased{...}`, `BeatDetected{BeatInfo Beat}` (`Beat.Strength`).
- `HitQuality` (Domain): enum incl. `None`, `Perfect`, `Good`.

---

## File Structure

**New (runtime, `Assets/PitTycoon/Unity/UI/`):**
- `AbilityButtonWidget.cs` — ref-holder for one ability button (button, bg, label, hotkey, cooldown overlay).
- `ShopRowWidget.cs` — ref-holder for one shop row (button, icon, label, cost, canvas group).
- `LiveHudView.cs` — live-set HUD (hype bar + peak marker, cash, set, ability buttons, hit-quality popup, beat pulse).
- `ShopView.cs` — intermission side panel (cash, banked callout, upgrade rows, ability-unlock rows, start button).
- `HudController.cs` — top-level; toggles the two views on `SetStarted`/`SetEnded`.

**New (editor):**
- `Assets/PitTycoon/Unity/Editor/HudSetup.cs` — `Pit Tycoon → Build HUD` canvas builder + wiring.

**Modified:**
- `Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef` — add `UnityEngine.UI`, `Unity.TextMeshPro`.
- `Assets/PitTycoon/Unity/Editor/PitTycoon.Unity.Editor.asmdef` — add `UnityEngine.UI`, `Unity.TextMeshPro`, `Unity.InputSystem`.
- `Assets/PitTycoon/Unity/GameBootstrap.cs` — `hud` ref + `Initialize` call.
- `Assets/PitTycoon/Unity/DebugHud.cs` — strip to dev overlay + F1 toggle.
- `SETUP.md` — Build HUD step, TMP-essentials note, verification checklist.

---

## Task 1: Assembly references

**Files:**
- Modify: `Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef`
- Modify: `Assets/PitTycoon/Unity/Editor/PitTycoon.Unity.Editor.asmdef`

**Interfaces:**
- Produces: the `UnityEngine.UI` + `TMPro` types become resolvable in both the runtime and editor assemblies; `InputSystemUIInputModule` resolvable in the editor assembly.

- [ ] **Step 1: Add UI/TMP references to the runtime asmdef**

Replace the `references` array in `Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef` with:

```json
    "references": [
        "PitTycoon.Domain",
        "Unity.InputSystem",
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
```

- [ ] **Step 2: Add UI/TMP/InputSystem references to the editor asmdef**

Replace the `references` array in `Assets/PitTycoon/Unity/Editor/PitTycoon.Unity.Editor.asmdef` with:

```json
    "references": [
        "PitTycoon.Unity",
        "PitTycoon.Domain",
        "Unity.RenderPipelines.Universal.Runtime",
        "Unity.RenderPipelines.Core.Runtime",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "Unity.InputSystem"
    ],
```

- [ ] **Step 3: Self-check**

Confirm both files are valid JSON (arrays comma-correct, no trailing commas) and each name string is spelled exactly as above. These assembly names are the canonical Unity package assembly names; a typo silently drops the reference and the UI code fails to compile at Task 10.

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/PitTycoon.Unity.asmdef" "Assets/PitTycoon/Unity/Editor/PitTycoon.Unity.Editor.asmdef"
git commit -m "build(unity): reference UnityEngine.UI/TextMeshPro (+InputSystem in editor) for UGUI HUD"
```

---

## Task 2: Widget ref-holders

**Files:**
- Create: `Assets/PitTycoon/Unity/UI/AbilityButtonWidget.cs`
- Create: `Assets/PitTycoon/Unity/UI/ShopRowWidget.cs`

**Interfaces:**
- Produces: `AbilityButtonWidget { Button Button; Image Background; TMP_Text Label; TMP_Text Hotkey; Image CooldownOverlay; }` and `ShopRowWidget { Button Button; Image Icon; TMP_Text Label; TMP_Text Cost; CanvasGroup Group; }` — both with **public** fields (assigned directly by the editor builder; cloned at runtime by the views).

- [ ] **Step 1: Create `AbilityButtonWidget`**

Create `Assets/PitTycoon/Unity/UI/AbilityButtonWidget.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one ability button instance. The editor builder fills these
    /// public fields on a disabled template; LiveHudView clones the template per owned ability.
    /// </summary>
    public sealed class AbilityButtonWidget : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public TMP_Text Hotkey;
        public Image CooldownOverlay;   // Image.type = Filled; fillAmount: 1 = just fired, 0 = ready
    }
}
```

- [ ] **Step 2: Create `ShopRowWidget`**

Create `Assets/PitTycoon/Unity/UI/ShopRowWidget.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Plain ref-holder for one shop row (an upgrade or an ability unlock). The editor builder
    /// fills these public fields on a disabled template; ShopView clones it per row.
    /// </summary>
    public sealed class ShopRowWidget : MonoBehaviour
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Label;
        public TMP_Text Cost;
        public CanvasGroup Group;       // alpha 1 = affordable, 0.5 = greyed
    }
}
```

- [ ] **Step 3: Self-check**

Each file: one `public sealed class` whose name matches the file name, namespace `PitTycoon.Unity.UI`, all fields public. No logic.

- [ ] **Step 4: Commit**

```bash
git add "Assets/PitTycoon/Unity/UI/AbilityButtonWidget.cs" "Assets/PitTycoon/Unity/UI/ShopRowWidget.cs"
git commit -m "feat(unity): add AbilityButtonWidget/ShopRowWidget UI ref-holders"
```

---

## Task 3: `LiveHudView`

**Files:**
- Create: `Assets/PitTycoon/Unity/UI/LiveHudView.cs`

**Interfaces:**
- Consumes: `AbilityButtonWidget` (Task 2); the system APIs in Global Constraints.
- Produces: `void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy, SetController set, AbilitySystem abilities)`, `void Show()`, `void Hide()`. Serialized fields (wired by Task 8): `hypeFill`, `peakMarker`, `hypeText`, `cashText`, `setText`, `abilityBar`, `abilityButtonTemplate`, `hitQualityText`, `beatPulse`.

- [ ] **Step 1: Create the file**

Create `Assets/PitTycoon/Unity/UI/LiveHudView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Live-set HUD: top-center hype bar (fill + peak marker + cur/ceiling), cash, set/phase,
    /// one ability button per owned ability (cooldown overlay + hotkey), a hit-quality popup
    /// and a beat pulse. Reads system state each frame; reacts to SetStarted/AbilityFired/
    /// BeatDetected on the bus. Pure presentation — no game logic.
    /// </summary>
    public sealed class LiveHudView : MonoBehaviour
    {
        [Header("Hype")]
        [SerializeField] private Image hypeFill;            // type = Filled, horizontal
        [SerializeField] private RectTransform peakMarker;  // parented to the hype bar area
        [SerializeField] private TMP_Text hypeText;
        [Header("Readouts")]
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private TMP_Text setText;
        [Header("Abilities")]
        [SerializeField] private RectTransform abilityBar;
        [SerializeField] private AbilityButtonWidget abilityButtonTemplate;
        [Header("Feedback")]
        [SerializeField] private TMP_Text hitQualityText;
        [SerializeField] private Graphic beatPulse;
        [SerializeField] private float hitQualityHold = 0.8f;
        [SerializeField] private float beatPulseDecay = 4f;

        private EventBus _bus;
        private HypeSystem _hype;
        private EconomySystem _economy;
        private SetController _set;
        private AbilitySystem _abilities;

        private readonly List<AbilityButtonWidget> _buttons = new List<AbilityButtonWidget>();
        private float _hitTimer;
        private float _pulse;

        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities)
        {
            _bus = bus; _hype = hype; _economy = economy; _set = set; _abilities = abilities;

            if (abilityButtonTemplate != null) abilityButtonTemplate.gameObject.SetActive(false);
            if (hitQualityText != null) hitQualityText.gameObject.SetActive(false);

            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
            _bus.Subscribe<BeatDetected>(OnBeat);

            BuildAbilityButtons();
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
            _bus.Unsubscribe<BeatDetected>(OnBeat);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        private void OnSetStarted(SetStarted e) => BuildAbilityButtons();

        private void OnAbilityFired(AbilityFired e)
        {
            if (hitQualityText == null || _abilities == null) return;
            HitQuality q = _abilities.LastQuality;
            if (q == HitQuality.None) return;
            hitQualityText.text = q.ToString().ToUpperInvariant() + "!";
            hitQualityText.color = q switch
            {
                HitQuality.Perfect => new Color(0.3f, 1f, 0.4f),
                HitQuality.Good => new Color(1f, 0.9f, 0.3f),
                _ => new Color(0.8f, 0.8f, 0.8f),
            };
            hitQualityText.gameObject.SetActive(true);
            _hitTimer = hitQualityHold;
        }

        private void OnBeat(BeatDetected e) =>
            _pulse = Mathf.Max(_pulse, Mathf.Clamp01(0.4f + e.Beat.Strength));

        /// <summary>Clear and rebuild one button per owned ability (front of the bar = hotkey 1).</summary>
        private void BuildAbilityButtons()
        {
            if (_abilities == null || abilityButtonTemplate == null || abilityBar == null) return;

            foreach (var w in _buttons) if (w != null) Destroy(w.gameObject);
            _buttons.Clear();

            int hotkey = 1;
            foreach (var a in _abilities.Abilities)
            {
                if (!a.Owned) continue;
                AbilityDefinition def = _abilities.DefinitionOf(a);

                AbilityButtonWidget w = Instantiate(abilityButtonTemplate, abilityBar);
                w.gameObject.SetActive(true);
                if (w.Background != null) w.Background.color = def.HudColor;
                if (w.Label != null) w.Label.text = def.DisplayName;
                if (w.Hotkey != null)
                    w.Hotkey.text = def.Trigger == AbilityTrigger.Spacebar ? "Space" : hotkey.ToString();
                if (w.CooldownOverlay != null) w.CooldownOverlay.fillAmount = 0f;

                Ability captured = a;
                if (w.Button != null) w.Button.onClick.AddListener(() => _abilities.TryFire(captured));

                _buttons.Add(w);
                if (def.Trigger == AbilityTrigger.Button) hotkey++;
            }
        }

        private void Update()
        {
            if (_hype != null && hypeFill != null)
            {
                hypeFill.fillAmount = Mathf.Clamp01(_hype.HypeFraction);
                if (hypeText != null) hypeText.text = $"{_hype.Current:0} / {_hype.Ceiling:0}";
                if (peakMarker != null && _hype.Ceiling > 0f)
                {
                    float frac = Mathf.Clamp01(_hype.Peak / _hype.Ceiling);
                    peakMarker.anchorMin = new Vector2(frac, peakMarker.anchorMin.y);
                    peakMarker.anchorMax = new Vector2(frac, peakMarker.anchorMax.y);
                    peakMarker.anchoredPosition = new Vector2(0f, peakMarker.anchoredPosition.y);
                }
            }

            if (_economy != null && cashText != null) cashText.text = $"${_economy.Cash}";
            if (_set != null && setText != null) setText.text = $"SET {_set.SetNumber}";

            if (_abilities != null)
            {
                int i = 0;
                foreach (var a in _abilities.Abilities)
                {
                    if (!a.Owned) continue;
                    if (i >= _buttons.Count) break;
                    AbilityButtonWidget w = _buttons[i++];
                    if (w == null) continue;
                    AbilityDefinition def = _abilities.DefinitionOf(a);
                    if (w.CooldownOverlay != null)
                        w.CooldownOverlay.fillAmount =
                            def.Cooldown > 0f ? Mathf.Clamp01(a.CooldownRemaining / def.Cooldown) : 0f;
                    if (w.Button != null) w.Button.interactable = a.CanFire;
                }
            }

            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                if (_hitTimer <= 0f && hitQualityText != null) hitQualityText.gameObject.SetActive(false);
            }

            if (_pulse > 0f)
            {
                _pulse = Mathf.MoveTowards(_pulse, 0f, beatPulseDecay * Time.deltaTime);
                if (beatPulse != null) { Color c = beatPulse.color; c.a = _pulse; beatPulse.color = c; }
            }
        }
    }
}
```

- [ ] **Step 2: Self-check**

Confirm: `Initialize` subscribes 3 bus events and `OnDestroy` unsubscribes the same 3; ability buttons are cloned from the template (template set inactive); `Update` pairs the i-th owned ability to `_buttons[i]` (same order as `BuildAbilityButtons`); peak marker repositions by `Peak/Ceiling`; all field accesses are null-guarded.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/UI/LiveHudView.cs"
git commit -m "feat(unity): add LiveHudView — hype bar, cash, ability buttons, hit-quality, beat pulse"
```

---

## Task 4: `ShopView`

**Files:**
- Create: `Assets/PitTycoon/Unity/UI/ShopView.cs`

**Interfaces:**
- Consumes: `ShopRowWidget` (Task 2); the system APIs in Global Constraints.
- Produces: `void Initialize(EventBus bus, EconomySystem economy, SetController set, AbilitySystem abilities, UpgradeSystem upgrades)`, `void Show(int bankedAmount)`, `void Hide()`. Serialized fields (wired by Task 8): `cashText`, `bankedText`, `upgradeContainer`, `abilityContainer`, `rowTemplate`, `startNextSetButton`.

- [ ] **Step 1: Create the file**

Create `Assets/PitTycoon/Unity/UI/ShopView.cs`:

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
    /// Intermission side panel (docked right, no scene dimming): cash, banked-this-set callout,
    /// a row per upgrade (name/level/cost, greyed when unaffordable), a row per un-owned ability,
    /// and Start Next Set. Rebuilds rows on show and on UpgradePurchased/AbilityUnlocked.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private TMP_Text bankedText;
        [SerializeField] private RectTransform upgradeContainer;
        [SerializeField] private RectTransform abilityContainer;
        [SerializeField] private ShopRowWidget rowTemplate;
        [SerializeField] private Button startNextSetButton;

        private EventBus _bus;
        private EconomySystem _economy;
        private SetController _set;
        private AbilitySystem _abilities;
        private UpgradeSystem _upgrades;

        private readonly List<ShopRowWidget> _rows = new List<ShopRowWidget>();

        public void Initialize(EventBus bus, EconomySystem economy, SetController set,
                               AbilitySystem abilities, UpgradeSystem upgrades)
        {
            _bus = bus; _economy = economy; _set = set; _abilities = abilities; _upgrades = upgrades;

            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (startNextSetButton != null) startNextSetButton.onClick.AddListener(OnStartNextSet);

            _bus.Subscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Subscribe<AbilityUnlocked>(OnAbilityUnlocked);
        }

        private void OnDestroy()
        {
            if (startNextSetButton != null) startNextSetButton.onClick.RemoveListener(OnStartNextSet);
            if (_bus == null) return;
            _bus.Unsubscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Unsubscribe<AbilityUnlocked>(OnAbilityUnlocked);
        }

        public void Show(int bankedAmount)
        {
            gameObject.SetActive(true);
            if (bankedText != null) bankedText.text = bankedAmount > 0 ? $"banked +${bankedAmount}" : string.Empty;
            Rebuild();
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnStartNextSet() { if (_set != null) _set.StartNextSet(); }
        private void OnUpgradePurchased(UpgradePurchased e) => RefreshIfVisible();
        private void OnAbilityUnlocked(AbilityUnlocked e) => RefreshIfVisible();
        private void RefreshIfVisible() { if (gameObject.activeSelf) Rebuild(); }

        private void Rebuild()
        {
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();

            if (cashText != null && _economy != null) cashText.text = $"${_economy.Cash}";

            if (_upgrades != null && upgradeContainer != null && rowTemplate != null)
            {
                foreach (var u in _upgrades.Upgrades)
                {
                    int cost = _upgrades.CurrentCost(u);
                    int lvl = _upgrades.LevelOf(u);
                    bool afford = _upgrades.CanAfford(u);
                    UpgradeDefinition captured = u;
                    MakeRow(upgradeContainer, $"{u.DisplayName}  Lv{lvl}", $"${cost}", afford,
                            () => _upgrades.TryPurchase(captured));
                }
            }

            if (_abilities != null && abilityContainer != null && rowTemplate != null)
            {
                foreach (var a in _abilities.Abilities)
                {
                    if (a.Owned) continue;
                    AbilityDefinition def = _abilities.DefinitionOf(a);
                    bool afford = _abilities.CanAfford(def);
                    AbilityDefinition captured = def;
                    MakeRow(abilityContainer, def.DisplayName, $"${def.Cost}", afford,
                            () => _abilities.TryUnlock(captured));
                }
            }
        }

        private void MakeRow(RectTransform parent, string label, string cost, bool afford, UnityAction onClick)
        {
            ShopRowWidget row = Instantiate(rowTemplate, parent);
            row.gameObject.SetActive(true);
            if (row.Label != null) row.Label.text = label;
            if (row.Cost != null) row.Cost.text = cost;
            if (row.Group != null) row.Group.alpha = afford ? 1f : 0.5f;
            if (row.Button != null) { row.Button.interactable = afford; row.Button.onClick.AddListener(onClick); }
            _rows.Add(row);
        }
    }
}
```

- [ ] **Step 2: Self-check**

Confirm: `Initialize` subscribes `UpgradePurchased`/`AbilityUnlocked` and adds the start-button listener; `OnDestroy` removes both subs + the listener; `Rebuild` clears old rows, lists all upgrades then un-owned abilities, greys unaffordable via `Group.alpha` + `Button.interactable`; purchase callbacks re-fire `Rebuild` through the bus events. No dimming/overlay behavior anywhere (panel is a side dock).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/UI/ShopView.cs"
git commit -m "feat(unity): add ShopView — intermission side panel with upgrade/ability rows"
```

---

## Task 5: `HudController`

**Files:**
- Create: `Assets/PitTycoon/Unity/UI/HudController.cs`

**Interfaces:**
- Consumes: `LiveHudView` (Task 3), `ShopView` (Task 4).
- Produces: `void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy, SetController set, AbilitySystem abilities, UpgradeSystem upgrades)`. Serialized fields (wired by Task 8): `liveView`, `shopView`. **This exact signature is what `GameBootstrap` (Task 6) calls.**

- [ ] **Step 1: Create the file**

Create `Assets/PitTycoon/Unity/UI/HudController.cs`:

```csharp
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Top-level HUD root. Initializes the two views, then toggles Live ↔ Shop on the set
    /// lifecycle: SetStarted shows the live HUD, SetEnded shows the intermission shop with the
    /// banked amount. Owns no widget logic itself.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private LiveHudView liveView;
        [SerializeField] private ShopView shopView;

        private EventBus _bus;

        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities, UpgradeSystem upgrades)
        {
            _bus = bus;

            if (liveView != null) liveView.Initialize(bus, hype, economy, set, abilities);
            if (shopView != null) shopView.Initialize(bus, economy, set, abilities, upgrades);

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

- [ ] **Step 2: Self-check**

Confirm: `Initialize` initializes both views, subscribes `SetStarted`/`SetEnded`, hides both initially; `OnDestroy` unsubscribes both; `OnSetEnded` passes `e.CashEarned` to `shopView.Show`. Signature matches the Produces block exactly (Task 6 depends on it).

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/UI/HudController.cs"
git commit -m "feat(unity): add HudController toggling live HUD and intermission shop"
```

---

## Task 6: `GameBootstrap` wiring

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

**Interfaces:**
- Consumes: `HudController.Initialize(EventBus, HypeSystem, EconomySystem, SetController, AbilitySystem, UpgradeSystem)` (Task 5).
- Produces: a serialized `HudController hud` field that Task 8 sets.

- [ ] **Step 1: Add the field**

In `Assets/PitTycoon/Unity/GameBootstrap.cs`, add a `using` and a serialized field. After the existing `using PitTycoon.Domain;` line add:

```csharp
using PitTycoon.Unity.UI;
```

After the `[SerializeField] private BeatVfxController beatVfx;` line add:

```csharp
        [SerializeField] private HudController hud;
```

- [ ] **Step 2: Add `hud` to the null-guard**

Replace the guard condition:

```csharp
            if (analyzer == null || crowd == null || hype == null || abilities == null
                || economy == null || upgrades == null || setController == null || beatVfx == null)
```

with:

```csharp
            if (analyzer == null || crowd == null || hype == null || abilities == null
                || economy == null || upgrades == null || setController == null || beatVfx == null
                || hud == null)
```

- [ ] **Step 3: Initialize the HUD last**

Immediately after the `beatVfx.Initialize(Bus);` line (and before the `// SetController.Start()` comment), add:

```csharp
            hud.Initialize(Bus, hype, economy, setController, abilities, upgrades);
```

- [ ] **Step 4: Self-check + Domain regression guard**

Confirm `hud` is in the field list, the null-guard, and is initialized after every system it receives (`hype`, `economy`, `setController`, `abilities`, `upgrades` are all initialized earlier in the method). Then run the Domain guard:

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — 73 tests (UI changes don't touch Domain; this confirms nothing regressed).

- [ ] **Step 5: Commit**

```bash
git add "Assets/PitTycoon/Unity/GameBootstrap.cs"
git commit -m "feat(unity): initialize HudController from GameBootstrap"
```

---

## Task 7: Strip `DebugHud` to an F1 dev overlay

**Files:**
- Modify: `Assets/PitTycoon/Unity/DebugHud.cs`

**Interfaces:**
- Produces: a `DebugHud` that renders only dev readouts (intensity, beat box + strength, last-beat dsp) and only while toggled on by F1 (default off). Its player-facing IMGUI (hype bar, ability buttons, intermission shop, cash/set) is removed — the new UI owns those.

- [ ] **Step 1: Replace the file contents**

Replace all of `Assets/PitTycoon/Unity/DebugHud.cs` with:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Dev-only IMGUI overlay (toggle with F1, default off): analyzer intensity bar, a flash on
    /// each detected beat, and the last beat's dsp time. The player-facing UI is now UGUI
    /// (HudController/LiveHudView/ShopView); this exists purely for tuning the analyzer.
    /// </summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;

        private double _lastBeat = -1.0;
        private float _flash;
        private bool _show;

        private void OnEnable()
        {
            if (analyzer != null) analyzer.BeatDetected += OnBeat;
        }

        private void OnDisable()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat)
        {
            _lastBeat = beat.DspTime;
            _flash = Mathf.Clamp01(0.4f + beat.Strength);
        }

        private void Update()
        {
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 4f);
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) _show = !_show;
        }

        private void OnGUI()
        {
            if (!_show) return;

            if (analyzer == null)
            {
                GUI.Label(new Rect(12, 12, 360, 20), "DebugHud: analyzer not assigned");
                return;
            }

            float intensity = analyzer.Intensity01;
            GUI.Label(new Rect(12, 12, 300, 20), $"Intensity: {intensity:0.00}");
            GUI.Box(new Rect(12, 34, 200, 16), GUIContent.none);
            GUI.Box(new Rect(12, 34, 200f * Mathf.Clamp01(intensity), 16), GUIContent.none);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f, _flash);
            GUI.Box(new Rect(12, 56, 90, 90), "BEAT");
            GUI.color = prev;

            GUI.Label(new Rect(12, 152, 320, 20), $"Last beat dsp: {_lastBeat:0.00}");
        }
    }
}
```

- [ ] **Step 2: Self-check**

Confirm: only `analyzer` remains as a serialized field (the `hype`/`abilities`/`economy`/`setController`/`upgrades` fields and all their IMGUI are gone); F1 toggles `_show`; `OnGUI` early-returns when `_show` is false; beat subscription/unsubscription preserved.

> Note for Task 10: the scene's existing `DebugHud` component will keep stale serialized references to the removed fields — harmless (Unity drops unknown fields), and the new UI replaces that functionality. Rebuilding via the editor menus is not required for `DebugHud` itself.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/DebugHud.cs"
git commit -m "refactor(unity): reduce DebugHud to an F1-toggle analyzer dev overlay"
```

---

## Task 8: `HudSetup` editor builder

**Files:**
- Create: `Assets/PitTycoon/Unity/Editor/HudSetup.cs`

**Interfaces:**
- Consumes: `HudController`/`LiveHudView`/`ShopView`/`AbilityButtonWidget`/`ShopRowWidget` (Tasks 2–5), the `hud` field on `GameBootstrap` (Task 6).
- Produces: menu `Pit Tycoon → Build HUD` that constructs the canvas, wires every serialized ref, and sets `GameBootstrap.hud`.

- [ ] **Step 1: Create the file**

Create `Assets/PitTycoon/Unity/Editor/HudSetup.cs`:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using PitTycoon.Unity;
using PitTycoon.Unity.UI;

namespace PitTycoon.Unity.EditorTools
{
    /// <summary>
    /// Builds the UGUI HUD (canvas, live HUD, intermission shop) from scratch and wires every
    /// serialized reference, including GameBootstrap.hud. Idempotent: re-running destroys the
    /// previous "GameHUD" root and rebuilds. New-Input-System aware (InputSystemUIInputModule).
    /// </summary>
    public static class HudSetup
    {
        private static readonly Color Panel = new Color(0.14f, 0.12f, 0.21f, 0.96f);
        private static readonly Color Bar = new Color(0.17f, 0.15f, 0.25f, 1f);
        private static readonly Color Ink = new Color(0.07f, 0.07f, 0.07f, 1f);
        private static readonly Color HypeOrange = new Color(0.85f, 0.35f, 0.19f, 1f);
        private static readonly Color Amber = new Color(0.98f, 0.78f, 0.46f, 1f);

        [MenuItem("Pit Tycoon/Build HUD")]
        public static void Build()
        {
            var existing = GameObject.Find("GameHUD");
            if (existing != null) Object.DestroyImmediate(existing);

            EnsureEventSystem();

            var root = new GameObject("GameHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            HudController controller = root.AddComponent<HudController>();

            LiveHudView live = BuildLiveHud(root.transform, out var liveRefs);
            ShopView shop = BuildShop(root.transform, out var shopRefs);

            WireLive(live, liveRefs);
            WireShop(shop, shopRefs);
            WireController(controller, live, shop);
            WireBootstrap(controller);

            live.gameObject.SetActive(true);
            shop.gameObject.SetActive(true);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Pit Tycoon: HUD built. Import TMP Essentials if text is invisible " +
                      "(Window > TextMeshPro > Import TMP Essential Resources).");
        }

        // ---- live HUD ------------------------------------------------------

        private struct LiveRefs
        {
            public Image hypeFill; public RectTransform peakMarker; public TMP_Text hypeText;
            public TMP_Text cashText; public TMP_Text setText; public RectTransform abilityBar;
            public AbilityButtonWidget template; public TMP_Text hitQuality; public Graphic beatPulse;
        }

        private static LiveHudView BuildLiveHud(Transform parent, out LiveRefs r)
        {
            var go = NewUI("LiveHud", parent);
            Stretch(go.GetComponent<RectTransform>());
            LiveHudView view = go.AddComponent<LiveHudView>();
            r = new LiveRefs();

            // hype bar area (top-center)
            var barArea = NewUI("HypeBar", go.transform);
            var barRT = barArea.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 1f); barRT.anchorMax = new Vector2(0.5f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -24f);
            barRT.sizeDelta = new Vector2(680f, 30f);
            AddImage(barArea, Bar);
            AddOutline(barArea);

            var fill = NewUI("Fill", barArea.transform);
            Stretch(fill.GetComponent<RectTransform>());
            r.hypeFill = AddImage(fill, HypeOrange);
            r.hypeFill.type = Image.Type.Filled;
            r.hypeFill.fillMethod = Image.FillMethod.Horizontal;
            r.hypeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            r.hypeFill.fillAmount = 0.3f;

            var marker = NewUI("PeakMarker", barArea.transform);
            r.peakMarker = marker.GetComponent<RectTransform>();
            r.peakMarker.anchorMin = new Vector2(0.5f, 0f); r.peakMarker.anchorMax = new Vector2(0.5f, 1f);
            r.peakMarker.pivot = new Vector2(0.5f, 0.5f);
            r.peakMarker.sizeDelta = new Vector2(3f, 0f);
            AddImage(marker, Amber);

            var hypeTextGO = NewUI("HypeText", barArea.transform);
            Stretch(hypeTextGO.GetComponent<RectTransform>());
            r.hypeText = AddText(hypeTextGO, "0 / 0", 16, TextAlignmentOptions.Center);

            // cash (top-right)
            var cash = NewUI("Cash", go.transform);
            var cashRT = cash.GetComponent<RectTransform>();
            cashRT.anchorMin = new Vector2(1f, 1f); cashRT.anchorMax = new Vector2(1f, 1f);
            cashRT.pivot = new Vector2(1f, 1f); cashRT.anchoredPosition = new Vector2(-16f, -16f);
            cashRT.sizeDelta = new Vector2(160f, 36f);
            AddImage(cash, Bar); AddOutline(cash);
            var cashTextGO = NewUI("Text", cash.transform);
            Stretch(cashTextGO.GetComponent<RectTransform>());
            r.cashText = AddText(cashTextGO, "$0", 18, TextAlignmentOptions.Center);
            r.cashText.color = Amber;

            // set (top-left)
            var set = NewUI("Set", go.transform);
            var setRT = set.GetComponent<RectTransform>();
            setRT.anchorMin = new Vector2(0f, 1f); setRT.anchorMax = new Vector2(0f, 1f);
            setRT.pivot = new Vector2(0f, 1f); setRT.anchoredPosition = new Vector2(16f, -16f);
            setRT.sizeDelta = new Vector2(160f, 36f);
            AddImage(set, Bar); AddOutline(set);
            var setTextGO = NewUI("Text", set.transform);
            Stretch(setTextGO.GetComponent<RectTransform>());
            r.setText = AddText(setTextGO, "SET 1", 16, TextAlignmentOptions.Center);

            // ability bar (bottom-center) with a horizontal layout
            var abilityBar = NewUI("AbilityBar", go.transform);
            r.abilityBar = abilityBar.GetComponent<RectTransform>();
            r.abilityBar.anchorMin = new Vector2(0.5f, 0f); r.abilityBar.anchorMax = new Vector2(0.5f, 0f);
            r.abilityBar.pivot = new Vector2(0.5f, 0f); r.abilityBar.anchoredPosition = new Vector2(0f, 24f);
            r.abilityBar.sizeDelta = new Vector2(400f, 72f);
            var layout = abilityBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f; layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            r.template = BuildAbilityButtonTemplate(abilityBar.transform);

            // hit-quality popup (above the ability bar)
            var hit = NewUI("HitQuality", go.transform);
            var hitRT = hit.GetComponent<RectTransform>();
            hitRT.anchorMin = new Vector2(0.5f, 0f); hitRT.anchorMax = new Vector2(0.5f, 0f);
            hitRT.pivot = new Vector2(0.5f, 0f); hitRT.anchoredPosition = new Vector2(0f, 104f);
            hitRT.sizeDelta = new Vector2(300f, 40f);
            r.hitQuality = AddText(hit, "PERFECT!", 24, TextAlignmentOptions.Center);

            // beat pulse (full-screen edge tint)
            var pulse = NewUI("BeatPulse", go.transform);
            Stretch(pulse.GetComponent<RectTransform>());
            var pulseImg = AddImage(pulse, new Color(1f, 1f, 1f, 0f));
            pulseImg.raycastTarget = false;
            r.beatPulse = pulseImg;

            return view;
        }

        private static AbilityButtonWidget BuildAbilityButtonTemplate(Transform parent)
        {
            var go = NewUI("AbilityButtonTemplate", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(58f, 58f);
            var bg = AddImage(go, new Color(0.33f, 0.29f, 0.72f, 1f));
            AddOutline(go);
            var button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            var widget = go.AddComponent<AbilityButtonWidget>();
            widget.Button = button; widget.Background = bg;

            var label = NewUI("Label", go.transform);
            Stretch(label.GetComponent<RectTransform>());
            widget.Label = AddText(label, "A", 12, TextAlignmentOptions.Center);

            var cd = NewUI("Cooldown", go.transform);
            Stretch(cd.GetComponent<RectTransform>());
            var cdImg = AddImage(cd, new Color(0f, 0f, 0f, 0.45f));
            cdImg.type = Image.Type.Filled; cdImg.fillMethod = Image.FillMethod.Vertical;
            cdImg.fillOrigin = (int)Image.OriginVertical.Bottom; cdImg.fillAmount = 0f;
            cdImg.raycastTarget = false;
            widget.CooldownOverlay = cdImg;

            var hk = NewUI("Hotkey", go.transform);
            var hkRT = hk.GetComponent<RectTransform>();
            hkRT.anchorMin = new Vector2(0.5f, 0f); hkRT.anchorMax = new Vector2(0.5f, 0f);
            hkRT.pivot = new Vector2(0.5f, 1f); hkRT.anchoredPosition = new Vector2(0f, -2f);
            hkRT.sizeDelta = new Vector2(58f, 16f);
            widget.Hotkey = AddText(hk, "1", 11, TextAlignmentOptions.Center);

            return widget;
        }

        // ---- shop ----------------------------------------------------------

        private struct ShopRefs
        {
            public TMP_Text cash; public TMP_Text banked; public RectTransform upgrades;
            public RectTransform abilities; public ShopRowWidget template; public Button start;
        }

        private static ShopView BuildShop(Transform parent, out ShopRefs r)
        {
            var go = NewUI("Shop", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(300f, 0f);
            rt.anchoredPosition = new Vector2(-12f, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 12f);
            rt.offsetMax = new Vector2(rt.offsetMax.x, -12f);
            AddImage(go, Panel); AddOutline(go);
            ShopView view = go.AddComponent<ShopView>();
            r = new ShopRefs();

            var vlayout = go.AddComponent<VerticalLayoutGroup>();
            vlayout.padding = new RectOffset(12, 12, 12, 12);
            vlayout.spacing = 8f; vlayout.childControlWidth = true; vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true; vlayout.childForceExpandHeight = false;

            var header = NewUI("Cash", go.transform);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
            r.cash = AddText(header, "$0", 18, TextAlignmentOptions.Right);
            r.cash.color = Amber;

            var banked = NewUI("Banked", go.transform);
            banked.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);
            r.banked = AddText(banked, "", 14, TextAlignmentOptions.Center);
            r.banked.color = new Color(0.62f, 0.88f, 0.8f);

            var upHead = NewUI("UpgradesLabel", go.transform);
            upHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(upHead, "UPGRADES", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var upgrades = NewUI("Upgrades", go.transform);
            r.upgrades = upgrades.GetComponent<RectTransform>();
            var ul = upgrades.AddComponent<VerticalLayoutGroup>();
            ul.spacing = 6f; ul.childControlWidth = true; ul.childControlHeight = false;
            ul.childForceExpandWidth = true; ul.childForceExpandHeight = false;
            upgrades.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var abHead = NewUI("AbilitiesLabel", go.transform);
            abHead.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
            AddText(abHead, "ABILITIES", 12, TextAlignmentOptions.Left).color = new Color(0.6f, 0.6f, 0.6f);

            var abilities = NewUI("Abilities", go.transform);
            r.abilities = abilities.GetComponent<RectTransform>();
            var al = abilities.AddComponent<VerticalLayoutGroup>();
            al.spacing = 6f; al.childControlWidth = true; al.childControlHeight = false;
            al.childForceExpandWidth = true; al.childForceExpandHeight = false;
            abilities.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            r.template = BuildShopRowTemplate(go.transform);

            var start = NewUI("StartNextSet", go.transform);
            start.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
            var startImg = AddImage(start, HypeOrange); AddOutline(start);
            r.start = start.AddComponent<Button>(); r.start.targetGraphic = startImg;
            var startText = NewUI("Text", start.transform);
            Stretch(startText.GetComponent<RectTransform>());
            AddText(startText, "START NEXT SET", 14, TextAlignmentOptions.Center);

            return view;
        }

        private static ShopRowWidget BuildShopRowTemplate(Transform parent)
        {
            var go = NewUI("ShopRowTemplate", parent);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
            var bg = AddImage(go, Bar); AddOutline(go);
            var button = go.AddComponent<Button>(); button.targetGraphic = bg;
            var group = go.AddComponent<CanvasGroup>();
            var widget = go.AddComponent<ShopRowWidget>();
            widget.Button = button; widget.Icon = bg; widget.Group = group;

            var label = NewUI("Label", go.transform);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-64f, 0f);
            widget.Label = AddText(label, "Item", 13, TextAlignmentOptions.Left);

            var cost = NewUI("Cost", go.transform);
            var crt = cost.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 0.5f); crt.sizeDelta = new Vector2(60f, 0f);
            crt.anchoredPosition = new Vector2(-8f, 0f);
            widget.Cost = AddText(cost, "$0", 13, TextAlignmentOptions.Right);

            return widget;
        }

        // ---- wiring (SerializedObject for private [SerializeField]s) --------

        private static void WireLive(LiveHudView view, LiveRefs r)
        {
            var so = new SerializedObject(view);
            so.FindProperty("hypeFill").objectReferenceValue = r.hypeFill;
            so.FindProperty("peakMarker").objectReferenceValue = r.peakMarker;
            so.FindProperty("hypeText").objectReferenceValue = r.hypeText;
            so.FindProperty("cashText").objectReferenceValue = r.cashText;
            so.FindProperty("setText").objectReferenceValue = r.setText;
            so.FindProperty("abilityBar").objectReferenceValue = r.abilityBar;
            so.FindProperty("abilityButtonTemplate").objectReferenceValue = r.template;
            so.FindProperty("hitQualityText").objectReferenceValue = r.hitQuality;
            so.FindProperty("beatPulse").objectReferenceValue = r.beatPulse;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireShop(ShopView view, ShopRefs r)
        {
            var so = new SerializedObject(view);
            so.FindProperty("cashText").objectReferenceValue = r.cash;
            so.FindProperty("bankedText").objectReferenceValue = r.banked;
            so.FindProperty("upgradeContainer").objectReferenceValue = r.upgrades;
            so.FindProperty("abilityContainer").objectReferenceValue = r.abilities;
            so.FindProperty("rowTemplate").objectReferenceValue = r.template;
            so.FindProperty("startNextSetButton").objectReferenceValue = r.start;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireController(HudController c, LiveHudView live, ShopView shop)
        {
            var so = new SerializedObject(c);
            so.FindProperty("liveView").objectReferenceValue = live;
            so.FindProperty("shopView").objectReferenceValue = shop;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBootstrap(HudController c)
        {
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            if (boot == null) { Debug.LogWarning("HudSetup: no GameBootstrap in scene; skipped hud wiring."); return; }
            var so = new SerializedObject(boot);
            so.FindProperty("hud").objectReferenceValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- primitives ----------------------------------------------------

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }
            // New-Input-System: replace any legacy StandaloneInputModule.
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Image AddImage(GameObject go, Color c)
        {
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static void AddOutline(GameObject go)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = Ink;
            o.effectDistance = new Vector2(2f, -2f);
        }

        private static TMP_Text AddText(GameObject go, string text, float size, TextAlignmentOptions align)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }
    }
}
```

- [ ] **Step 2: Self-check**

Confirm: menu item `Pit Tycoon/Build HUD`; idempotent (`DestroyImmediate` of existing `GameHUD`); `EnsureEventSystem` adds `InputSystemUIInputModule` and strips `StandaloneInputModule`; every serialized field name passed to `FindProperty` exactly matches the field declared on the corresponding view/controller/bootstrap (cross-check the names against Tasks 3–6); widget public fields assigned directly. Templates (`AbilityButtonTemplate`, `ShopRowTemplate`) are left active here — the views set them inactive in `Initialize`.

- [ ] **Step 3: Commit**

```bash
git add "Assets/PitTycoon/Unity/Editor/HudSetup.cs"
git commit -m "feat(editor): add HudSetup — builds and wires the UGUI HUD canvas"
```

---

## Task 9: SETUP.md — Build HUD section

**Files:**
- Modify: `SETUP.md`

- [ ] **Step 1: Append the section**

At the end of `SETUP.md`, append:

```markdown
## UI foundation (UGUI HUD + intermission shop)

Prereq: the festival scene exists (M2b) and systems are wired (`GameBootstrap`).

1. Pull the branch; let Unity recompile (new `PitTycoon.Unity.UI` scripts, `HudSetup`).
2. **One-time:** `Window → TextMeshPro → Import TMP Essential Resources` (UGUI text is invisible without it).
3. Run **Pit Tycoon → Build HUD**. This creates the `GameHUD` canvas, an `EventSystem`
   (with `InputSystemUIInputModule`), and wires every reference including `GameBootstrap.hud`.
   Re-running rebuilds cleanly.
4. Play. Expected:
   - **Live:** hype bar (top-center) tracks hype with a peak marker; cash (top-right) and
     set number (top-left) update; an ability button per owned ability (bottom-center) fires
     on click **and** on its hotkey, with a cooldown overlay; a hit-quality popup and a beat
     pulse fire during play.
   - **Set end:** the intermission shop docks on the right (scene stays visible, no dimming),
     shows the banked-this-set amount, upgrade rows (name/level/cost, greyed when unaffordable)
     and ability-unlock rows; buying refreshes the rows; **Start Next Set** returns to live.
   - **F1** toggles the dev overlay (analyzer intensity / beat / dsp).

### Tuning
- `LiveHudView`: `hitQualityHold` (popup duration), `beatPulseDecay` (pulse fade speed).
- `HudSetup` constants (`Panel`/`Bar`/`HypeOrange`/`Amber`, anchors/sizes) control the look;
  re-run **Build HUD** after editing.
```

- [ ] **Step 2: Commit**

```bash
git add "SETUP.md"
git commit -m "docs: SETUP UI foundation — Build HUD, TMP essentials, verification"
```

---

## Task 10: Unity bring-up checkpoint (needs user)

**Files:** none (Editor + Play-mode verification by the user).

This is a manual checkpoint — the agent cannot operate the Editor.

- [ ] **Step 1: Recompile**

Open the project; watch the Console. Expected: **no errors** (asmdef refs resolve `UnityEngine.UI`/`TMPro`/`InputSystemUIInputModule`; new UI scripts + `HudSetup` compile).

- [ ] **Step 2: Import TMP essentials**

`Window → TextMeshPro → Import TMP Essential Resources` (one-time; skip if already imported).

- [ ] **Step 3: Build the HUD**

Run **Pit Tycoon → Build HUD**. Expected: a `GameHUD` object + `EventSystem` appear; Console logs the TMP reminder; no errors.

- [ ] **Step 4: Play and verify**

- [ ] Hype bar tracks hype; peak marker sits at the set's best; cur/ceiling text correct.
- [ ] Cash and set number update; phase transitions reflected.
- [ ] Owned-ability buttons appear, fire on **click** and on **hotkey** (Space/1/2), show cooldown fills, disable while cooling.
- [ ] Hit-quality popup shows on fire; beat pulse pulses on beats.
- [ ] At set end the shop docks right, **scene still visible (no dimming)**, banked amount correct.
- [ ] Upgrade + ability rows buy correctly, grey when unaffordable, refresh after purchase; Start Next Set returns to live.
- [ ] **F1** toggles the dev overlay; M1/M2/M3 intact (abilities, VFX, coins, venue scaling, crowd fill).

- [ ] **Step 5: Report**

Report results. If a layout element is off-screen or mis-anchored, the fix is an anchor/size tweak in `HudSetup` (then re-run **Build HUD**); if buttons don't respond, confirm the `EventSystem` has `InputSystemUIInputModule` (not `StandaloneInputModule`).

---

## Notes for the implementer

- **Commit messages:** imperative present tense, **no `Co-Authored-By` trailer**.
- **`dotnet test` scope:** Domain only — it stays at **73** green throughout (no Domain changes). Unity-side correctness is the Task 10 checkpoint.
- **The shop must not dim/overlay the scene** — sub-project B (camera + ghost preview) depends on the scene staying visible behind the docked panel.
- **Serialized field names are a contract:** `HudSetup` wires views by string (`FindProperty`). If you rename a private serialized field on a view, update the matching `FindProperty` call in `HudSetup`.
