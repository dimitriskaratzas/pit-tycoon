using System;
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class BeatGridTests
    {
        [Test]
        public void NoBeats_HasBeatFalse_NearestIsNegativeInfinity()
        {
            var g = new BeatGrid();
            Assert.That(g.HasBeat, Is.False);
            Assert.That(g.NearestBeatTime(1.0), Is.EqualTo(double.NegativeInfinity));
        }

        [Test]
        public void SingleBeat_NoIntervalYet_NearestIsThatBeat()
        {
            var g = new BeatGrid();
            g.Register(1.0);
            Assert.That(g.HasBeat, Is.True);
            Assert.That(g.NearestBeatTime(1.3), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void UniformBeats_EstimatesInterval()
        {
            var g = new BeatGrid();
            g.Register(1.0);
            g.Register(1.5);
            g.Register(2.0);
            Assert.That(g.Interval, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void NearestSnapsToRecentPastBeat()
        {
            var g = new BeatGrid();
            g.Register(1.0); g.Register(1.5); g.Register(2.0);
            Assert.That(g.NearestBeatTime(2.05), Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void NearestSnapsToPredictedNextBeat_AnticipationWorks()
        {
            var g = new BeatGrid();
            g.Register(1.0); g.Register(1.5); g.Register(2.0);
            // Tap just BEFORE the next beat (2.5) — must still snap to 2.5, not 2.0.
            Assert.That(g.NearestBeatTime(2.45), Is.EqualTo(2.5).Within(1e-9));
        }

        [Test]
        public void NearestPicksCloserGridLine()
        {
            var g = new BeatGrid();
            g.Register(1.0); g.Register(1.5); g.Register(2.0);
            // 2.30 is 0.20 from 2.5 but 0.30 from 2.0 -> 2.5.
            Assert.That(g.NearestBeatTime(2.30), Is.EqualTo(2.5).Within(1e-9));
        }

        [Test]
        public void AnticipatedTap_ScoresOnBeatThroughBeatWindow()
        {
            var g = new BeatGrid();
            g.Register(1.0); g.Register(1.5); g.Register(2.0);
            double fire = 2.48; // a hair before the predicted 2.5 beat
            double nearest = g.NearestBeatTime(fire);
            float mult = BeatWindow.Multiplier(nearest, fire, 0.1, 3f);
            Assert.That(mult, Is.GreaterThan(1.5f), "anticipating the next beat should give a strong on-beat bonus");
        }

        [Test]
        public void OffBeatTap_ScoresNoBonus()
        {
            var g = new BeatGrid();
            g.Register(1.0); g.Register(1.5); g.Register(2.0);
            double fire = 2.25; // squarely between beats
            double nearest = g.NearestBeatTime(fire);
            float mult = BeatWindow.Multiplier(nearest, fire, 0.1, 3f);
            Assert.That(mult, Is.EqualTo(1f).Within(1e-6), "a tap halfway between beats is off-beat");
        }

        [Test]
        public void Ctor_InvalidSmoothing_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeatGrid(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BeatGrid(1.5));
        }
    }
}
