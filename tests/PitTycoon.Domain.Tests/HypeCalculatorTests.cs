using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class HypeCalculatorTests
    {
        [Test]
        public void NonPositiveCeiling_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypeCalculator(0f));
        }

        [Test]
        public void Tick_FillsProportionalToIntensityRateAndTime()
        {
            var h = new HypeCalculator(ceiling: 100f);
            h.Tick(deltaSeconds: 1f, intensity01: 0.5f, passiveRatePerSecond: 10f);
            Assert.That(h.Current, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void Tick_ClampsIntensityToOne()
        {
            var h = new HypeCalculator(100f);
            h.Tick(1f, 2f, 10f); // intensity treated as 1.0
            Assert.That(h.Current, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Current_ClampsAtCeiling()
        {
            var h = new HypeCalculator(20f);
            h.Tick(10f, 1f, 10f); // would be 100, clamps to 20
            Assert.That(h.Current, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void AddSpike_AddsAndTracksPeak()
        {
            var h = new HypeCalculator(100f);
            h.AddSpike(30f);
            Assert.That(h.Current, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(h.Peak, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void AddSpike_ClampsAtCeiling()
        {
            var h = new HypeCalculator(20f);
            h.AddSpike(50f);
            Assert.That(h.Current, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void NegativeSpike_Throws()
        {
            var h = new HypeCalculator(100f);
            Assert.Throws<ArgumentOutOfRangeException>(() => h.AddSpike(-1f));
        }

        [Test]
        public void Average_IsTimeWeightedAcrossTicks()
        {
            var h = new HypeCalculator(100f);
            h.Tick(1f, 1f, 10f); // current 10, accum 10
            h.Tick(1f, 1f, 10f); // current 20, accum 30, time 2 -> avg 15
            Assert.That(h.Average, Is.EqualTo(15f).Within(0.0001f));
        }

        [Test]
        public void RaiseCeiling_AllowsMoreFill()
        {
            var h = new HypeCalculator(20f);
            h.AddSpike(50f); // clamps to 20
            h.RaiseCeiling(30f); // ceiling now 50
            h.AddSpike(50f); // clamps to 50
            Assert.That(h.Ceiling, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(h.Current, Is.EqualTo(50f).Within(0.0001f));
        }

        [Test]
        public void ResetForNewSet_ZeroesProgressButKeepsCeiling()
        {
            var h = new HypeCalculator(20f);
            h.RaiseCeiling(30f);
            h.Tick(1f, 1f, 10f);
            h.AddSpike(5f);
            h.ResetForNewSet();
            Assert.That(h.Current, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Peak, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Average, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(h.Ceiling, Is.EqualTo(50f).Within(0.0001f));
        }
    }
}
