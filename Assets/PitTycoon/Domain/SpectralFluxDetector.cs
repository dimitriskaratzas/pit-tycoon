using System;
using System.Collections.Generic;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Pure spectral-flux onset detector. Feed it successive FFT magnitude frames
    /// (from the Unity layer's AudioSource.GetSpectrumData) plus the DSP timestamp;
    /// it reports beat onsets using an adaptive threshold over a rolling history of
    /// recent flux, with a refractory period. No UnityEngine dependency.
    ///
    /// Tolerates noisy beats by design: dense genres (e.g. metal) produce more flux
    /// noise, so tune ThresholdMultiplier / MinInterval via AudioAnalyzerConfig.
    /// </summary>
    public sealed class SpectralFluxDetector
    {
        private const float FluxFloor = 1e-4f;

        private readonly int _bins;
        private readonly int _historySize;
        private readonly float _thresholdMultiplier;
        private readonly double _minIntervalSeconds;
        private readonly float[] _previous;
        private readonly Queue<float> _history;
        private bool _hasPrevious;
        private double _lastBeatTime = double.NegativeInfinity;

        public SpectralFluxDetector(int bins, int historySize, float thresholdMultiplier, double minIntervalSeconds)
        {
            if (bins <= 0) throw new ArgumentOutOfRangeException(nameof(bins));
            if (historySize <= 0) throw new ArgumentOutOfRangeException(nameof(historySize));
            if (thresholdMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(thresholdMultiplier));
            if (minIntervalSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds));

            _bins = bins;
            _historySize = historySize;
            _thresholdMultiplier = thresholdMultiplier;
            _minIntervalSeconds = minIntervalSeconds;
            _previous = new float[bins];
            _history = new Queue<float>(historySize);
        }

        /// <summary>Process one spectrum frame. Returns true on a detected onset.</summary>
        public bool Process(float[] spectrum, double dspTime, out float strength)
        {
            if (spectrum == null) throw new ArgumentNullException(nameof(spectrum));
            if (spectrum.Length != _bins) throw new ArgumentException("Spectrum length mismatch", nameof(spectrum));

            strength = 0f;

            // Spectral flux: sum of positive bin-to-bin increases since the last frame.
            float flux = 0f;
            if (_hasPrevious)
            {
                for (int i = 0; i < _bins; i++)
                {
                    float diff = spectrum[i] - _previous[i];
                    if (diff > 0f) flux += diff;
                }
            }
            Array.Copy(spectrum, _previous, _bins);
            _hasPrevious = true;

            // Adaptive threshold from the rolling history of PAST flux (excludes this frame).
            float threshold = float.PositiveInfinity; // not warmed up yet -> no detection
            if (_history.Count >= _historySize)
            {
                float sum = 0f;
                foreach (float f in _history) sum += f;
                threshold = (sum / _history.Count) * _thresholdMultiplier;
            }

            // Record this frame's flux into the bounded history.
            _history.Enqueue(flux);
            if (_history.Count > _historySize) _history.Dequeue();

            bool warmed = !float.IsPositiveInfinity(threshold);
            bool aboveThreshold = flux > threshold && flux > FluxFloor;
            bool pastRefractory = (dspTime - _lastBeatTime) >= _minIntervalSeconds;

            if (warmed && aboveThreshold && pastRefractory)
            {
                _lastBeatTime = dspTime;
                float denom = Math.Max(threshold, FluxFloor);
                strength = Clamp01((flux - threshold) / denom);
                return true;
            }
            return false;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
