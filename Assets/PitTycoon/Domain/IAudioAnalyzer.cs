using System;
using System.Collections.Generic;

namespace PitTycoon.Domain
{
    /// <summary>
    /// The only audio surface the game consumes. The Unity layer's FftAudioAnalyzer
    /// implements this over AudioSource; a future v2 implementation can read a
    /// user-supplied file. Consumers must tolerate noisy beats (dense genres like
    /// metal produce rougher detection). Never touch AudioSource outside the impl.
    /// </summary>
    public interface IAudioAnalyzer
    {
        /// <summary>Smoothed overall loudness/energy, 0..1.</summary>
        float Intensity01 { get; }

        /// <summary>Current per-band energies (low..high), engine-defined length.</summary>
        IReadOnlyList<float> Bands { get; }

        /// <summary>Fires when a beat onset is detected, timestamped on the DSP clock.</summary>
        event Action<BeatInfo> BeatDetected;
    }
}
