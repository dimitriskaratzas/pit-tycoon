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
