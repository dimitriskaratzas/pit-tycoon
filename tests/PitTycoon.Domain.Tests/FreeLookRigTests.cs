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
        public void SeedFrom_NormalizesPitchAbove180ToSignedRange()
        {
            var r = Wide();
            // eulerAngles.x of 340 means a 20-degree upward tilt; must normalize to -20, not stay 340.
            r.SeedFrom(new CameraPose(0f, 5f, 0f, 0f, 340f));
            Assert.That(r.Pitch, Is.EqualTo(-20f).Within(1e-3f));
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
