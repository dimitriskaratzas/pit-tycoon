using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Persistent cash. Wraps the pure EconomyCalculator: banks a finished set's
    /// hype (weighted peak + average) into cash and validates/executes purchases.
    /// </summary>
    public sealed class EconomySystem : MonoBehaviour
    {
        [SerializeField] private int startingCash = 0;
        [Tooltip("Cash = round(peakHype*peakWeight + avgHype*avgWeight).")]
        [SerializeField] private float peakWeight = 1f;
        [SerializeField] private float avgWeight = 1f;

        private EconomyCalculator _calc;

        public int Cash => _calc?.Cash ?? startingCash;

        public void Initialize()
        {
            _calc = new EconomyCalculator(startingCash);
        }

        /// <summary>Bank a finished set's hype to cash; returns the amount earned.</summary>
        public int Bank(float peakHype, float avgHype)
            => _calc.BankSet(peakHype, avgHype, peakWeight, avgWeight);

        public bool CanAfford(int cost) => _calc.CanAfford(cost);
        public bool TrySpend(int cost) => _calc.TrySpend(cost);

        /// <summary>Structure effect (M4c): flat cash added on top of every set-end bank.</summary>
        public void AddPassiveIncome(int delta) => _calc.AddPassiveIncome(delta);

        /// <summary>Structure effect (M4c): additively raise the hype->cash multiplier.</summary>
        public void AddCashMultiplier(float pct) => _calc.AddCashMultiplier(pct);

        /// <summary>Debug tool: overwrite the balance (skip-to-shop grant). Not the normal earn loop.</summary>
        public void SetCash(int amount) => _calc?.SetCash(amount);
    }
}
