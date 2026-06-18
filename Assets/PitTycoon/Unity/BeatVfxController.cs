using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Bus-driven VFX conductor. On a big beat (strength over threshold, rate-limited) it
    /// throws a shockwave + onomatopoeia from the stage; on a great ability hit (multiplier
    /// over threshold) it throws a "POW!" at the pit. Also sets the comic override materials
    /// on the whirlpool/coin VFX. Live-phase only. Wired by the editor script; Initialize(bus)
    /// called by GameBootstrap.
    /// </summary>
    public sealed class BeatVfxController : MonoBehaviour
    {
        [SerializeField] private Transform stageAnchor;
        [SerializeField] private float strengthThreshold = 0.6f;
        [SerializeField] private float multiplierThreshold = 4f;
        [SerializeField] private float minInterval = 1.5f;
        [SerializeField] private float shockwaveRadius = 9f;
        [SerializeField] private Material whirlpoolMaterial;
        [SerializeField] private Material coinMaterial;

        private static readonly string[] Words = { "DON!", "POW!", "BOOM!", "WHAM!", "BAM!" };
        private static readonly Color[] Accents =
        {
            new Color(1f, 0.69f, 0.24f),
            new Color(0.88f, 0.27f, 0.49f),
            new Color(0.21f, 0.79f, 0.88f),
            Color.white,
        };

        private EventBus _bus;
        private bool _live;
        private double _lastBigBeat = -999.0;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            WhirlpoolVfx.OverrideMaterial = whirlpoolMaterial;
            CoinFlyVfx.OverrideMaterial = coinMaterial;
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
            _bus.Subscribe<BeatDetected>(OnBeat);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
            _bus.Unsubscribe<BeatDetected>(OnBeat);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
        }

        private void OnSetStarted(SetStarted e) => _live = true;
        private void OnSetEnded(SetEnded e) => _live = false;

        private void OnBeat(BeatDetected e)
        {
            if (!_live || e.Beat.Strength < strengthThreshold) return;
            double now = AudioSettings.dspTime;
            if (now - _lastBigBeat < minInterval) return;
            _lastBigBeat = now;

            Vector3 stagePos = stageAnchor != null ? stageAnchor.position : Vector3.zero;
            ShockwaveVfx.Spawn(stagePos, shockwaveRadius);
            SpawnWord(stagePos + Vector3.up * 3f + new Vector3(Random.Range(-2f, 2f), 0f, 0f));
        }

        private void OnAbilityFired(AbilityFired e)
        {
            if (!_live || e.Multiplier < multiplierThreshold) return;
            SpawnWord(Vector3.up * 2.5f + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)));
        }

        private void SpawnWord(Vector3 pos)
        {
            string w = Words[Random.Range(0, Words.Length)];
            Color c = Accents[Random.Range(0, Accents.Length)];
            OnomatopoeiaVfx.Spawn(w, c, pos);
        }
    }
}
