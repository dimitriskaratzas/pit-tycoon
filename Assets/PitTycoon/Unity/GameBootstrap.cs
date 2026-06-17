using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Single composition root (the "Startup.cs"): constructs the EventBus, bridges
    /// analyzer beats onto it, and Initialize(bus)s every system. Systems get their
    /// peer references via serialized fields (wired by the editor script), so nothing
    /// uses FindObjectOfType or singletons. Order: all Awakes (wiring) run before any
    /// Start (SetController auto-starts set 1 in its Start).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private WhirlpoolAbility ability;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private SetController setController;

        public EventBus Bus { get; private set; }

        private void Awake()
        {
            Bus = new EventBus();

            if (analyzer == null || crowd == null || hype == null || ability == null
                || economy == null || upgrades == null || setController == null)
            {
                Debug.LogError("GameBootstrap: one or more system references are not assigned.", this);
                return;
            }

            // Bridge raw analyzer beats onto the bus (ability, future consumers subscribe here).
            analyzer.BeatDetected += OnBeat;

            crowd.Initialize(analyzer);
            economy.Initialize();
            hype.Initialize(Bus);
            ability.Initialize(Bus);
            upgrades.Initialize(Bus);
            setController.Initialize(Bus);
            // SetController.Start() (after all Awakes) kicks off set 1.
        }

        private void OnDestroy()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat) => Bus.Publish(new BeatDetected(beat));
    }
}
