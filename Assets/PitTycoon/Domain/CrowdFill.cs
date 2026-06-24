using System;

namespace PitTycoon.Domain
{
    /// <summary>
    /// The organic pit model. A persistent Following (arrived fans) ratchets up across
    /// sets; within a set, Active swells from Following toward Capacity as hype climbs and
    /// never ebbs. Grounds raises Capacity. Pure C# — no UnityEngine, fully unit-testable.
    /// CrowdController renders this; HypeSystem reads FillFraction.
    /// </summary>
    public sealed class CrowdFill
    {
        public int Capacity { get; private set; }
        public int Following { get; private set; }
        public float Active { get; private set; }

        public CrowdFill(int capacity, int initialFollowing)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Following = Clamp(initialFollowing, 0, capacity);
            Active = Following;
        }

        /// <summary>Each set starts at the persistent following floor.</summary>
        public void BeginSet() => Active = Following;

        /// <summary>Swell Active toward Capacity by hype fraction; ratchets, never ebbs.</summary>
        public void Tick(float hypeFraction01)
        {
            float h = Clamp01(hypeFraction01);
            float target = Following + (Capacity - Following) * h;
            if (target > Capacity) target = Capacity;
            if (target > Active) Active = target;
        }

        /// <summary>End of set: the arrivals stay — promote peak Active to the following.</summary>
        public void BankSet()
        {
            int banked = (int)Math.Round(Active, MidpointRounding.AwayFromZero);
            Following = Clamp(banked, 0, Capacity);
        }

        /// <summary>Grounds upgrade: raise the capacity the pit fills toward.</summary>
        public void RaiseCapacity(int delta)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            Capacity += delta;
        }

        public int ActiveCount => (int)Math.Round(Active, MidpointRounding.AwayFromZero);
        public float FillFraction => Capacity > 0 ? Active / Capacity : 0f;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
