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
