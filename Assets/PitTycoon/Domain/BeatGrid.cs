using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Tracks detected beats and estimates the tempo grid so the on-beat check can
    /// reward *anticipation*: given a fire time, it returns the nearest beat — the last
    /// one OR the predicted next one — projected onto the estimated interval. Shared by
    /// every timed ability (whirlpool now; clickable items later). Pure C#, unit-tested.
    /// </summary>
    public sealed class BeatGrid
    {
        private readonly double _smoothing; // EMA weight for new interval samples (0..1)
        private double _lastBeat = double.NegativeInfinity;
        private double _interval;
        private bool _hasInterval;

        public BeatGrid(double smoothing = 0.2)
        {
            if (smoothing <= 0.0 || smoothing > 1.0) throw new ArgumentOutOfRangeException(nameof(smoothing));
            _smoothing = smoothing;
        }

        public bool HasBeat => !double.IsNegativeInfinity(_lastBeat);
        public double Interval => _interval;

        /// <summary>Feed a detected beat's DSP time (monotonic non-decreasing in practice).</summary>
        public void Register(double beatDspTime)
        {
            if (HasBeat)
            {
                double gap = beatDspTime - _lastBeat;
                if (gap > 0.0)
                {
                    if (!_hasInterval) { _interval = gap; _hasInterval = true; }
                    else _interval += _smoothing * (gap - _interval);
                }
            }
            _lastBeat = beatDspTime;
        }

        /// <summary>
        /// Nearest beat time to <paramref name="fireDspTime"/> on the estimated grid.
        /// Returns NegativeInfinity if no beat has been seen (caller treats as off-beat).
        /// With only one beat (no interval yet), returns that beat.
        /// </summary>
        public double NearestBeatTime(double fireDspTime)
        {
            if (!HasBeat) return double.NegativeInfinity;
            if (!_hasInterval || _interval <= 0.0) return _lastBeat;
            double steps = Math.Round((fireDspTime - _lastBeat) / _interval);
            return _lastBeat + steps * _interval;
        }
    }
}
