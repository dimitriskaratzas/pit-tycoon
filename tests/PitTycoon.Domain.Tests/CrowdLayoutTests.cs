using NUnit.Framework;

namespace PitTycoon.Domain.Tests
{
    public class CrowdLayoutTests
    {
        // columns 12, spacing 1.2, startRows 7 — the shipping CrowdController defaults.
        private static CrowdLayoutSettings Settings(float positionJitter = 0.3f, float falloff = 0.06f)
            => new CrowdLayoutSettings(12, 1.2f, 7, positionJitter, falloff, 18f, 0.12f);

        [Test]
        public void Slot_IsDeterministic_SameIndexSamePlace()
        {
            var s = Settings();
            var a = CrowdLayout.Slot(37, s);
            var b = CrowdLayout.Slot(37, s);
            Assert.That(b.X, Is.EqualTo(a.X));
            Assert.That(b.Z, Is.EqualTo(a.Z));
            Assert.That(b.Scale, Is.EqualTo(a.Scale));
            Assert.That(b.RotationY, Is.EqualTo(a.RotationY));
            Assert.That(b.PopScale, Is.EqualTo(a.PopScale));
        }

        [Test]
        public void RowDepth_FrontRowIsZero()
        {
            Assert.That(CrowdLayout.RowDepth(0, 1.2f, 0.06f), Is.EqualTo(0f));
        }

        [Test]
        public void RowDepth_NoFalloff_MatchesUniformGrid()
        {
            Assert.That(CrowdLayout.RowDepth(5, 1.2f, 0f), Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void RowDepth_GapWidensWithDistanceFromStage()
        {
            float gapNear = CrowdLayout.RowDepth(2, 1.2f, 0.06f) - CrowdLayout.RowDepth(1, 1.2f, 0.06f);
            float gapFar = CrowdLayout.RowDepth(9, 1.2f, 0.06f) - CrowdLayout.RowDepth(8, 1.2f, 0.06f);
            Assert.That(gapFar, Is.GreaterThan(gapNear));
        }

        [Test]
        public void Slot_JitterStaysWithinBounds()
        {
            var s = Settings(positionJitter: 0.3f);
            float maxOffset = 0.3f * 1.2f;      // jitter is a fraction of spacing, max multiplier is 1
            float offsetX = (12 - 1) * 1.2f * 0.5f;

            for (int i = 0; i < 400; i++)
            {
                var slot = CrowdLayout.Slot(i, s);
                float gridX = (i % 12) * 1.2f - offsetX;
                Assert.That(System.Math.Abs(slot.X - gridX), Is.LessThanOrEqualTo(maxOffset + 1e-4f));
            }
        }

        [Test]
        public void Slot_BackRowsScatterMoreThanTheFrontRow()
        {
            var s = Settings();
            float offsetX = (12 - 1) * 1.2f * 0.5f;

            float FrontRowSpread()
            {
                float total = 0f;
                for (int col = 0; col < 12; col++)
                    total += System.Math.Abs(CrowdLayout.Slot(col, s).X - (col * 1.2f - offsetX));
                return total;
            }

            float BackRowSpread()
            {
                float total = 0f;
                for (int col = 0; col < 12; col++)
                {
                    int i = 10 * 12 + col;
                    total += System.Math.Abs(CrowdLayout.Slot(i, s).X - (col * 1.2f - offsetX));
                }
                return total;
            }

            Assert.That(BackRowSpread(), Is.GreaterThan(FrontRowSpread()));
        }

        [Test]
        public void Slot_NeighbouringIndicesDoNotShareJitter()
        {
            // Adjacent indices stand next to each other in the pit, so a weak hash shows up as
            // visible diagonal banding. Neighbours must not land on the same offset.
            var s = Settings();
            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                var a = CrowdLayout.Slot(i, s);
                var b = CrowdLayout.Slot(i + 1, s);
                float aOff = a.X - (i % 12) * 1.2f;
                float bOff = b.X - ((i + 1) % 12) * 1.2f;
                if (System.Math.Abs(aOff - bOff) < 1e-6f) identical++;
            }
            Assert.That(identical, Is.Zero);
        }

        [Test]
        public void Slot_ScaleStaysWithinJitter()
        {
            var s = Settings();
            for (int i = 0; i < 200; i++)
            {
                float scale = CrowdLayout.Slot(i, s).Scale;
                Assert.That(scale, Is.InRange(1f - 0.12f - 1e-4f, 1f + 0.12f + 1e-4f));
            }
        }

        [Test]
        public void Slot_PopScaleStaysInTheDesignedRange()
        {
            var s = Settings();
            for (int i = 0; i < 200; i++)
                Assert.That(CrowdLayout.Slot(i, s).PopScale, Is.InRange(0.55f, 1.2f));
        }

        [Test]
        public void PopHeight_IsZeroBeforeTheWaveArrives()
        {
            Assert.That(CrowdLayout.PopHeight(row: 4, secondsSinceBeat: 0.05f,
                strength: 1f, rowDelay: 0.03f, decay: 2.5f), Is.EqualTo(0f));
        }

        [Test]
        public void PopHeight_IsFullStrengthExactlyOnArrival()
        {
            Assert.That(CrowdLayout.PopHeight(row: 4, secondsSinceBeat: 0.12f,
                strength: 1f, rowDelay: 0.03f, decay: 2.5f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void PopHeight_FrontRowLeadsTheBackRow()
        {
            float front = CrowdLayout.PopHeight(0, 0.05f, 1f, 0.03f, 2.5f);
            float back = CrowdLayout.PopHeight(6, 0.05f, 1f, 0.03f, 2.5f);
            Assert.That(front, Is.GreaterThan(back));
        }

        [Test]
        public void PopHeight_DecaysToZero()
        {
            Assert.That(CrowdLayout.PopHeight(0, 10f, 1f, 0.03f, 2.5f), Is.EqualTo(0f));
        }

        [Test]
        public void PopHeight_ZeroRowDelay_FiresEveryRowTogether()
        {
            // rowDelay 0 must reproduce the pre-M5f behaviour exactly, so the wave can be
            // A/B'd from the Inspector.
            float front = CrowdLayout.PopHeight(0, 0.05f, 1f, 0f, 2.5f);
            float back = CrowdLayout.PopHeight(9, 0.05f, 1f, 0f, 2.5f);
            Assert.That(back, Is.EqualTo(front));
        }

        [Test]
        public void VariantIndex_StaysInRangeAndIsDeterministic()
        {
            for (int i = 0; i < 200; i++)
            {
                int v = CrowdLayout.VariantIndex(i, 4);
                Assert.That(v, Is.InRange(0, 3));
                Assert.That(CrowdLayout.VariantIndex(i, 4), Is.EqualTo(v));
            }
        }

        [Test]
        public void OutfitIndex_StaysInRangeAndDiffersFromVariantChoice()
        {
            int sameCount = 0;
            for (int i = 0; i < 200; i++)
            {
                int o = CrowdLayout.OutfitIndex(i, 6);
                Assert.That(o, Is.InRange(0, 5));
                if (o == CrowdLayout.VariantIndex(i, 6)) sameCount++;
            }
            // Different salts: outfit and body must not be locked together across the pit.
            Assert.That(sameCount, Is.LessThan(200));
        }

        [Test]
        public void PickIndex_HandlesEmptyCollections()
        {
            Assert.That(CrowdLayout.VariantIndex(5, 0), Is.Zero);
            Assert.That(CrowdLayout.OutfitIndex(5, 0), Is.Zero);
        }
    }
}
