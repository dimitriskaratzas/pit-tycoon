using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>Cosmetic idle motion for structure prefabs (M4d): gently sways/pans a child
    /// transform (banner cloth, strobe heads) with a sine oscillation. Purely visual —
    /// no EventBus, no Domain, safe to leave running during live sets.</summary>
    public sealed class IdleMotion : MonoBehaviour
    {
        [SerializeField, Tooltip("Transform to animate; defaults to this transform.")]
        private Transform target;
        [SerializeField, Tooltip("Local rotation axis of the sway/pan.")]
        private Vector3 rotationAxis = Vector3.up;
        [SerializeField, Tooltip("Peak rotation in degrees (0 disables rotation).")]
        private float rotationAmplitude = 10f;
        [SerializeField, Tooltip("Peak vertical bob in local units (0 disables bobbing).")]
        private float bobAmplitude = 0f;
        [SerializeField, Tooltip("Oscillations per second.")]
        private float speed = 0.25f;
        [SerializeField, Tooltip("Phase offset in seconds so neighbours don't sync.")]
        private float phase = 0f;

        private Quaternion _baseRotation;
        private Vector3 _basePosition;

        private void Awake()
        {
            if (target == null) target = transform;
            _baseRotation = target.localRotation;
            _basePosition = target.localPosition;
        }

        private void Update()
        {
            float s = Mathf.Sin((Time.time + phase) * speed * Mathf.PI * 2f);
            if (rotationAmplitude != 0f)
                target.localRotation = _baseRotation * Quaternion.AngleAxis(s * rotationAmplitude, rotationAxis);
            if (bobAmplitude != 0f)
                target.localPosition = _basePosition + Vector3.up * (s * bobAmplitude);
        }
    }
}
