using System;
using System.Collections.Generic;
using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// v1 IAudioAnalyzer: runtime FFT over an AudioSource. Computes a smoothed
    /// intensity and delegates beat onset detection to the pure SpectralFluxDetector.
    /// Beats are timestamped on AudioSettings.dspTime (audio clock), NOT Time.time.
    ///
    /// v2 (architected for, not built): a second IAudioAnalyzer reading a user file.
    /// Consumers must tolerate noisy beats.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class FftAudioAnalyzer : MonoBehaviour, IAudioAnalyzer
    {
        [SerializeField] private AudioAnalyzerConfig config;
        [SerializeField] private AudioSource source;

        private float[] _spectrum;
        private float[] _bands;
        private SpectralFluxDetector _detector;
        private float _intensity;

        public float Intensity01 => _intensity;
        public IReadOnlyList<float> Bands => _bands;
        public event Action<BeatInfo> BeatDetected;

        private void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("FftAudioAnalyzer: AudioAnalyzerConfig not assigned.", this);
                enabled = false;
                return;
            }
            if (source == null) source = GetComponent<AudioSource>();

            _spectrum = new float[config.FftSize];
            _bands = new float[config.NumBands];
            _detector = new SpectralFluxDetector(
                config.FftSize, config.HistorySize, config.ThresholdMultiplier, config.MinBeatIntervalSeconds);
        }

        private void Update()
        {
            if (source == null || !source.isPlaying) return;

            source.GetSpectrumData(_spectrum, 0, config.FftWindow);

            // Smoothed overall intensity 0..1.
            float energy = 0f;
            for (int i = 0; i < _spectrum.Length; i++) energy += _spectrum[i];
            float target = Mathf.Clamp01(energy * config.IntensityGain);
            float a = 1f - Mathf.Exp(-config.IntensitySmoothing * Time.deltaTime);
            _intensity = Mathf.Lerp(_intensity, target, a);

            // Band energies (simple linear grouping is fine for greybox).
            int per = Mathf.Max(1, _spectrum.Length / _bands.Length);
            for (int b = 0; b < _bands.Length; b++)
            {
                int start = b * per;
                int end = Mathf.Min(start + per, _spectrum.Length);
                float sum = 0f;
                for (int i = start; i < end; i++) sum += _spectrum[i];
                _bands[b] = sum;
            }

            // Beat onset on the DSP clock.
            double dsp = AudioSettings.dspTime;
            if (_detector.Process(_spectrum, dsp, out float strength))
            {
                BeatDetected?.Invoke(new BeatInfo(dsp, strength));
            }
        }
    }
}
