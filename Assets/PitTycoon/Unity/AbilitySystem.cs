using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven ability roster. Owns one shared BeatGrid (fed by BeatDetected), builds a
    /// Domain Ability per AbilityDefinition, routes input (Space fires the Spacebar ability;
    /// owned Button abilities fire via HUD buttons / number hotkeys), spawns VFX by VfxKind,
    /// pops the crowd, and handles purchases. Live-phase only. Replaces WhirlpoolAbility.
    /// </summary>
    public sealed class AbilitySystem : MonoBehaviour
    {
        [SerializeField] private List<AbilityDefinition> definitions = new List<AbilityDefinition>();
        [SerializeField] private EconomySystem economy;
        [SerializeField] private CrowdController crowd;
        [SerializeField] private Transform vfxAnchor;
        [SerializeField] private float wooferRadius = 7f;
        [SerializeField] private float crowdPopStrength = 0.9f;

        private readonly BeatGrid _grid = new BeatGrid();
        private readonly List<Ability> _abilities = new List<Ability>();
        private readonly Dictionary<Ability, AbilityDefinition> _defOf = new Dictionary<Ability, AbilityDefinition>();
        private EventBus _bus;
        private bool _live;

        public IReadOnlyList<Ability> Abilities => _abilities;
        public AbilityDefinition DefinitionOf(Ability a) => _defOf[a];
        public HitQuality LastQuality { get; private set; }
        public string LastFiredId { get; private set; }

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _abilities.Clear();
            _defOf.Clear();
            foreach (var d in definitions)
            {
                if (d == null) continue;
                var a = new Ability(d.Id, d.BaseSpike, d.MaxMultiplier, d.ToleranceSeconds, d.Cooldown, d.OwnedFromStart);
                _abilities.Add(a);
                _defOf[a] = d;
            }
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
        private void OnSetStarted(SetStarted e) { _live = true; LastQuality = HitQuality.None; }
        private void OnSetEnded(SetEnded e) => _live = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var a in _abilities) a.Tick(dt);
            if (!_live) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) FireFirstWithTrigger(AbilityTrigger.Spacebar);
            if (kb.digit1Key.wasPressedThisFrame) FireButtonIndex(0);
            if (kb.digit2Key.wasPressedThisFrame) FireButtonIndex(1);
            if (kb.digit3Key.wasPressedThisFrame) FireButtonIndex(2);
        }

        private void FireFirstWithTrigger(AbilityTrigger trigger)
        {
            foreach (var a in _abilities)
                if (_defOf[a].Trigger == trigger) { TryFire(a); return; }
        }

        private void FireButtonIndex(int idx)
        {
            int i = 0;
            foreach (var a in _abilities)
            {
                if (_defOf[a].Trigger != AbilityTrigger.Button) continue;
                if (i == idx) { TryFire(a); return; }
                i++;
            }
        }

        /// <summary>Fire an ability (HUD button or hotkey). No-op if not live / not ready.</summary>
        public void TryFire(Ability a)
        {
            if (!_live || a == null || !a.CanFire) return;
            double now = AudioSettings.dspTime;
            double nearest = _grid.NearestBeatTime(now);
            FireResult r = a.Fire(now, nearest);
            if (!r.Fired) return;

            AbilityDefinition def = _defOf[a];
            LastQuality = r.Quality;
            LastFiredId = def.Id;

            float onBeat01 = (r.Multiplier - 1f) / Mathf.Max(0.0001f, def.MaxMultiplier - 1f);
            Vector3 pos = vfxAnchor != null ? vfxAnchor.position : transform.position;
            switch (def.Vfx)
            {
                case VfxKind.Whirlpool: WhirlpoolVfx.Spawn(pos, onBeat01); break;
                case VfxKind.Woofer: ShockwaveVfx.Spawn(pos, wooferRadius); break;
                case VfxKind.LightBurst: LightBurstVfx.Flash(def.HudColor); break;
            }

            if (crowd != null) crowd.Pop(crowdPopStrength * Mathf.Clamp(r.Multiplier * 0.3f, 0.5f, 2f));
            _bus.Publish(new AbilityFired(def.Id, r.Multiplier, r.HypeAdded));
        }

        // ---- Purchases (intermission) ----
        public bool CanAfford(AbilityDefinition def)
            => def != null && economy != null && economy.CanAfford(def.Cost);

        public bool TryUnlock(AbilityDefinition def)
        {
            if (def == null || economy == null) return false;
            Ability a = _abilities.Find(x => _defOf[x] == def);
            if (a == null || a.Owned) return false;
            if (!economy.TrySpend(def.Cost)) return false;
            a.Unlock();
            _bus?.Publish(new AbilityUnlocked(def.Id));
            return true;
        }
    }
}
