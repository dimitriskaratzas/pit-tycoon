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
