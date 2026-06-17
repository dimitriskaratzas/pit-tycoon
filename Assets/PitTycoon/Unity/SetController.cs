using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Set-based state machine: Live ↔ Intermission. Owns track playback, detects the
    /// end of the track on the DSP clock, banks hype→cash, and broadcasts SetStarted/
    /// SetEnded. Auto-starts set 1 on Start(); the HUD calls StartNextSet() to continue.
    /// </summary>
    public sealed class SetController : MonoBehaviour
    {
        public enum Phase { Live, Intermission }

        [SerializeField] private AudioSource source;
        [SerializeField] private HypeSystem hype;
        [SerializeField] private EconomySystem economy;
        [SerializeField] private CrowdController crowd;

        private EventBus _bus;
        private double _setEndDsp;
        private bool _started;

        public Phase Current { get; private set; } = Phase.Intermission;
        public int SetNumber { get; private set; }
        public int LastCashEarned { get; private set; }

        public void Initialize(EventBus bus) { _bus = bus; }

        private void Start() => StartSet();

        /// <summary>Begin the next set (called once on Start, then by the HUD button).</summary>
        public void StartNextSet()
        {
            if (Current == Phase.Intermission) StartSet();
        }

        private void StartSet()
        {
            if (source == null || source.clip == null)
            {
                Debug.LogError("SetController: AudioSource/clip not assigned.", this);
                return;
            }

            SetNumber++;
            if (crowd != null) crowd.Build(); // picks up any capacity growth bought last intermission

            Current = Phase.Live;
            _bus?.Publish(new SetStarted(SetNumber)); // HypeSystem resets, ability arms

            source.Stop();
            source.time = 0f;
            source.Play();
            _setEndDsp = AudioSettings.dspTime + source.clip.length;
            _started = true;
        }

        private void Update()
        {
            if (Current != Phase.Live || !_started) return;
            if (AudioSettings.dspTime >= _setEndDsp) EndSet();
        }

        private void EndSet()
        {
            Current = Phase.Intermission;
            source.Stop();

            float peak = hype != null ? hype.Peak : 0f;
            float avg = hype != null ? hype.Average : 0f;
            int earned = economy != null ? economy.Bank(peak, avg) : 0;
            LastCashEarned = earned;

            // Coins fly up where the crowd is.
            Vector3 origin = crowd != null ? crowd.transform.position + Vector3.up * 2f : Vector3.up * 2f;
            CoinFlyVfx.Burst(earned, origin);

            _bus?.Publish(new SetEnded(peak, avg, earned));
        }
    }
}
