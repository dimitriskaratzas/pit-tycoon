using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PitTycoon.Domain;

namespace PitTycoon.Unity.UI
{
    /// <summary>
    /// Live-set HUD: top-center hype bar (fill + peak marker + cur/ceiling), cash, set/phase,
    /// one ability button per owned ability (cooldown overlay + hotkey), a hit-quality popup
    /// and a beat pulse. Reads system state each frame; reacts to SetStarted/AbilityFired/
    /// BeatDetected on the bus. Pure presentation — no game logic.
    /// </summary>
    public sealed class LiveHudView : MonoBehaviour
    {
        [Header("Hype")]
        [SerializeField] private Image hypeFill;            // type = Filled, horizontal
        [SerializeField] private RectTransform peakMarker;  // parented to the hype bar area
        [SerializeField] private TMP_Text hypeText;
        [Header("Readouts")]
        [SerializeField] private TMP_Text cashText;
        [SerializeField] private TMP_Text setText;
        [Header("Abilities")]
        [SerializeField] private RectTransform abilityBar;
        [SerializeField] private AbilityButtonWidget abilityButtonTemplate;
        [Header("Feedback")]
        [SerializeField] private TMP_Text hitQualityText;
        [SerializeField] private Graphic beatPulse;
        [SerializeField] private float hitQualityHold = 0.8f;
        [SerializeField] private float beatPulseDecay = 4f;

        private EventBus _bus;
        private HypeSystem _hype;
        private EconomySystem _economy;
        private SetController _set;
        private AbilitySystem _abilities;

        private readonly List<AbilityButtonWidget> _buttons = new List<AbilityButtonWidget>();
        private float _hitTimer;
        private float _pulse;

        public void Initialize(EventBus bus, HypeSystem hype, EconomySystem economy,
                               SetController set, AbilitySystem abilities)
        {
            _bus = bus; _hype = hype; _economy = economy; _set = set; _abilities = abilities;

            if (abilityButtonTemplate != null) abilityButtonTemplate.gameObject.SetActive(false);
            if (hitQualityText != null) hitQualityText.gameObject.SetActive(false);

            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<AbilityFired>(OnAbilityFired);
            _bus.Subscribe<BeatDetected>(OnBeat);

            BuildAbilityButtons();
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<AbilityFired>(OnAbilityFired);
            _bus.Unsubscribe<BeatDetected>(OnBeat);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        private void OnSetStarted(SetStarted e) => BuildAbilityButtons();

        private void OnAbilityFired(AbilityFired e)
        {
            if (hitQualityText == null || _abilities == null) return;
            HitQuality q = _abilities.LastQuality;
            if (q == HitQuality.None) return;
            hitQualityText.text = q.ToString().ToUpperInvariant() + "!";
            hitQualityText.color = q switch
            {
                HitQuality.Perfect => new Color(0.3f, 1f, 0.4f),
                HitQuality.Good => new Color(1f, 0.9f, 0.3f),
                _ => new Color(0.8f, 0.8f, 0.8f),
            };
            hitQualityText.gameObject.SetActive(true);
            _hitTimer = hitQualityHold;
        }

        private void OnBeat(BeatDetected e) =>
            _pulse = Mathf.Max(_pulse, Mathf.Clamp01(0.4f + e.Beat.Strength));

        /// <summary>Clear and rebuild one button per owned ability (front of the bar = hotkey 1).</summary>
        private void BuildAbilityButtons()
        {
            if (_abilities == null || abilityButtonTemplate == null || abilityBar == null) return;

            foreach (var w in _buttons) if (w != null) Destroy(w.gameObject);
            _buttons.Clear();

            int hotkey = 1;
            foreach (var a in _abilities.Abilities)
            {
                if (!a.Owned) continue;
                AbilityDefinition def = _abilities.DefinitionOf(a);

                AbilityButtonWidget w = Instantiate(abilityButtonTemplate, abilityBar);
                w.gameObject.SetActive(true);
                if (w.Background != null) w.Background.color = def.HudColor;
                if (w.Label != null) w.Label.text = def.DisplayName;
                if (w.Hotkey != null)
                    w.Hotkey.text = def.Trigger == AbilityTrigger.Spacebar ? "Space" : hotkey.ToString();
                if (w.CooldownOverlay != null) w.CooldownOverlay.fillAmount = 0f;

                Ability captured = a;
                if (w.Button != null) w.Button.onClick.AddListener(() => _abilities.TryFire(captured));

                _buttons.Add(w);
                if (def.Trigger == AbilityTrigger.Button) hotkey++;
            }
        }

        private void Update()
        {
            if (_hype != null && hypeFill != null)
            {
                hypeFill.fillAmount = Mathf.Clamp01(_hype.HypeFraction);
                if (hypeText != null) hypeText.text = $"{_hype.Current:0} / {_hype.Ceiling:0}";
                if (peakMarker != null && _hype.Ceiling > 0f)
                {
                    float frac = Mathf.Clamp01(_hype.Peak / _hype.Ceiling);
                    peakMarker.anchorMin = new Vector2(frac, peakMarker.anchorMin.y);
                    peakMarker.anchorMax = new Vector2(frac, peakMarker.anchorMax.y);
                    peakMarker.anchoredPosition = new Vector2(0f, peakMarker.anchoredPosition.y);
                }
            }

            if (_economy != null && cashText != null) cashText.text = $"${_economy.Cash}";
            if (_set != null && setText != null) setText.text = $"SET {_set.SetNumber}";

            if (_abilities != null)
            {
                int i = 0;
                foreach (var a in _abilities.Abilities)
                {
                    if (!a.Owned) continue;
                    if (i >= _buttons.Count) break;
                    AbilityButtonWidget w = _buttons[i++];
                    if (w == null) continue;
                    AbilityDefinition def = _abilities.DefinitionOf(a);
                    if (w.CooldownOverlay != null)
                        w.CooldownOverlay.fillAmount =
                            def.Cooldown > 0f ? Mathf.Clamp01((float)a.CooldownRemaining / def.Cooldown) : 0f;
                    if (w.Button != null) w.Button.interactable = a.CanFire;
                }
            }

            if (_hitTimer > 0f)
            {
                _hitTimer -= Time.deltaTime;
                if (_hitTimer <= 0f && hitQualityText != null) hitQualityText.gameObject.SetActive(false);
            }

            if (_pulse > 0f)
            {
                _pulse = Mathf.MoveTowards(_pulse, 0f, beatPulseDecay * Time.deltaTime);
                if (beatPulse != null) { Color c = beatPulse.color; c.a = _pulse; beatPulse.color = c; }
            }
        }
    }
}
