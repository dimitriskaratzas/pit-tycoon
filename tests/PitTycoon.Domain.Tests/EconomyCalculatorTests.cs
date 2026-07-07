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

        // ---- M4c: passive income + cash multiplier ----

        [Test]
        public void AddPassiveIncome_AddsFlatAmountToBankedCash()
        {
            var e = new EconomyCalculator(0);
            e.AddPassiveIncome(25);
            int earned = e.BankSet(100f, 0f, 1f, 0f);   // round(100*1) + 25
            Assert.That(earned, Is.EqualTo(125));
            Assert.That(e.Cash, Is.EqualTo(125));
            Assert.That(e.PassiveIncome, Is.EqualTo(25));
        }

        [Test]
        public void AddCashMultiplier_ScalesHypeEarnings()
        {
            var e = new EconomyCalculator(0);
            e.AddCashMultiplier(0.5f);                   // multiplier 1.5
            int earned = e.BankSet(100f, 0f, 1f, 0f);    // round(100*1.5)
            Assert.That(earned, Is.EqualTo(150));
        }

        [Test]
        public void PassiveAndMultiplier_Stack_MultiplierDoesNotScalePassive()
        {
            var e = new EconomyCalculator(0);
            e.AddPassiveIncome(10);
            e.AddCashMultiplier(0.5f);
            int earned = e.BankSet(100f, 0f, 1f, 0f);    // round(100*1.5) + 10, NOT round(110*1.5)
            Assert.That(earned, Is.EqualTo(160));
        }

        [Test]
        public void CashMultiplier_AccumulatesAdditively()
        {
            var e = new EconomyCalculator(0);
            e.AddCashMultiplier(0.15f);
            e.AddCashMultiplier(0.15f);
            Assert.That(e.CashMultiplier, Is.EqualTo(1.3f).Within(1e-4f));
            int earned = e.BankSet(100f, 0f, 1f, 0f);
            Assert.That(earned, Is.EqualTo(130));
        }

        [Test]
        public void Defaults_ReproduceOldFormula()
        {
            var e = new EconomyCalculator(0);            // no passive, multiplier 1
            int earned = e.BankSet(100f, 50f, 0.5f, 0.5f);
            Assert.That(earned, Is.EqualTo(75));         // identical to the pre-M4c behavior
        }

        [Test]
        public void AddPassiveIncome_Negative_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.AddPassiveIncome(-1));
        }

        [Test]
        public void AddCashMultiplier_Negative_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.AddCashMultiplier(-0.1f));
        }

        // ---- debug: set cash directly ----

        [Test]
        public void SetCash_OverwritesBalance()
        {
            var e = new EconomyCalculator(100);
            e.SetCash(99999);
            Assert.That(e.Cash, Is.EqualTo(99999));
        }

        [Test]
        public void SetCash_Negative_Throws()
        {
            var e = new EconomyCalculator(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => e.SetCash(-1));
        }
    }
}
