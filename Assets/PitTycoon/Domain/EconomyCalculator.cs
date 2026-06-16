using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Persistent currency across sets. Banks cash from a finished set's hype
    /// (weighted peak + average) and validates purchases. Pure C#.
    /// </summary>
    public sealed class EconomyCalculator
    {
        public int Cash { get; private set; }

        public EconomyCalculator(int startingCash = 0)
        {
            if (startingCash < 0) throw new ArgumentOutOfRangeException(nameof(startingCash));
            Cash = startingCash;
        }

        public int BankSet(float peakHype, float avgHype, float peakWeight, float avgWeight)
        {
            if (peakHype < 0f) throw new ArgumentOutOfRangeException(nameof(peakHype));
            if (avgHype < 0f) throw new ArgumentOutOfRangeException(nameof(avgHype));

            double raw = peakHype * peakWeight + avgHype * avgWeight;
            int earned = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            if (earned < 0) earned = 0;
            Cash += earned;
            return earned;
        }

        public bool CanAfford(int cost) => cost >= 0 && Cash >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost)) return false;
            Cash -= cost;
            return true;
        }
    }
}
