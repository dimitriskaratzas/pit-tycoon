using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// One stylised light-shaft cone. Sweeps the Light it hangs under — so the shaft and the lit
    /// pool on the crowd move together — takes its colour from that Light, and scales its
    /// brightness by both the hype level (via AtmosphereController) and the Light's own intensity,
    /// so a purchased Lighting upgrade reads on the brightest element in the frame. All writes go
    /// through a MaterialPropertyBlock, so every beam shares one material with no instancing.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class LightBeam : MonoBehaviour
    {
        [Tooltip("Half-arc of the sweep, in degrees.")]
        [SerializeField] private float sweepDegrees = 20f;
        [Tooltip("Sweeps per second at scale 1.")]
        [SerializeField] private float sweepSpeed = 0.25f;
        [Tooltip("Offset into the sweep cycle (0..1) so beams never move in unison.")]
        [SerializeField] private float phaseOffset;
        [Tooltip("Axis the rig light swings about, in its own local space. Y yaws the beam across " +
                 "the pit; flip to (1,0,0) to nod it up and down instead.")]
        [SerializeField] private Vector3 sweepAxis = new Vector3(0f, 1f, 0f);

        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int IntensityProp = Shader.PropertyToID("_Intensity");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Transform _sweepTarget;
        private Light _owner;
        private float _baseLightIntensity = 1f;
        private Quaternion _baseRotation;
        private float _intensity = 1f;
        private float _speedScale = 1f;
        private float _sweepPhase;

        /// <summary>Sweep rate multiplier; 1 is the serialized base speed.</summary>
        public void SetSweepScale(float scale) => _speedScale = Mathf.Max(0f, scale);

        /// <summary>Beam brightness before the owning Light's own intensity is applied.</summary>
        public void SetIntensity(float intensity) => _intensity = Mathf.Max(0f, intensity);

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            // NOTE: this block is authoritative and is never re-read from the renderer.
            // Renderer.GetPropertyBlock(mpb) OVERWRITES mpb with the renderer's current block,
            // which would silently wipe the colour set below on the first Apply call.
            _mpb = new MaterialPropertyBlock();

            _owner = GetComponentInParent<Light>();
            if (_owner != null)
            {
                _mpb.SetColor(ColorProp, _owner.color);
                // Captured before any upgrade is applied, so the ratio below reads 1 at level 0.
                _baseLightIntensity = Mathf.Max(0.0001f, _owner.intensity);
            }

            // Sweep the LIGHT, not just the cone. The cone is a child, so it follows — and the
            // pool of light on the crowd travels with the shaft instead of sitting still while
            // the shaft slides off it.
            _sweepTarget = _owner != null ? _owner.transform : transform;
            _baseRotation = _sweepTarget.localRotation;
            Apply();
        }

        private void Update()
        {
            // Accumulate phase rather than scaling absolute time: AtmosphereController rewrites
            // _speedScale every frame from hype, and scaling Time.time would jump the sine's
            // argument by Time.time * delta-scale each time it changes — the cone would strobe.
            _sweepPhase += Time.deltaTime * sweepSpeed * _speedScale;
            float angle = Mathf.Sin((_sweepPhase + phaseOffset) * Mathf.PI * 2f) * sweepDegrees;
            _sweepTarget.localRotation = _baseRotation * Quaternion.AngleAxis(angle, sweepAxis);

            Apply();
        }

        private void Apply()
        {
            if (_renderer == null) return;
            // VenueController raises the Light's intensity per Lighting-upgrade level; the beam
            // tracks that ratio so the purchase is visible on the shaft, not only on the ground.
            // Read every frame rather than on purchase: nothing notifies us, and it is one float.
            float lightScale = _owner != null ? _owner.intensity / _baseLightIntensity : 1f;
            _mpb.SetFloat(IntensityProp, _intensity * lightScale);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
