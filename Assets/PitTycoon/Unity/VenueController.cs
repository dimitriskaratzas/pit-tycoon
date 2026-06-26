using System.Collections.Generic;
using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Applies the visible step-change of a passive upgrade to the M2b venue geometry:
    /// scales the stage, scales the PA stacks, brightens the accent lights — by level.
    /// Captures the base values lazily on first Apply so repeated levels stay absolute.
    /// Also provides non-committing Preview/ClearPreview for the upgrade ghost-preview:
    /// Stage/PA show a translucent ghost clone at the target scale (real object untouched);
    /// Lighting ramps the real lights and restores them on clear.
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
        [Tooltip("Translucent material used for ghost-preview clones (wired by Build Upgrade Preview).")]
        [SerializeField] private Material ghostMaterial;

        private Vector3 _stageBase, _paLeftBase, _paRightBase;
        private float[] _lightBase;
        private bool _captured;
        private int _lightLevel;                 // last committed lighting level (for exact revert)
        private readonly List<GameObject> _ghosts = new List<GameObject>();

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
            _lightLevel = level;
            if (accentLights == null || _lightBase == null) return;
            for (int i = 0; i < accentLights.Length; i++)
                if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
        }

        // ---- Ghost preview (non-committing) --------------------------------

        /// <summary>Show the preview for an upgrade kind at an absolute level. Idempotent:
        /// clears any prior preview first.</summary>
        public void Preview(UpgradeKind kind, int level)
        {
            Capture();
            ClearPreview();
            switch (kind)
            {
                case UpgradeKind.Stage:
                    if (stage != null) _ghosts.Add(MakeGhost(stage, _stageBase * (1f + stageStep * level)));
                    break;
                case UpgradeKind.PA:
                    float f = 1f + paStep * level;
                    if (paLeft != null) _ghosts.Add(MakeGhost(paLeft, _paLeftBase * f));
                    if (paRight != null) _ghosts.Add(MakeGhost(paRight, _paRightBase * f));
                    break;
                case UpgradeKind.Lighting:
                    // No mesh to ghost — the brightness IS the preview. Ramp the real lights.
                    if (accentLights != null && _lightBase != null)
                        for (int i = 0; i < accentLights.Length; i++)
                            if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * level;
                    break;
            }
        }

        /// <summary>Destroy ghost clones and restore lighting to the last committed level.</summary>
        public void ClearPreview()
        {
            for (int i = 0; i < _ghosts.Count; i++)
                if (_ghosts[i] != null) Destroy(_ghosts[i]);
            _ghosts.Clear();
            // restore real lights to the committed level (absolute, so exact)
            if (accentLights != null && _lightBase != null)
                for (int i = 0; i < accentLights.Length; i++)
                    if (accentLights[i] != null) accentLights[i].intensity = _lightBase[i] + lightStep * _lightLevel;
        }

        private GameObject MakeGhost(Transform src, Vector3 targetScale)
        {
            var ghost = Instantiate(src.gameObject, src.parent);
            ghost.name = src.name + " (ghost)";
            ghost.transform.localPosition = src.localPosition;
            ghost.transform.localRotation = src.localRotation;
            ghost.transform.localScale = targetScale;
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            if (ghostMaterial != null)
                foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMaterial;
            return ghost;
        }
    }
}
