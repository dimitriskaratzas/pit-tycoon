using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class EconomyCalculatorTests
    {
        [Test]
        public void StartsWithGivenCash()
        {
            var e = new EconomyCalculator(startingCash: 50);
            Assert.That(e.Cash, Is.EqualTo(50));
        }

        [Test]
        public void NegativeStartingCash_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyCalculator(-1));
        }

        [Test]
        public void BankSet_AddsWeightedEarningsAndReturnsThem()
        {
            var e = new EconomyCalculator(0);
            int earned = e.BankSet(peakHype: 100f, avgHype: 50f, peakWeight: 0.5f, avgWeight: 0.5f);
            Assert.That(earned, Is.EqualTo(75)); // 100*0.5 + 50*0.5
            Assert.That(e.Cash, Is.EqualTo(75));
        }

        [Test]
        public void BankSet_RoundsAwayFromZero()
        {
            var e = new EconomyCalculator(0);
            int earned = e.BankSet(1f, 0f, 0.5f, 0f); // 0.5 -> 1
            Assert.That(earned, Is.EqualTo(1));
        }

        [Test]
        public void BankSet_NegativeHype_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.BankSet(-1f, 0f, 1f, 1f));
        }

        [Test]
        public void CanAfford_TrueWhenEnough_FalseWhenNot()
        {
            var e = new EconomyCalculator(100);
            Assert.That(e.CanAfford(100), Is.True);
            Assert.That(e.CanAfford(101), Is.False);
        }

        [Test]
        public void CanAfford_NegativeCost_IsFalse()
        {
            var e = new EconomyCalculator(100);
            Assert.That(e.CanAfford(-5), Is.False);
        }

        [Test]
        public void TrySpend_SuccessReducesCash()
        {
            var e = new EconomyCalculator(100);
            bool ok = e.TrySpend(40);
            Assert.That(ok, Is.True);
            Assert.That(e.Cash, Is.EqualTo(60));
        }

        [Test]
        public void TrySpend_InsufficientLeavesCashUnchanged()
        {
            var e = new EconomyCalculator(30);
            bool ok = e.TrySpend(40);
            Assert.That(ok, Is.False);
            Assert.That(e.Cash, Is.EqualTo(30));
        }
    }
}
