using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Drives the camera between arbitrary poses with a smoothstep ease (unscaled time). Used by
    /// UpgradePreviewController for authored fly-tos (survey/live rest poses, per-spot preview) and
    /// by FreeLookController, which yields while a tween is in flight (see IsTweening). Knows
    /// nothing about upgrades or input — it just moves the camera.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("Default seconds for a fly-to tween.")]
        [SerializeField] private float defaultDuration = 0.6f;

        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _elapsed, _duration;
        private bool _tweening;

        /// <summary>True while a fly-to tween is in flight; free-look yields during this.</summary>
        public bool IsTweening => _tweening;

        public void FlyTo(Vector3 position, Quaternion rotation) => FlyTo(position, rotation, defaultDuration);

        public void FlyTo(Vector3 position, Quaternion rotation, float duration)
        {
            _fromPos = transform.position;
            _fromRot = transform.rotation;
            _toPos = position;
            _toRot = rotation;
            _elapsed = 0f;
            _duration = Mathf.Max(0.0001f, duration);
            _tweening = true;
        }

        private void Update()
        {
            if (!_tweening) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float e = t * t * (3f - 2f * t);          // smoothstep ease
            transform.SetPositionAndRotation(
                Vector3.Lerp(_fromPos, _toPos, e),
                Quaternion.Slerp(_fromRot, _toRot, e));
            if (t >= 1f) _tweening = false;
        }
    }
}
