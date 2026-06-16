using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Live hype for one set. Fills passively from intensity * rate over time,
    /// spikes from abilities, and clamps to a ceiling that upgrades can raise.
    /// Tracks peak and a time-weighted average for end-of-set cash banking.
    /// Pure C# — no UnityEngine, fully unit-testable.
    /// </summary>
    public sealed class HypeCalculator
    {
        private double _weightedSum; // sum of Current * dt
        private double _elapsed;     // sum of dt

        public float Ceiling { get; private set; }
        public float Current { get; private set; }
        public float Peak { get; private set; }
        public float Average => _elapsed > 0.0 ? (float)(_weightedSum / _elapsed) : 0f;

        public HypeCalculator(float ceiling)
        {
            if (ceiling <= 0f) throw new ArgumentOutOfRangeException(nameof(ceiling));
            Ceiling = ceiling;
        }

        public void Tick(float deltaSeconds, float intensity01, float passiveRatePerSecond)
        {
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            float intensity = Clamp01(intensity01);
            Add(passiveRatePerSecond * intensity * deltaSeconds);
            _weightedSum += Current * deltaSeconds;
            _elapsed += deltaSeconds;
        }

        public void AddSpike(float amount)
        {
            if (amount < 0f) throw new ArgumentOutOfRangeException(nameof(amount));
            Add(amount);
        }

        public void RaiseCeiling(float delta)
        {
            if (delta < 0f) throw new ArgumentOutOfRangeException(nameof(delta));
            Ceiling += delta;
        }

        public void ResetForNewSet()
        {
            Current = 0f;
            Peak = 0f;
            _weightedSum = 0.0;
            _elapsed = 0.0;
        }

        private void Add(float amount)
        {
            Current = Math.Min(Ceiling, Current + amount);
            if (Current > Peak) Peak = Current;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
