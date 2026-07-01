using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private RectTransform buildContainer;
        [SerializeField] private ShopRowWidget rowTemplate;
        [SerializeField] private Button startNextSetButton;
        [SerializeField] private Button returnHomeButton;

        private EventBus _bus;
        private EconomySystem _economy;
        private SetController _set;
        private AbilitySystem _abilities;
        private UpgradeSystem _upgrades;
        private UpgradePreviewController _preview;
        private BuildSystem _builds;

        private readonly List<ShopRowWidget> _rows = new List<ShopRowWidget>();
        private readonly Dictionary<ShopRowWidget, UpgradeDefinition> _rowUpgrade = new Dictionary<ShopRowWidget, UpgradeDefinition>();
        private readonly Dictionary<ShopRowWidget, AbilityDefinition> _rowAbility = new Dictionary<ShopRowWidget, AbilityDefinition>();
        private readonly Dictionary<ShopRowWidget, BuildSpot> _rowBuild = new Dictionary<ShopRowWidget, BuildSpot>();
        private ShopRowWidget _selected;
        private UpgradeDefinition _selectedUpgrade;
        private AbilityDefinition _selectedAbility;
        private string _selectedBuildId;   // builds are structs -> track selection by id

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

        private void OnDestroy()
        {
            if (startNextSetButton != null) startNextSetButton.onClick.RemoveListener(OnStartNextSet);
            if (returnHomeButton != null) returnHomeButton.onClick.RemoveListener(OnReturnHome);
            if (_bus == null) return;
            _bus.Unsubscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Unsubscribe<AbilityUnlocked>(OnAbilityUnlocked);
            _bus.Unsubscribe<StructureBuilt>(OnStructureBuilt);
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
            _preview?.Cancel();
            gameObject.SetActive(false);
        }

        private void OnStartNextSet() { if (_set != null) _set.StartNextSet(); }   // HudController flies the camera to the live pose on SetStarted
        private void OnReturnHome() { ClearSelectionState(); _preview?.ReturnHome(); }
        private void OnUpgradePurchased(UpgradePurchased e) => RefreshIfVisible();
        private void OnAbilityUnlocked(AbilityUnlocked e) => RefreshIfVisible();
        private void OnStructureBuilt(StructureBuilt e) => RefreshIfVisible();
        private void RefreshIfVisible() { if (gameObject.activeSelf) Rebuild(); }

        private void ClearSelectionState()
        {
            if (_selected != null && _selected.Actions != null) _selected.Actions.SetActive(false);
            _selected = null; _selectedUpgrade = null; _selectedAbility = null; _selectedBuildId = null;
        }

        private void Rebuild()
        {
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();
            _rowUpgrade.Clear();
            _rowAbility.Clear();
            _rowBuild.Clear();

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

            ReapplySelection();
            RebuildLayout();
        }

        // Rows are instantiated at runtime and the panel is shown the same frame, so the nested
        // ContentSizeFitters/VerticalLayoutGroups don't settle until the next layout-dirtying event
        // (e.g. selecting a row) — which left the panel looking cramped on first open. Force an
        // immediate rebuild bottom-up (each section, then the panel) so it lays out correctly at once.
        private void RebuildLayout()
        {
            if (upgradeContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(upgradeContainer);
            if (abilityContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(abilityContainer);
            if (buildContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(buildContainer);
            if (transform is RectTransform root) LayoutRebuilder.ForceRebuildLayoutImmediate(root);
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
            if (_upgrades == null) return;
            SetSelected(row);
            _selectedUpgrade = def; _selectedAbility = null; _selectedBuildId = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _upgrades.CanAfford(def);
            _preview?.BeginUpgradePreview(def, _upgrades.LevelOf(def) + 1);
        }

        private void SelectAbility(ShopRowWidget row, AbilityDefinition def)
        {
            if (_abilities == null) return;
            SetSelected(row);
            _selectedAbility = def; _selectedUpgrade = null; _selectedBuildId = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _abilities.CanAfford(def);
            _preview?.BeginAbilityPreview(def);
        }

        private void SelectBuild(ShopRowWidget row, BuildSpot spot)
        {
            if (_builds == null) return;
            SetSelected(row);
            _selectedBuildId = spot.id; _selectedUpgrade = null; _selectedAbility = null;
            if (row.BuyButton != null) row.BuyButton.interactable = _builds.CanAfford(spot);
            _preview?.BeginBuildPreview(spot);
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

        private void BuyBuild(BuildSpot spot)
        {
            // TryBuild publishes StructureBuilt -> Rebuild; the now-built spot drops from the list,
            // so ReapplySelection clears the selection and the preview (real structure already raised).
            _builds?.TryBuild(spot);
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

        private static ShopRowWidget FindRow<T>(Dictionary<ShopRowWidget, T> map, T value) where T : Object
        {
            foreach (var kv in map) if (kv.Value == value) return kv.Key;
            return null;
        }
    }
}
