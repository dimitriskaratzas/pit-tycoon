using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class BeatWindowTests
    {
        [Test]
        public void PerfectlyOnBeat_ReturnsMaxMultiplier()
        {
            float m = BeatWindow.Multiplier(beatDspTime: 10.0, fireDspTime: 10.0, toleranceSeconds: 0.1, maxMultiplier: 3f);
            Assert.That(m, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void AtToleranceEdge_ReturnsOne()
        {
            float m = BeatWindow.Multiplier(10.0, 10.1, 0.1, 3f);
            Assert.That(m, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void BeyondTolerance_ReturnsOne()
        {
            float m = BeatWindow.Multiplier(10.0, 10.5, 0.1, 3f);
            Assert.That(m, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HalfwayInsideWindow_ReturnsLinearMidpoint()
        {
            // delta = 0.05, tolerance 0.1 -> t = 0.5 -> 1 + 0.5*(3-1) = 2.0
            float m = BeatWindow.Multiplier(10.0, 10.05, 0.1, 3f);
            Assert.That(m, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void FiringBeforeBeat_IsSymmetric()
        {
            float m = BeatWindow.Multiplier(10.0, 9.95, 0.1, 3f);
            Assert.That(m, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void NonPositiveTolerance_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BeatWindow.Multiplier(0, 0, 0, 3f));
        }

        [Test]
        public void MaxMultiplierBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BeatWindow.Multiplier(0, 0, 0.1, 0.5f));
        }

        [Test]
        public void BeatInfo_StoresValues()
        {
            var b = new BeatInfo(12.5, 0.8f);
            Assert.That(b.DspTime, Is.EqualTo(12.5));
            Assert.That(b.Strength, Is.EqualTo(0.8f));
        }
    }
}
