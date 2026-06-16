using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class EventBusTests
    {
        [Test]
        public void Publish_InvokesSubscriberWithPayload()
        {
            var bus = new EventBus();
            int received = 0;
            bus.Subscribe<UpgradePurchased>(e => received++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(received, Is.EqualTo(1));
        }

        [Test]
        public void Publish_InvokesAllSubscribers()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            bus.Subscribe<UpgradePurchased>(e => a++);
            bus.Subscribe<UpgradePurchased>(e => b++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            void Handler(UpgradePurchased e) => count++;
            bus.Subscribe<UpgradePurchased>(Handler);
            bus.Unsubscribe<UpgradePurchased>(Handler);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new SetEnded(10f, 5f, 3)));
        }

        [Test]
        public void DifferentEventTypes_AreIsolated()
        {
            var bus = new EventBus();
            int upgrades = 0, setEnds = 0;
            bus.Subscribe<UpgradePurchased>(e => upgrades++);
            bus.Subscribe<SetEnded>(e => setEnds++);
            bus.Publish(new UpgradePurchased("capacity"));
            Assert.That(upgrades, Is.EqualTo(1));
            Assert.That(setEnds, Is.EqualTo(0));
        }

        [Test]
        public void SetEnded_CarriesPayload()
        {
            var e = new SetEnded(peakHype: 100f, avgHype: 40f, cashEarned: 70);
            Assert.That(e.PeakHype, Is.EqualTo(100f));
            Assert.That(e.AvgHype, Is.EqualTo(40f));
            Assert.That(e.CashEarned, Is.EqualTo(70));
        }
    }
}
