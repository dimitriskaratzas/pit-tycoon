namespace PitTycoon.Domain
{
    /// <summary>
    /// A detected beat. <see cref="DspTime"/> is on the audio DSP clock
    /// (Unity: AudioSettings.dspTime), NOT the frame clock, so on-beat
    /// timing stays accurate independent of frame rate.
    /// </summary>
    public readonly struct BeatInfo
    {
        public double DspTime { get; }
        public float Strength { get; }

        public BeatInfo(double dspTime, float strength)
        {
            DspTime = dspTime;
            Strength = strength;
        }
    }
}
