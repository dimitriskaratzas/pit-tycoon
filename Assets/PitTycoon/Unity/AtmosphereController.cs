using UnityEngine;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Owns time of day and the light rig's response to hype. The sky advances dusk -> night
    /// across sets (SetStarted.SetNumber is 1-based), animating over transitionSeconds so the
    /// change is visible rather than a pop. Within a set, hype drives the beam cones only.
    ///
    /// It deliberately does NOT touch accent-light intensity: VenueController owns that field
    /// for the Lighting upgrade, and a per-frame write here would make a paid upgrade invisible.
    /// </summary>
    public sealed class AtmosphereController : MonoBehaviour
    {
        [Header("Day phase")]
        [Tooltip("Maps set progress 0..1 to dusk->night phase 0..1.")]
        [SerializeField] private AnimationCurve dayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Set number at which full night is reached.")]
        [SerializeField] private int setsToNight = 4;
        [SerializeField] private float transitionSeconds = 2f;

        [Header("Sky")]
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Color duskHorizon = new Color(0.98f, 0.55f, 0.28f);
        [SerializeField] private Color duskZenith = new Color(0.24f, 0.26f, 0.48f);
        [SerializeField] private Color nightHorizon = new Color(0.14f, 0.11f, 0.26f);
        [SerializeField] private Color nightZenith = new Color(0.03f, 0.03f, 0.09f);

        [Header("Fog")]
        [SerializeField] private Color duskFog = new Color(0.42f, 0.34f, 0.36f);
        [SerializeField] private Color nightFog = new Color(0.07f, 0.06f, 0.12f);
        [SerializeField] private float duskFogDensity = 0.010f;
        [SerializeField] private float nightFogDensity = 0.020f;

        [Header("Ambient + sun")]
        [SerializeField] private Light sun;
        [SerializeField] private Color duskAmbient = new Color(0.30f, 0.28f, 0.34f);
        [SerializeField] private Color nightAmbient = new Color(0.10f, 0.10f, 0.16f);
        [SerializeField] private Color duskSunColor = new Color(1f, 0.78f, 0.52f);
        [SerializeField] private Color nightSunColor = new Color(0.50f, 0.58f, 0.85f);
        [SerializeField] private float duskSunIntensity = 0.9f;
        [SerializeField] private float nightSunIntensity = 0.25f;

        [Header("Beams (hype-driven)")]
        [SerializeField] private LightBeam[] beams;
        [SerializeField] private float beamIntensityLow = 0.35f;
        [SerializeField] private float beamIntensityHigh = 1.6f;
        [SerializeField] private float beamSweepLow = 0.5f;
        [SerializeField] private float beamSweepHigh = 2.4f;

        private static readonly int HorizonProp = Shader.PropertyToID("_HorizonColor");
        private static readonly int ZenithProp = Shader.PropertyToID("_ZenithColor");
        private static readonly int StarProp = Shader.PropertyToID("_StarStrength");

        private EventBus _bus;
        private IHypeMeter _hype;
        private Material _skyInstance;
        private float _phase;         // current, animated
        private float _phaseTarget;   // set by SetStarted

        public void Initialize(EventBus bus, IHypeMeter hype)
        {
            if (_bus != null) _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus = bus;
            if (_bus != null) _bus.Subscribe<SetStarted>(OnSetStarted);
            _hype = hype;
        }

        private void Awake()
        {
            // A runtime COPY: writing to the skybox material asset persists after Play mode
            // exits and would permanently leave the scene's sky at the last set's look.
            if (skyMaterial != null)
            {
                _skyInstance = new Material(skyMaterial);
                RenderSettings.skybox = _skyInstance;
            }
            ApplyPhase(0f);
        }

        private void OnDestroy()
        {
            if (_bus != null) _bus.Unsubscribe<SetStarted>(OnSetStarted);
            if (_skyInstance != null)
            {
                RenderSettings.skybox = skyMaterial;
                Destroy(_skyInstance);
            }
        }

        private void OnSetStarted(SetStarted e)
        {
            float progress = setsToNight > 0
                ? Mathf.Clamp01((e.SetNumber - 1) / (float)setsToNight)
                : 1f;
            _phaseTarget = Mathf.Clamp01(dayCurve.Evaluate(progress));
        }

        private void Update()
        {
            if (!Mathf.Approximately(_phase, _phaseTarget))
            {
                float step = transitionSeconds > 0f ? Time.deltaTime / transitionSeconds : 1f;
                _phase = Mathf.MoveTowards(_phase, _phaseTarget, step);
                ApplyPhase(_phase);
            }

            if (beams == null || beams.Length == 0) return;
            float h = _hype != null ? Mathf.Clamp01(_hype.HypeFraction) : 0f;
            float intensity = Mathf.Lerp(beamIntensityLow, beamIntensityHigh, h);
            float sweep = Mathf.Lerp(beamSweepLow, beamSweepHigh, h);
            for (int i = 0; i < beams.Length; i++)
            {
                if (beams[i] == null) continue;
                beams[i].SetIntensity(intensity);
                beams[i].SetSweepScale(sweep);
            }
        }

        private void ApplyPhase(float t)
        {
            if (_skyInstance != null)
            {
                _skyInstance.SetColor(HorizonProp, Color.Lerp(duskHorizon, nightHorizon, t));
                _skyInstance.SetColor(ZenithProp, Color.Lerp(duskZenith, nightZenith, t));
                _skyInstance.SetFloat(StarProp, t);
            }

            RenderSettings.fogColor = Color.Lerp(duskFog, nightFog, t);
            RenderSettings.fogDensity = Mathf.Lerp(duskFogDensity, nightFogDensity, t);
            RenderSettings.ambientLight = Color.Lerp(duskAmbient, nightAmbient, t);

            if (sun != null)
            {
                sun.color = Color.Lerp(duskSunColor, nightSunColor, t);
                sun.intensity = Mathf.Lerp(duskSunIntensity, nightSunIntensity, t);
            }
        }
    }
}
