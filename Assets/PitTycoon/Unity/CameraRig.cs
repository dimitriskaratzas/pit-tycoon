using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Drives the camera the rig is attached to between an overview "home" pose (captured at
    /// startup) and arbitrary target poses, with a smoothstep ease. Used by the upgrade
    /// ghost-preview to fly to a venue spot and (only on request) back home. Knows nothing
    /// about upgrades — it just moves the camera.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("Default seconds for a fly-to / return-home tween.")]
        [SerializeField] private float defaultDuration = 0.6f;

        private Vector3 _homePos;
        private Quaternion _homeRot;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _elapsed, _duration;
        private bool _tweening;

        private void Awake()
        {
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

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

        public void ReturnHome() => FlyTo(_homePos, _homeRot, defaultDuration);

        public void SnapHome()
        {
            _tweening = false;
            transform.SetPositionAndRotation(_homePos, _homeRot);
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
