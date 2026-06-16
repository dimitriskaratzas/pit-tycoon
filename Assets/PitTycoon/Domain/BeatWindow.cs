using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Pure on-beat scoring. Returns a multiplier in [1, maxMultiplier]:
    /// maxMultiplier when the fire lands exactly on the beat, falling off
    /// linearly to 1.0 at the edge of the tolerance window, and 1.0 outside it.
    /// </summary>
    public static class BeatWindow
    {
        public static float Multiplier(double beatDspTime, double fireDspTime, double toleranceSeconds, float maxMultiplier)
        {
            if (toleranceSeconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(toleranceSeconds));
            if (maxMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(maxMultiplier));

            double delta = Math.Abs(fireDspTime - beatDspTime);
            if (delta >= toleranceSeconds) return 1f;

            double closeness = 1.0 - (delta / toleranceSeconds); // 1 at perfect, 0 at edge
            return 1f + (float)(closeness * (maxMultiplier - 1f));
        }
    }
}
