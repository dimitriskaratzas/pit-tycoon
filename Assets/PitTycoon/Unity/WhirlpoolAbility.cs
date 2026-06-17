using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Player ability. Feeds detected beats into a BeatGrid (shared on-beat core), so on
    /// fire it scores against the *nearest* beat — last or predicted-next — letting you
    /// anticipate the beat. Publishes AbilityFired (HypeSystem applies the spike) and
    /// spawns a whirlpool VFX sized by hit quality. Live phase only. Space or HUD button.
    /// </summary>
    public sealed class WhirlpoolAbility : MonoBehaviour
    {
        public enum HitQuality { None, Miss, Good, Perfect }

        [SerializeField] private AbilityDefinition definition;
        [Tooltip("Where the VFX spawns (the crowd centre).")]
        [SerializeField] private Transform vfxAnchor;

        private readonly BeatGrid _grid = new BeatGrid();
        private EventBus _bus;
        private float _cooldownRemaining;
        private bool _live;

        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownTotal => definition != null ? definition.Cooldown : 0f;
        public float LastMultiplier { get; private set; }
        public HitQuality LastQuality { get; private set; }
        public bool Ready => _live && _cooldownRemaining <= 0f;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _bus.Subscribe<BeatDetected>(OnBeat);
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<BeatDetected>(OnBeat);
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
        }

        private void OnBeat(BeatDetected e) => _grid.Register(e.Beat.DspTime);
        private void OnSetStarted(SetStarted e) { _live = true; _cooldownRemaining = 0f; LastQuality = HitQuality.None; }
        private void OnSetEnded(SetEnded e) { _live = false; }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) Fire();
        }

        /// <summary>Fire the ability (also called by the HUD button). No-op if not ready.</summary>
        public void Fire()
        {
            if (definition == null || _bus == null || !Ready) return;

            double now = AudioSettings.dspTime;
            double nearestBeat = _grid.NearestBeatTime(now);
            float mult = BeatWindow.Multiplier(
                nearestBeat, now, definition.ToleranceSeconds, definition.MaxMultiplier);
            float hypeAdded = definition.BaseSpike * mult;

            LastMultiplier = mult;
            float onBeat01 = (mult - 1f) / Mathf.Max(0.0001f, definition.MaxMultiplier - 1f);
            LastQuality = onBeat01 >= 0.66f ? HitQuality.Perfect
                        : onBeat01 >= 0.25f ? HitQuality.Good
                        : HitQuality.Miss;
            _cooldownRemaining = definition.Cooldown;

            Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
            WhirlpoolVfx.Spawn(pos, onBeat01);

            _bus.Publish(new AbilityFired(definition.Id, mult, hypeAdded));
        }
    }
}
