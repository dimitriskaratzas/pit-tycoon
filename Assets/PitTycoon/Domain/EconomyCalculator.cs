using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// Persistent currency across sets. Banks cash from a finished set's hype
    /// (weighted peak + average) and validates purchases. Supports passive income (flat bonus
    /// on set-end) and cash multiplier (scales the hype portion). Pure C#.
    /// </summary>
    public sealed class EconomyCalculator
    {
        public int Cash { get; private set; }

        /// <summary>Flat cash added on top of every set-end bank (structures: Food Court, Bar).</summary>
        public int PassiveIncome { get; private set; }

        /// <summary>Multiplier applied to the hype portion of a bank (structures: VIP Lounge,
        /// Sponsor Banner). Starts at 1; bonuses accumulate additively.</summary>
        public float CashMultiplier { get; private set; } = 1f;

        public EconomyCalculator(int startingCash = 0)
        {
            if (startingCash < 0) throw new ArgumentOutOfRangeException(nameof(startingCash));
            Cash = startingCash;
        }

        public int BankSet(float peakHype, float avgHype, float peakWeight, float avgWeight)
        {
            if (peakHype < 0f) throw new ArgumentOutOfRangeException(nameof(peakHype));
            if (avgHype < 0f) throw new ArgumentOutOfRangeException(nameof(avgHype));

            double raw = (peakHype * peakWeight + avgHype * avgWeight) * CashMultiplier;
            int earned = (int)Math.Round(raw, MidpointRounding.AwayFromZero) + PassiveIncome;
            if (earned < 0) earned = 0;
            Cash += earned;
            return earned;
        }

        public void AddPassiveIncome(int delta)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            PassiveIncome += delta;
        }

        public void AddCashMultiplier(float pct)
        {
            if (pct < 0f) throw new ArgumentOutOfRangeException(nameof(pct));
            CashMultiplier += pct;
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
