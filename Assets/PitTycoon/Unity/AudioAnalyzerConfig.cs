using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Inspector-tunable knobs for FftAudioAnalyzer. Beat detection WILL need
    /// iteration per track, so everything lives here (no recompile to tune).
    /// </summary>
    [CreateAssetMenu(fileName = "AudioAnalyzerConfig", menuName = "Pit Tycoon/Audio Analyzer Config")]
    public sealed class AudioAnalyzerConfig : ScriptableObject
    {
        [Header("FFT")]
        [Tooltip("Spectrum sample count. Must be a power of two (64..8192).")]
        public int FftSize = 1024;
        public FFTWindow FftWindow = FFTWindow.BlackmanHarris;
        [Min(1)] public int NumBands = 8;

        [Header("Intensity")]
        [Tooltip("Multiplies summed spectrum energy before clamping to 0..1. Tune per track loudness.")]
        public float IntensityGain = 40f;
        [Tooltip("Higher = snappier intensity response (exponential smoothing rate).")]
        public float IntensitySmoothing = 8f;

        [Header("Beat detection (spectral flux)")]
        [Tooltip("Frames of flux history for the adaptive threshold (~1s at 60fps).")]
        [Min(1)] public int HistorySize = 43;
        [Tooltip("Onset when flux exceeds recent-average-flux * this.")]
        public float ThresholdMultiplier = 1.5f;
        [Tooltip("Minimum seconds between detected beats (refractory).")]
        public float MinBeatIntervalSeconds = 0.12f;
    }
}
