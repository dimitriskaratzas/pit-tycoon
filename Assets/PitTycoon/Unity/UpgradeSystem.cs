using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns the available upgrade(s) and applies their effects. On purchase: spend via
    /// EconomySystem, grow the crowd + raise the hype ceiling, then broadcast
    /// UpgradePurchased. The visible payoff lands when the next set's crowd is built.
    /// </summary>
    public sealed class UpgradeSystem : MonoBehaviour
    {
        [SerializeField] private UpgradeDefinition capacityUpgrade;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private CrowdController crowd;

        private EventBus _bus;

        public UpgradeDefinition CapacityUpgrade => capacityUpgrade;

        public void Initialize(EventBus bus) { _bus = bus; }

        public bool CanAfford(UpgradeDefinition upg)
            => upg != null && economy != null && economy.CanAfford(upg.Cost);

        /// <summary>Attempt to buy; applies effects and broadcasts on success.</summary>
        public bool TryPurchase(UpgradeDefinition upg)
        {
            if (upg == null || economy == null) return false;
            if (!economy.TrySpend(upg.Cost)) return false;

            if (crowd != null) crowd.Grow(upg.AddColumns, upg.AddRows);
            if (hype != null) hype.RaiseCeiling(upg.CeilingDelta);

            _bus?.Publish(new UpgradePurchased(upg.Id));
            return true;
        }
    }
}
