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
