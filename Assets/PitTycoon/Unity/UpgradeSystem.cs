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
