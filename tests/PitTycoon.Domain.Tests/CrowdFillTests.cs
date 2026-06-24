using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class CrowdFillTests
    {
        [Test]
        public void BeginSet_ResetsActiveToFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);              // push Active up to 100
            f.BeginSet();
            Assert.That(f.Active, Is.EqualTo(20f));
        }

        [Test]
        public void Tick_Zero_StaysAtFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0f);
            Assert.That(f.Active, Is.EqualTo(20f));
        }

        [Test]
        public void Tick_Full_ReachesCapacity()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);
            Assert.That(f.Active, Is.EqualTo(100f));
        }

        [Test]
        public void Tick_Half_IsMidwayBetweenFollowingAndCapacity()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // 20 + (100-20)*0.5
            Assert.That(f.Active, Is.EqualTo(60f));
        }

        [Test]
        public void Tick_Ratchets_DoesNotShrinkOnLowerHype()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.75f);           // Active = 20 + 80*0.75 = 80 (0.75 is exact in binary)
            f.Tick(0.25f);           // target 40 < 80 -> unchanged
            Assert.That(f.Active, Is.EqualTo(80f));
        }

        [Test]
        public void BankSet_PromotesActiveToFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // Active 60
            f.BankSet();
            Assert.That(f.Following, Is.EqualTo(60));
        }

        [Test]
        public void RaiseCapacity_AllowsFurtherGrowth()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(1f);              // Active 100 at old cap
            f.RaiseCapacity(40);     // cap 140
            f.Tick(1f);              // target 20 + 120 = 140
            Assert.That(f.Capacity, Is.EqualTo(140));
            Assert.That(f.Active, Is.EqualTo(140f));
        }

        [Test]
        public void RaiseCapacity_DoesNotChangeActiveOrFollowing()
        {
            var f = new CrowdFill(100, 20);
            f.Tick(0.5f);            // Active 60
            f.RaiseCapacity(50);
            Assert.That(f.Active, Is.EqualTo(60f));
            Assert.That(f.Following, Is.EqualTo(20));
        }

        [Test]
        public void FillFraction_IsActiveOverCapacity()
        {
            var f = new CrowdFill(100, 25);
            Assert.That(f.FillFraction, Is.EqualTo(0.25f));
        }

        [Test]
        public void Constructor_ClampsInitialFollowingToCapacity()
        {
            var f = new CrowdFill(50, 999);
            Assert.That(f.Following, Is.EqualTo(50));
            Assert.That(f.Active, Is.EqualTo(50f));
        }

        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.That(() => new CrowdFill(0, 0),
                        Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void RaiseCapacity_RejectsNegativeDelta()
        {
            var f = new CrowdFill(100, 20);
            Assert.That(() => f.RaiseCapacity(-1),
                        Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
