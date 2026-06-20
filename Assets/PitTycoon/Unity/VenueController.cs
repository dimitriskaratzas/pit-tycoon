using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Applies the visible step-change of a passive upgrade to the M2b venue geometry:
    /// scales the stage, scales the PA stacks, brightens the accent lights — by level.
    /// Captures the base values lazily on first Apply so repeated levels stay absolute.
    /// </summary>
    public sealed class VenueController : MonoBehaviour
    {
        [SerializeField] private Transform stage;
        [SerializeField] private Transform paLeft;
        [SerializeField] private Transform paRight;
        [SerializeField] private Light[] accentLights;
        [SerializeField] private float stageStep = 0.08f;
        [SerializeField] private float paStep = 0.10f;
        [SerializeField] private float lightStep = 0.6f;

        private Vector3 _stageBase, _paLeftBase, _paRightBase;
        private float[] _lightBase;
        private bool _captured;

        private void Capture()
        {
            if (_captured) return;
            if (stage != null) _stageBase = stage.localScale;
            if (paLeft != null) _paLeftBase = paLeft.localScale;
            if (paRight != null) _paRightBase = paRight.localScale;
            if (accentLights != null)
            {
                _lightBase = new float[accentLights.Length];
                for (int i = 0; i < accentLights.Length; i++)
                    _lightBase[i] = accentLights[i] != null ? accentLights[i].intensity : 0f;
            }
            _captured = true;
        }

        public void Apply(UpgradeKind kind, int level)
        {
            Capture();
            switch (kind)
            {
                case UpgradeKind.Stage: ApplyStage(level); break;
                case UpgradeKind.Lighting: ApplyLighting(level); break;
                case UpgradeKind.PA: ApplyPa(level); break;
            }
        }

        private void ApplyStage(int level)
        {
            if (stage != null) stage.localScale = _stageBase * (1f + stageStep * level);
        }

        private void ApplyPa(int level)
        {
            float f = 1f + paStep * level;
            if (paLeft != null) paLeft.localScale = _paLeftBase * f;
            if (paRight != null) paRight.localScale = _paRightBase * f;
        }

        private void ApplyLighting(int level)
        {
            if (accentLights == null || _lightBase == null) return;
            for (int i = 0; i < accentLights.Length; i++)
                if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
        }
    }
}
