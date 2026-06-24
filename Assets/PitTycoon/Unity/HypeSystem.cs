using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Drives the pure HypeCalculator from the analyzer's intensity during the live
    /// phase and applies ability spikes. Listens to set lifecycle on the bus; never
    /// references SetController. Hype resets each set; the ceiling persists (upgrades).
    /// </summary>
    public sealed class HypeSystem : MonoBehaviour, IHypeMeter
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private float startingCeiling = 100f;
        [Tooltip("Hype/sec at full intensity from passive venue (before upgrades).")]
        [SerializeField] private float passiveRatePerSecond = 6f;
        [Range(0f, 1f)]
        [Tooltip("Hype-rate multiplier when the pit is empty; 1 = full pit. Lower = stronger warm-up pressure.")]
        [SerializeField] private float minFillMultiplier = 0.4f;

        private HypeCalculator _calc;
        private EventBus _bus;
        private bool _live;
        private float _rateBonus;
        private ICrowdMeter _crowd;

        public float Current => _calc?.Current ?? 0f;
        public float Ceiling => _calc?.Ceiling ?? startingCeiling;
        public float Peak => _calc?.Peak ?? 0f;
        public float Average => _calc?.Average ?? 0f;

        /// <summary>IHypeMeter: current hype as a 0..1 fraction of the ceiling.</summary>
        public float HypeFraction => Ceiling > 0f ? Current / Ceiling : 0f;

        /// <summary>Inject the crowd fill meter (called by GameBootstrap after both exist).</summary>
        public void SetCrowdMeter(ICrowdMeter crowd) => _crowd = crowd;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _calc = new HypeCalculator(startingCeiling);
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnSetStarted(SetStarted e) { _calc.ResetForNewSet(); _live = true; }
        private void OnSetEnded(SetEnded e) { _live = false; }
        private void OnAbilityFired(AbilityFired e) { _calc.AddSpike(e.HypeAdded); }

        /// <summary>Permanently raise the hype ceiling (capacity/stage upgrades).</summary>
        public void RaiseCeiling(float delta) => _calc?.RaiseCeiling(delta);

        /// <summary>Permanently raise the passive hype/sec (lighting/PA upgrades).</summary>
        public void RaiseRate(float delta) => _rateBonus += delta;

        private void Update()
        {
            if (!_live || analyzer == null || _calc == null) return;
            float fill = _crowd?.FillFraction ?? 1f;          // no meter wired -> no penalty
            float mult = Mathf.Lerp(minFillMultiplier, 1f, Mathf.Clamp01(fill));
            _calc.Tick(Time.deltaTime, analyzer.Intensity01, (passiveRatePerSecond + _rateBonus) * mult);
        }
    }
}
