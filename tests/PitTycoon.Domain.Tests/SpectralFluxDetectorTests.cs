using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class SpectralFluxDetectorTests
    {
        private static float[] Frame(int bins, float value)
        {
            var a = new float[bins];
            for (int i = 0; i < bins; i++) a[i] = value;
            return a;
        }

        [Test]
        public void Ctor_InvalidBins_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectralFluxDetector(0, 4, 1.5f, 0.1));
        }

        [Test]
        public void Ctor_InvalidHistory_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectralFluxDetector(4, 0, 1.5f, 0.1));
        }

        [Test]
        public void Ctor_MultiplierBelowOne_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpectralFluxDetector(4, 4, 0.9f, 0.1));
        }

        [Test]
        public void Process_WrongLength_Throws()
        {
            var d = new SpectralFluxDetector(4, 4, 1.5f, 0.1);
            Assert.Throws<ArgumentException>(() => d.Process(new float[3], 0.0, out _));
        }

        [Test]
        public void SteadySpectrum_ProducesNoBeats()
        {
            var d = new SpectralFluxDetector(4, 4, 1.5f, 0.1);
            int beats = 0;
            for (int i = 0; i < 10; i++)
                if (d.Process(Frame(4, 0.5f), i * 0.05, out _)) beats++;
            Assert.That(beats, Is.EqualTo(0));
        }

        [Test]
        public void EnergyJump_AfterWarmup_DetectsBeatWithBoundedStrength()
        {
            var d = new SpectralFluxDetector(4, 4, 1.5f, 0.1);
            // Warmup: 6 silent frames build the (zero) flux history.
            for (int i = 0; i < 6; i++) d.Process(Frame(4, 0f), i * 0.01, out _);
            bool beat = d.Process(Frame(4, 1f), 1.0, out float strength);
            Assert.That(beat, Is.True);
            Assert.That(strength, Is.GreaterThan(0f));
            Assert.That(strength, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void RefractoryPeriod_SuppressesRapidSecondBeat()
        {
            var d = new SpectralFluxDetector(4, 4, 1.5f, 0.5);
            var times = new List<double>();
            for (int i = 0; i < 6; i++) d.Process(Frame(4, 0f), i * 0.01, out _); // warmup

            if (d.Process(Frame(4, 1f), 1.0, out _)) times.Add(1.0); // A: beat
            d.Process(Frame(4, 0f), 1.1, out _);
            if (d.Process(Frame(4, 1f), 1.2, out _)) times.Add(1.2); // B: within 0.5s -> suppressed
            d.Process(Frame(4, 0f), 1.3, out _);
            if (d.Process(Frame(4, 1f), 1.6, out _)) times.Add(1.6); // C: 0.6s after A -> beat

            Assert.That(times, Is.EqualTo(new List<double> { 1.0, 1.6 }));
        }
    }
}
