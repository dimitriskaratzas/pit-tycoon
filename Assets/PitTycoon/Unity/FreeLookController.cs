using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PitTycoon.Domain;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Intermission free-look camera (M4b): Civ-style pan (WASD/arrows + screen-edge push),
    /// middle-mouse yaw orbit, and scroll zoom, all bounded. Drives the pure FreeLookRig and writes
    /// this camera's transform. Runs only while intermission is active AND the CameraRig is not mid
    /// fly-to, so authored moves always win; when it regains control it re-seeds from the landed
    /// pose. Lives on the Main Camera beside CameraRig. Only the analyzer touches AudioSource — this
    /// touches nothing but its own transform and input devices.
    /// </summary>
    public sealed class FreeLookController : MonoBehaviour
    {
        [SerializeField] private CameraRig rig;

        [Header("Pan bounds (world XZ) — seeded by Build Festival Ground")]
        [SerializeField] private float minX = -58f;
        [SerializeField] private float maxX = 58f;
        [SerializeField] private float minZ = -58f;
        [SerializeField] private float maxZ = 58f;
        [SerializeField] private float focusY = 0f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 20f;
        [SerializeField] private float maxDistance = 90f;

        [Header("Speeds")]
        [Tooltip("Pan units/sec per world-unit of zoom distance (pan is faster when zoomed out).")]
        [SerializeField] private float panSpeedPerDistance = 0.5f;
        [Tooltip("Degrees of yaw per pixel of middle-mouse drag.")]
        [SerializeField] private float orbitSpeed = 0.2f;
        [Tooltip("World units of zoom per scroll notch.")]
        [SerializeField] private float zoomSpeed = 3f;
        [Tooltip("Pixels from a screen edge that trigger edge-push panning. 0 disables edge-push.")]
        [SerializeField] private float edgePanMargin = 12f;

        private EventBus _bus;
        private FreeLookRig _look;
        private bool _intermission;   // false = live set. The scene opens live (SetController auto-starts set 1).
        private bool _wasTweening;

        public void Initialize(EventBus bus)
        {
            _bus = bus;
            _look = new FreeLookRig(minX, maxX, minZ, maxZ, minDistance, maxDistance, focusY, panSpeedPerDistance);
            _bus.Subscribe<SetStarted>(OnSetStarted);
            _bus.Subscribe<SetEnded>(OnSetEnded);
        }

        private void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<SetStarted>(OnSetStarted);
            _bus.Unsubscribe<SetEnded>(OnSetEnded);
        }

        private void OnSetStarted(SetStarted e) => _intermission = false;
        private void OnSetEnded(SetEnded e) => _intermission = true;

        private void Update()
        {
            if (_look == null || rig == null) return;

            bool tweening = rig.IsTweening;
            // Just regained control after an authored fly-to: adopt the pose we landed on.
            if (_wasTweening && !tweening)
            {
                Vector3 p = transform.position;
                Vector3 e = transform.eulerAngles;
                _look.SeedFrom(new CameraPose(p.x, p.y, p.z, e.y, e.x));
            }
            _wasTweening = tweening;

            if (!_intermission || tweening) return;

            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // --- Pan direction from keys ---
            float right = 0f, forward = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) right -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) right += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) forward += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forward -= 1f;
            }

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // --- Edge-push adds to the pan direction (suppressed over UI / off-window) ---
            if (edgePanMargin > 0f && mouse != null && !overUI)
            {
                Vector2 m = mouse.position.ReadValue();
                if (m.x >= 0f && m.x <= Screen.width && m.y >= 0f && m.y <= Screen.height)
                {
                    if (m.x <= edgePanMargin) right -= 1f;
                    else if (m.x >= Screen.width - edgePanMargin) right += 1f;
                    if (m.y <= edgePanMargin) forward -= 1f;
                    else if (m.y >= Screen.height - edgePanMargin) forward += 1f;
                }
            }

            if (right != 0f || forward != 0f)
                _look.Pan(right * dt, forward * dt);

            // --- Orbit + zoom (mouse only, gated by UI hover) ---
            if (mouse != null && !overUI)
            {
                if (mouse.middleButton.isPressed)
                {
                    float dx = mouse.delta.ReadValue().x;
                    if (dx != 0f) _look.Orbit(dx * orbitSpeed);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) _look.Zoom(Mathf.Sign(scroll) * zoomSpeed);
            }

            CameraPose pose = _look.Resolve();
            transform.SetPositionAndRotation(
                new Vector3(pose.PosX, pose.PosY, pose.PosZ),
                Quaternion.Euler(pose.Pitch, pose.Yaw, 0f));
        }
    }
}
