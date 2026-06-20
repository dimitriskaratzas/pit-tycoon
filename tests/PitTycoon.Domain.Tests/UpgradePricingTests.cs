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
