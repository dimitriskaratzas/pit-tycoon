using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Single composition root (the "Startup.cs"): constructs the EventBus, wires
    /// the analyzer to the crowd, and bridges analyzer beats onto the bus for future
    /// consumers (hype, abilities). No singletons; everything is referenced explicitly.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private FftAudioAnalyzer analyzer;
        [SerializeField] private CrowdController crowd;

        public EventBus Bus { get; private set; }

        private void Awake()
        {
            Bus = new EventBus();

            if (analyzer == null)
            {
                Debug.LogError("GameBootstrap: analyzer not assigned.", this);
                return;
            }
            if (crowd == null)
            {
                Debug.LogError("GameBootstrap: crowd not assigned.", this);
                return;
            }

            // Bridge raw analyzer beats onto the domain EventBus (future consumers subscribe here).
            analyzer.BeatDetected += OnBeat;

            crowd.Initialize(analyzer);
            crowd.Build();
        }

        private void OnDestroy()
        {
            if (analyzer != null) analyzer.BeatDetected -= OnBeat;
        }

        private void OnBeat(BeatInfo beat) => Bus.Publish(new BeatDetected(beat));
    }
}
