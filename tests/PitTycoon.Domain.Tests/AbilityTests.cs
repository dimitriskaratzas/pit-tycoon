using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class AbilityTests
    {
        private static Ability Make(bool owned = true, float baseSpike = 4f, float maxMult = 6f,
            double tol = 0.1, double cooldown = 0.5)
            => new Ability("test", baseSpike, maxMult, tol, cooldown, owned);

        [Test]
        public void NotOwned_CannotFire_AndUnlockOwns()
        {
            var a = Make(owned: false);
            Assert.That(a.Owned, Is.False);
            Assert.That(a.CanFire, Is.False);
            Assert.That(a.Fire(1.0, 1.0).Fired, Is.False);
            a.Unlock();
            Assert.That(a.Owned, Is.True);
            Assert.That(a.CanFire, Is.True);
        }

        [Test]
        public void OnBeatFire_GivesMaxMultiplier_AndPerfect()
        {
            var a = Make();
            var r = a.Fire(2.0, 2.0); // fire exactly on the beat
            Assert.That(r.Fired, Is.True);
            Assert.That(r.Multiplier, Is.EqualTo(6f).Within(1e-4));
            Assert.That(r.HypeAdded, Is.EqualTo(24f).Within(1e-3)); // 4 * 6
            Assert.That(r.Quality, Is.EqualTo(HitQuality.Perfect));
        }

        [Test]
        public void OffBeatFire_GivesNoBonus_AndMiss()
        {
            var a = Make(tol: 0.1);
            var r = a.Fire(2.5, 2.0); // 0.5s off a 0.1s window
            Assert.That(r.Multiplier, Is.EqualTo(1f).Within(1e-4));
            Assert.That(r.HypeAdded, Is.EqualTo(4f).Within(1e-3));
            Assert.That(r.Quality, Is.EqualTo(HitQuality.Miss));
        }

        [Test]
        public void Fire_StartsCooldown_BlocksUntilTicked()
        {
            var a = Make(cooldown: 0.5);
            Assert.That(a.Fire(2.0, 2.0).Fired, Is.True);
            Assert.That(a.CanFire, Is.False);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(a.Fire(2.05, 2.0).Fired, Is.False); // still cooling
            a.Tick(0.5);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(a.CanFire, Is.True);
        }

        [Test]
        public void Tick_DoesNotDriveCooldownNegative()
        {
            var a = Make(cooldown: 0.5);
            a.Fire(2.0, 2.0);
            a.Tick(10.0);
            Assert.That(a.CooldownRemaining, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void MaxMultiplierBelowOne_IsClampedToOne()
        {
            var a = new Ability("x", 4f, 0.5f, 0.1, 0.5, true);
            var r = a.Fire(2.0, 2.0);
            Assert.That(r.Multiplier, Is.EqualTo(1f).Within(1e-4));
        }
    }
}
