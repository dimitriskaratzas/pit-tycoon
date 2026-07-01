# Free-Look Survey Camera (M4b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a bounded, Civ-6-style free-look camera (pan / orbit-yaw / zoom) to the intermission, layered on the existing `CameraRig`, that always yields to authored fly-tos and is off during live sets.

**Architecture:** A pure `Domain/FreeLookRig` owns the orbit state (focus point, yaw, distance, held pitch) and all clamping/resolve/seed math, unit-tested outside the Editor. A thin `Unity/FreeLookController` MonoBehaviour on the Main Camera polls the Input System, feeds the rig, and writes the transform — only when `intermission && !CameraRig.IsTweening`. `CameraRig` gains an `IsTweening` getter; `GameBootstrap` and `FestivalGroundSetup` wire the controller.

**Tech Stack:** Unity 6 LTS (URP), C#; `PitTycoon.Domain` (netstandard2.1, NUnit-tested); new Input System (`UnityEngine.InputSystem`), UGUI (`UnityEngine.EventSystems`).

## Global Constraints

- **Domain purity:** `FreeLookRig`/`CameraPose` live in `Assets/PitTycoon/Domain/`, reference **no UnityEngine** types (floats only), and are unit-tested. No new `EventBus` events — reuse existing `SetStarted`/`SetEnded`.
- **Only automated gate:** `dotnet test PitTycoon.Domain.slnx` must stay green. It is **73 passing** today and must end at **~80+** (the new `FreeLookRigTests`). Unity C# only compiles in the Editor — transcribe MonoBehaviour/editor code carefully; it is verified in the manual checkpoint (Task 7), not by the test runner.
- **Input System only:** poll `Keyboard.current` / `Mouse.current`, always null-guarded (they are null when no device is present), matching `AbilitySystem`/`DebugHud`.
- **Optional + null-guarded wiring:** `GameBootstrap.freeLook` is optional, **not** added to the bootstrap null-guard block (same discipline as `preview`/`builds`), so the game runs before `Build Festival Ground` wires it.
- **Behaviour rules:** free-look runs only when `intermission && !rig.IsTweening`; authored fly-tos (`GoToSurvey`/`GoToLive`/build & upgrade previews) always win; pitch is **held** (no pitch input); edge-push is suppressed over UI and when the cursor leaves the window; `edgePanMargin <= 0` disables edge-push.
- **Commits:** imperative present tense, **no `Co-Authored-By` trailer**.

---

### Task 1: `FreeLookRig` + `CameraPose` (Domain, TDD)

**Files:**
- Create: `Assets/PitTycoon/Domain/FreeLookRig.cs`
- Test: `tests/PitTycoon.Domain.Tests/FreeLookRigTests.cs`

**Interfaces:**
- Consumes: nothing (pure, leaf type).
- Produces:
  - `readonly struct CameraPose(float posX, float posY, float posZ, float yaw, float pitch)` with `float PosX,PosY,PosZ,Yaw,Pitch { get; }`.
  - `sealed class FreeLookRig`:
    - ctor `FreeLookRig(float minX, float maxX, float minZ, float maxZ, float minDistance, float maxDistance, float focusY, float panSpeedPerDistance)`
    - props `float FocusX, FocusZ, Yaw, Distance, Pitch { get; private set; }`
    - `void Pan(float right, float forward)` — screen-relative, scaled by `panSpeedPerDistance * Distance`, focus clamped to the XZ rect
    - `void Orbit(float deltaYaw)` — yaw wraps 0..360
    - `void Zoom(float delta)` — `Distance = clamp(Distance - delta, min, max)` (positive delta zooms in)
    - `CameraPose Resolve()`
    - `void SeedFrom(CameraPose pose)` — inverse of `Resolve` (clamped)

- [ ] **Step 1: Write the failing tests**

Create `tests/PitTycoon.Domain.Tests/FreeLookRigTests.cs`:

```csharp
using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class FreeLookRigTests
    {
        // Wide symmetric bounds so clamps don't fire; maxDistance=10 => default Distance=10.
        private static FreeLookRig Wide(float panFactor = 0.1f) =>
            new FreeLookRig(-60f, 60f, -60f, 60f, minDistance: 2f, maxDistance: 10f,
                            focusY: 0f, panSpeedPerDistance: panFactor);

        [Test]
        public void Defaults_CenterFocus_MaxDistance_ZeroYaw()
        {
            var r = Wide();
            Assert.That(r.FocusX, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(r.FocusZ, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(r.Distance, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(r.Yaw, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Resolve_AtYawZero_CameraSitsBehindAndAboveFocus()
        {
            var r = Wide();                       // Distance 10, Pitch 45
            var p = r.Resolve();
            // horiz = height = 10*cos45 = 7.0710678
            Assert.That(p.PosX, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(p.PosZ, Is.EqualTo(-7.0710678f).Within(1e-3f));
            Assert.That(p.PosY, Is.EqualTo(7.0710678f).Within(1e-3f));
            Assert.That(p.Yaw, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(p.Pitch, Is.EqualTo(45f).Within(1e-4f));
        }

        [Test]
        public void Orbit_Ninety_MovesCameraToMinusX()
        {
            var r = Wide();
            r.Orbit(90f);
            var p = r.Resolve();
            Assert.That(p.PosX, Is.EqualTo(-7.0710678f).Within(1e-3f));
            Assert.That(p.PosZ, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Orbit_WrapsPast360AndBelowZero()
        {
            var r = Wide();
            r.Orbit(370f);
            Assert.That(r.Yaw, Is.EqualTo(10f).Within(1e-4f));
            r.Orbit(-30f);
            Assert.That(r.Yaw, Is.EqualTo(340f).Within(1e-4f));
        }

        [Test]
        public void Pan_AtYawZero_ForwardMovesFocusPlusZ_RightUnchangedX()
        {
            var r = Wide(panFactor: 0.1f);        // factor*Distance = 0.1*10 = 1
            r.Pan(right: 0f, forward: 3f);        // dz = 3*1 = 3
            Assert.That(r.FocusZ, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(r.FocusX, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Pan_AtYawNinety_ForwardMovesFocusPlusX()
        {
            var r = Wide(panFactor: 0.1f);
            r.Orbit(90f);
            r.Pan(right: 0f, forward: 3f);        // dx = 3*1 = 3
            Assert.That(r.FocusX, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(r.FocusZ, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void Pan_ClampsFocusToRectangle()
        {
            var r = Wide();
            r.Pan(right: 0f, forward: 100000f);
            Assert.That(r.FocusZ, Is.EqualTo(60f).Within(1e-4f));   // maxZ
            r.Pan(right: -100000f, forward: 0f);
            Assert.That(r.FocusX, Is.EqualTo(-60f).Within(1e-4f));  // minX
        }

        [Test]
        public void Zoom_ClampsAtMinAndMax()
        {
            var r = Wide();
            r.Zoom(100000f);
            Assert.That(r.Distance, Is.EqualTo(2f).Within(1e-4f));   // minDistance
            r.Zoom(-100000f);
            Assert.That(r.Distance, Is.EqualTo(10f).Within(1e-4f));  // maxDistance
        }

        [Test]
        public void SeedFrom_Resolve_RoundTrips()
        {
            var r = Wide(panFactor: 0.1f);
            r.Orbit(37f);
            r.Zoom(3f);                 // Distance 7
            r.Pan(2f, -1.5f);
            float fx = r.FocusX, fz = r.FocusZ, yaw = r.Yaw, dist = r.Distance, pitch = r.Pitch;
            var pose = r.Resolve();
            r.SeedFrom(pose);
            Assert.That(r.FocusX, Is.EqualTo(fx).Within(1e-2f));
            Assert.That(r.FocusZ, Is.EqualTo(fz).Within(1e-2f));
            Assert.That(r.Yaw, Is.EqualTo(yaw).Within(1e-2f));
            Assert.That(r.Distance, Is.EqualTo(dist).Within(1e-2f));
            Assert.That(r.Pitch, Is.EqualTo(pitch).Within(1e-2f));
        }

        [Test]
        public void SeedFrom_ClampsOutOfBoundsPoseIntoBounds()
        {
            var r = Wide();
            // A pose whose ground focus is far outside the rect => focus clamps.
            var far = new CameraPose(1000f, 7.07f, 1000f, 0f, 45f);
            r.SeedFrom(far);
            Assert.That(r.FocusX, Is.LessThanOrEqualTo(60f));
            Assert.That(r.FocusZ, Is.LessThanOrEqualTo(60f));
        }

        [Test]
        public void Constructor_RejectsBadRanges()
        {
            Assert.That(() => new FreeLookRig(10f, -10f, 0f, 1f, 1f, 2f, 0f, 0.1f),
                        Throws.TypeOf<System.ArgumentException>());          // maxX < minX
            Assert.That(() => new FreeLookRig(-1f, 1f, 0f, 1f, 0f, 2f, 0f, 0.1f),
                        Throws.TypeOf<System.ArgumentException>());          // minDistance <= 0
            Assert.That(() => new FreeLookRig(-1f, 1f, 0f, 1f, 5f, 2f, 0f, 0.1f),
                        Throws.TypeOf<System.ArgumentException>());          // maxDistance < minDistance
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: FAIL — `FreeLookRig`/`CameraPose` do not exist (compile error).

- [ ] **Step 3: Write the implementation**

Create `Assets/PitTycoon/Domain/FreeLookRig.cs`:

```csharp
using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// A resolved camera pose from <see cref="FreeLookRig"/> — plain floats so Domain stays
    /// UnityEngine-free. Consumers build Vector3(PosX,PosY,PosZ) and Quaternion.Euler(Pitch,Yaw,0).
    /// </summary>
    public readonly struct CameraPose
    {
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }
        public float Yaw { get; }
        public float Pitch { get; }

        public CameraPose(float posX, float posY, float posZ, float yaw, float pitch)
        {
            PosX = posX; PosY = posY; PosZ = posZ; Yaw = yaw; Pitch = pitch;
        }
    }

    /// <summary>
    /// Bounded orbit/pan/zoom state for the intermission free-look camera (M4b). Pure C# — no
    /// UnityEngine, fully unit-tested. Owns a focus point on the ground plane, a yaw, a zoom
    /// distance, and a held pitch (no pitch input; adopted via SeedFrom). FreeLookController feeds
    /// it input deltas and applies Resolve() to the camera; authored fly-tos bypass it and it
    /// re-adopts the landed pose through SeedFrom. All clamping lives here.
    /// </summary>
    public sealed class FreeLookRig
    {
        public float FocusX { get; private set; }
        public float FocusZ { get; private set; }
        public float Yaw { get; private set; }
        public float Distance { get; private set; }
        public float Pitch { get; private set; }

        private readonly float _minX, _maxX, _minZ, _maxZ;
        private readonly float _minDistance, _maxDistance;
        private readonly float _focusY;
        private readonly float _panSpeedPerDistance;

        private const float Deg2Rad = 0.017453292519943295f;

        public FreeLookRig(float minX, float maxX, float minZ, float maxZ,
                           float minDistance, float maxDistance, float focusY, float panSpeedPerDistance)
        {
            if (maxX < minX) throw new ArgumentException("maxX < minX");
            if (maxZ < minZ) throw new ArgumentException("maxZ < minZ");
            if (minDistance <= 0f) throw new ArgumentException("minDistance must be > 0");
            if (maxDistance < minDistance) throw new ArgumentException("maxDistance < minDistance");

            _minX = minX; _maxX = maxX; _minZ = minZ; _maxZ = maxZ;
            _minDistance = minDistance; _maxDistance = maxDistance;
            _focusY = focusY; _panSpeedPerDistance = panSpeedPerDistance;

            // Sensible defaults; normally overwritten by SeedFrom on first activation.
            FocusX = Clamp((minX + maxX) * 0.5f, minX, maxX);
            FocusZ = Clamp((minZ + maxZ) * 0.5f, minZ, maxZ);
            Distance = Clamp(maxDistance, minDistance, maxDistance);
            Yaw = 0f;
            Pitch = 45f;
        }

        /// <summary>Screen-relative pan: (right, forward) rotated by yaw into world XZ, scaled by
        /// panSpeedPerDistance*Distance (faster when zoomed out), added to focus, clamped to the rect.</summary>
        public void Pan(float right, float forward)
        {
            float yawRad = Yaw * Deg2Rad;
            float sin = (float)Math.Sin(yawRad);
            float cos = (float)Math.Cos(yawRad);
            float scale = _panSpeedPerDistance * Distance;
            float dx = (forward * sin + right * cos) * scale;
            float dz = (forward * cos - right * sin) * scale;
            FocusX = Clamp(FocusX + dx, _minX, _maxX);
            FocusZ = Clamp(FocusZ + dz, _minZ, _maxZ);
        }

        public void Orbit(float deltaYaw) => Yaw = Wrap360(Yaw + deltaYaw);

        public void Zoom(float delta) => Distance = Clamp(Distance - delta, _minDistance, _maxDistance);

        public CameraPose Resolve()
        {
            float yawRad = Yaw * Deg2Rad;
            float pitchRad = Pitch * Deg2Rad;
            float horiz = Distance * (float)Math.Cos(pitchRad);
            float height = Distance * (float)Math.Sin(pitchRad);
            float camX = FocusX - (float)Math.Sin(yawRad) * horiz;
            float camZ = FocusZ - (float)Math.Cos(yawRad) * horiz;
            float camY = _focusY + height;
            return new CameraPose(camX, camY, camZ, Yaw, Pitch);
        }

        /// <summary>Adopt an arbitrary camera pose (inverse of Resolve): recover distance from the
        /// pose height, project back to the focus, clamp into bounds. Called when free-look regains
        /// control after an authored fly-to so it continues from the landed pose.</summary>
        public void SeedFrom(CameraPose pose)
        {
            Yaw = Wrap360(pose.Yaw);
            Pitch = pose.Pitch;
            float pitchRad = Pitch * Deg2Rad;
            float yawRad = Yaw * Deg2Rad;
            float sinP = (float)Math.Sin(pitchRad);
            float dist = sinP > 1e-4f ? (pose.PosY - _focusY) / sinP : _maxDistance;
            Distance = Clamp(dist, _minDistance, _maxDistance);
            float horiz = Distance * (float)Math.Cos(pitchRad);
            FocusX = Clamp(pose.PosX + (float)Math.Sin(yawRad) * horiz, _minX, _maxX);
            FocusZ = Clamp(pose.PosZ + (float)Math.Cos(yawRad) * horiz, _minZ, _maxZ);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Wrap360(float d) { d %= 360f; return d < 0f ? d + 360f : d; }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0, Passed: 84` (73 prior + 11 new). Any count ≥ 84 with 0 failures is fine.

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Domain/FreeLookRig.cs Assets/PitTycoon/Domain/FreeLookRig.cs.meta tests/PitTycoon.Domain.Tests/FreeLookRigTests.cs
git commit -m "feat(domain): FreeLookRig — bounded orbit/pan/zoom camera state with tests"
```
(The `.meta` may not exist yet if Unity hasn't imported; `git add` it if present, otherwise it is committed at the Task 7 checkpoint.)

---

### Task 2: `CameraRig` — expose `IsTweening`, drop dead home-pose members

**Files:**
- Modify: `Assets/PitTycoon/Unity/CameraRig.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public bool IsTweening { get; }` on `CameraRig` (read by Task 3).

- [ ] **Step 1: Confirm the dead members are unreferenced**

Run: `grep -rn "SnapHome\|ReturnHome\|_homePos\|_homeRot" Assets/PitTycoon`
Expected: matches only inside `CameraRig.cs` itself (no external callers — `UpgradePreviewController.ReturnHome` routes to `GoToSurvey`, not `rig.ReturnHome`). If any *other* file references them, STOP and report — do not delete.

- [ ] **Step 2: Edit `CameraRig.cs`**

Add `public bool IsTweening => _tweening;` and remove `SnapHome()`, `ReturnHome()`, the `_homePos`/`_homeRot` fields, and the now-empty `Awake`. Result:

```csharp
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
```

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/CameraRig.cs
git commit -m "refactor(unity): expose CameraRig.IsTweening; remove dead home-pose members"
```

---

### Task 3: `FreeLookController` MonoBehaviour

**Files:**
- Create: `Assets/PitTycoon/Unity/FreeLookController.cs`

**Interfaces:**
- Consumes: `PitTycoon.Domain.FreeLookRig`, `PitTycoon.Domain.CameraPose` (Task 1); `CameraRig.IsTweening` (Task 2); `EventBus`, `SetStarted`, `SetEnded` (existing Domain).
- Produces: `public void Initialize(EventBus bus)` (called by Task 4); serialized fields wired by Task 5 by these **exact names**: `rig`, `minX`, `maxX`, `minZ`, `maxZ`, `focusY`, `minDistance`, `maxDistance`, `panSpeedPerDistance`, `orbitSpeed`, `zoomSpeed`, `edgePanMargin`.

- [ ] **Step 1: Create `Assets/PitTycoon/Unity/FreeLookController.cs`**

```csharp
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
```

- [ ] **Step 2: Sanity-run the Domain suite (regression guard — this file isn't compiled by it)**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — still `Failed: 0` (unchanged count from Task 1). This only confirms Domain is untouched; the controller compiles in the Editor at Task 7.

- [ ] **Step 3: Commit**

```bash
git add Assets/PitTycoon/Unity/FreeLookController.cs
git commit -m "feat(unity): FreeLookController — Civ-style pan/orbit/zoom driving FreeLookRig"
```

---

### Task 4: Wire `FreeLookController` through `GameBootstrap`

**Files:**
- Modify: `Assets/PitTycoon/Unity/GameBootstrap.cs`

**Interfaces:**
- Consumes: `FreeLookController.Initialize(EventBus)` (Task 3).
- Produces: serialized field named exactly `freeLook` on `GameBootstrap` (wired by Task 5).

- [ ] **Step 1: Add the optional field**

In `GameBootstrap.cs`, after the `builds` field (line ~28), add:

```csharp
        [Tooltip("Optional — wired by Build Festival Ground. Null = no free-look camera.")]
        [SerializeField] private FreeLookController freeLook;
```

- [ ] **Step 2: Initialize it after the other systems**

In `Awake`, immediately after `builds?.Initialize(Bus);` (line ~53), add:

```csharp
            freeLook?.Initialize(Bus);
```

Do **not** add `freeLook` to the null-guard `if (analyzer == null || ...)` block — it is optional, exactly like `preview` and `builds`.

- [ ] **Step 3: Sanity-run the Domain suite**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0` (unchanged). (Editor compile verified at Task 7.)

- [ ] **Step 4: Commit**

```bash
git add Assets/PitTycoon/Unity/GameBootstrap.cs
git commit -m "feat(unity): initialize optional FreeLookController from GameBootstrap"
```

---

### Task 5: Add + seed `FreeLookController` in `FestivalGroundSetup`

**Files:**
- Modify: `Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs`

**Interfaces:**
- Consumes: `FreeLookController` serialized field names (Task 3: `rig`, `minX/maxX/minZ/maxZ`, `focusY`, `minDistance/maxDistance`, `panSpeedPerDistance`, `orbitSpeed`, `zoomSpeed`, `edgePanMargin`); `GameBootstrap.freeLook` (Task 4); the existing `SetRef`/`SetVector3` helpers and the resolved `cam`/`boot` locals.
- Produces: nothing (terminal editor wiring).

- [ ] **Step 1: Add a `SetFloat` helper**

Next to `SetVector3` (near line 145), add:

```csharp
        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"FestivalGroundSetup: field '{field}' not found on {so.targetObject.GetType().Name}.");
        }
```

- [ ] **Step 2: Add + seed the controller before `AssetDatabase.SaveAssets()`**

After the `GameBootstrap` refs block (step 8, line ~105) and before `EditorUtility.SetDirty(spotsCtl);`, insert a step 9:

```csharp
            // 9. FreeLookController on the Main Camera (M4b): bounds from the ~120u ground, zoom
            //    around the survey pose's distance to the ground centre. Idempotent.
            var freeLook = cam.GetComponent<FreeLookController>();
            if (freeLook == null) freeLook = cam.gameObject.AddComponent<FreeLookController>();

            var camRig = cam.GetComponent<CameraRig>();
            if (camRig != null) SetRef(freeLook, "rig", camRig);
            else Debug.LogWarning("FestivalGroundSetup: no CameraRig on the camera. Run Build Upgrade Preview first.");

            var flSo = new SerializedObject(freeLook);
            SetFloat(flSo, "minX", -58f);
            SetFloat(flSo, "maxX", 58f);
            SetFloat(flSo, "minZ", -58f);
            SetFloat(flSo, "maxZ", 58f);
            SetFloat(flSo, "focusY", 0f);
            SetFloat(flSo, "minDistance", 20f);
            SetFloat(flSo, "maxDistance", 90f);
            SetFloat(flSo, "panSpeedPerDistance", 0.5f);
            SetFloat(flSo, "orbitSpeed", 0.2f);
            SetFloat(flSo, "zoomSpeed", 3f);
            SetFloat(flSo, "edgePanMargin", 12f);
            flSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(freeLook);

            if (boot != null) SetRef(boot, "freeLook", freeLook);
```

- [ ] **Step 3: Update the completion log line**

Change the final `Debug.Log(...)` message to mention the camera, e.g. append:
`" Free-look bounds/speeds live on the Main Camera's FreeLookController."`

- [ ] **Step 4: Sanity-run the Domain suite**

Run: `dotnet test PitTycoon.Domain.slnx`
Expected: PASS — `Failed: 0` (editor code is not compiled by the test runner; this only confirms no Domain regression).

- [ ] **Step 5: Commit**

```bash
git add Assets/PitTycoon/Unity/Editor/FestivalGroundSetup.cs
git commit -m "feat(editor): FestivalGroundSetup adds + seeds the free-look camera"
```

---

### Task 6: SETUP.md — free-look camera section

**Files:**
- Modify: `SETUP.md` (append a section at the end)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Append the section**

Add at the end of `SETUP.md`:

```markdown
## Free-look camera (M4b — Civ-style intermission camera)

Adds a bounded pan / orbit / zoom camera during the intermission. Layers on the M4a
festival ground; authored fly-tos still win and free-look is off during live sets.

**Build steps**
1. Pull the branch and let Unity recompile (new `FreeLookRig` in Domain, `FreeLookController`
   in Unity, edited `CameraRig`/`GameBootstrap`/`FestivalGroundSetup`).
2. Run, in order: **Pit Tycoon → Build HUD → Build Upgrade Preview → Build Festival Ground**.
   The last step now also adds a `FreeLookController` to the Main Camera and seeds its bounds
   and speeds, and wires `GameBootstrap.freeLook`.
3. Commit the modified scene + any new `.meta` files.

**Play-test verification**
- In intermission: `W/A/S/D`/arrows pan across the ground, clamped at the edges (can't roam off).
- Push the cursor to a screen edge (not the shop side) → the camera scrolls that way; center it → stops.
- Middle-mouse drag orbits the yaw; scroll zooms between min/max; tilt stays fixed.
- Scrolling over the shop panel scrolls the list, not the camera.
- Select a build spot → camera flies there (free-look yields); on arrival you can orbit/pan/zoom;
  **⌂ Overview** flies back to the survey pose.
- **Start Next Set** → camera flies to the live pose and free-look is dead for the whole set.
- Regression: M1–M4a all still work; F1 overlay intact.

**Tuning** (Main Camera → `FreeLookController`, live-editable in Play):
pan rect `minX/maxX/minZ/maxZ`, `minDistance/maxDistance`, `panSpeedPerDistance`, `orbitSpeed`,
`zoomSpeed`, `edgePanMargin` (set to 0 to disable edge-push).
```

- [ ] **Step 2: Commit**

```bash
git add SETUP.md
git commit -m "docs: SETUP free-look camera — build steps, verification, tuning"
```

---

### Task 7: Manual Unity checkpoint (needs the user)

**Files:** none (Editor + play-test).

**Interfaces:**
- Consumes: all prior tasks.
- Produces: committed scene/`.meta` changes.

This task cannot be automated — the user drives the Editor. Provide these instructions and wait.

- [ ] **Step 1: Recompile** — open the project; confirm no compile errors (new/edited scripts).
- [ ] **Step 2: Run builders** — `Pit Tycoon → Build HUD → Build Upgrade Preview → Build Festival Ground`.
- [ ] **Step 3: Play-test** the full checklist from the SETUP.md M4b section (pan/orbit/zoom + clamps, edge-push, UI-scroll gate, authored fly-to yields + resumes, live-set disable, ⌂ Overview recenter, M1–M4a regression).
- [ ] **Step 4: Tune** pan bounds/speeds/`edgePanMargin` on the `FreeLookController` to taste.
- [ ] **Step 5: Commit** the modified scene, the `FreeLookRig.cs.meta`/`FreeLookController.cs.meta`, and any other generated `.meta`:

```bash
git add -A
git commit -m "feat(checkpoint): M4b free-look camera wired in-scene + .meta files"
```

---

## Self-Review

**Spec coverage** (checked against `2026-07-01-pit-tycoon-freelook-camera-m4b-design.md`):
- Civ-style controls (keys + MMB orbit + scroll zoom + edge-push) → Task 3. ✅
- Fixed/held pitch, no pitch input → `FreeLookRig` has no pitch mutator; controller never writes pitch → Task 1/3. ✅
- Pure `FreeLookRig` + thin controller split → Tasks 1 & 3. ✅
- `CameraRig.IsTweening` + dead-code cleanup → Task 2. ✅
- "Authored fly-to wins" via `intermission && !IsTweening` + re-seed → Task 3. ✅
- Off during live sets via `SetStarted`/`SetEnded` → Task 3. ✅
- Edge-push UI/window gating + `edgePanMargin<=0` disables → Task 3. ✅
- `GameBootstrap` optional wiring (not in null-guard) → Task 4. ✅
- `FestivalGroundSetup` adds + seeds controller → Task 5. ✅
- Domain 73 → ~80+ → Task 1 (11 tests → 84). ✅
- SETUP.md section → Task 6. ✅

**Placeholder scan:** none — every code step contains complete code; every run step has an exact command + expected output.

**Type consistency:** `FreeLookRig` ctor signature, method names (`Pan/Orbit/Zoom/Resolve/SeedFrom`), and `CameraPose` field order (`PosX,PosY,PosZ,Yaw,Pitch`) are identical across Task 1's implementation, Task 1's tests, and Task 3's consumer. The serialized field names in Task 3 match the `SetFloat`/`SetRef` strings in Task 5 (`rig`, `minX/maxX/minZ/maxZ`, `focusY`, `minDistance/maxDistance`, `panSpeedPerDistance`, `orbitSpeed`, `zoomSpeed`, `edgePanMargin`). `GameBootstrap.freeLook` (Task 4) matches the `SetRef(boot, "freeLook", …)` in Task 5.
