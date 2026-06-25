using UnityEngine;
using PitTycoon.Domain;
using PitTycoon.Unity.UI;

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
        [SerializeField] private AbilitySystem abilities;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private UpgradeSystem upgrades;
        [SerializeField] private SetController setController;
        [SerializeField] private BeatVfxController beatVfx;
        [SerializeField] private HudController hud;

        public EventBus Bus { get; private set; }

        private void Awake()
        {
            Bus = new EventBus();

            if (analyzer == null || crowd == null || hype == null || abilities == null
                || economy == null || upgrades == null || setController == null || beatVfx == null
                || hud == null)
            {
                Debug.LogError("GameBootstrap: one or more system references are not assigned.", this);
                return;
            }

            // Bridge raw analyzer beats onto the bus (ability, future consumers subscribe here).
            analyzer.BeatDetected += OnBeat;

            economy.Initialize();
            hype.Initialize(Bus);
            crowd.Initialize(analyzer, hype, Bus);   // hype passed as IHypeMeter
            hype.SetCrowdMeter(crowd);               // crowd passed as ICrowdMeter
            abilities.Initialize(Bus);
            upgrades.Initialize(Bus);
            setController.Initialize(Bus);
            beatVfx.Initialize(Bus);
            hud.Initialize(Bus, hype, economy, setController, abilities, upgrades);
            // SetController.Start() (after all Awakes) kicks off set 1.
        }

        private void OnDestroy()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat) => Bus.Publish(new BeatDetected(beat));
    }
}
