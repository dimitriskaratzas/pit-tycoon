using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// One stylised light-shaft cone. Sweeps on a sine around its mount, takes its colour from
    /// the Light it hangs under so beam and lit pool always agree, and exposes an intensity that
    /// AtmosphereController drives from hype. All writes go through a MaterialPropertyBlock, so
    /// every beam shares one material with no instancing.
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
        [Tooltip("Local axis the cone swings about. Flip to (1,0,0) for a vertical sweep.")]
        [SerializeField] private Vector3 sweepAxis = new Vector3(0f, 0f, 1f);

        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int IntensityProp = Shader.PropertyToID("_Intensity");

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Quaternion _baseRotation;
        private float _intensity = 1f;
        private float _speedScale = 1f;
        private float _sweepPhase;

        /// <summary>Sweep rate multiplier; 1 is the serialized base speed.</summary>
        public void SetSweepScale(float scale) => _speedScale = Mathf.Max(0f, scale);

        /// <summary>Beam brightness, 0 = invisible.</summary>
        public void SetIntensity(float intensity)
        {
            _intensity = Mathf.Max(0f, intensity);
            Apply();
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            // NOTE: this block is authoritative and is never re-read from the renderer.
            // Renderer.GetPropertyBlock(mpb) OVERWRITES mpb with the renderer's current block,
            // which would silently wipe the colour set below on the first SetIntensity call.
            _mpb = new MaterialPropertyBlock();

            var owner = GetComponentInParent<Light>();
            if (owner != null) _mpb.SetColor(ColorProp, owner.color);

            _baseRotation = transform.localRotation;
            Apply();
        }

        private void Update()
        {
            // Accumulate phase rather than scaling absolute time: AtmosphereController rewrites
            // _speedScale every frame from hype, and scaling Time.time would jump the sine's
            // argument by Time.time * delta-scale each time it changes — the cone would strobe.
            _sweepPhase += Time.deltaTime * sweepSpeed * _speedScale;
            float angle = Mathf.Sin((_sweepPhase + phaseOffset) * Mathf.PI * 2f) * sweepDegrees;
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(angle, sweepAxis);
        }

        private void Apply()
        {
            if (_renderer == null) return;
            _mpb.SetFloat(IntensityProp, _intensity);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
