using System;

namespace PitTycoon.Domain
{
    /// <summary>On-beat hit quality, shared by every ability (was on WhirlpoolAbility).</summary>
    public enum HitQuality { None, Miss, Good, Perfect }

    /// <summary>Result of attempting to fire an ability.</summary>
    public readonly struct FireResult
    {
        public bool Fired { get; }
        public float Multiplier { get; }
        public float HypeAdded { get; }
        public HitQuality Quality { get; }

        public FireResult(bool fired, float multiplier, float hypeAdded, HitQuality quality)
        {
            Fired = fired; Multiplier = multiplier; HypeAdded = hypeAdded; Quality = quality;
        }

        public static readonly FireResult NotFired = new FireResult(false, 0f, 0f, HitQuality.None);
    }

    /// <summary>
    /// One purchasable ability. Pure C#: owns its cooldown + owned flag, and scores a fire
    /// against the nearest beat via <see cref="BeatWindow"/>. The Unity layer feeds it the
    /// shared BeatGrid's nearest-beat time and applies the VFX/hype side effects.
    /// </summary>
    public sealed class Ability
    {
        private readonly float _baseSpike;
        private readonly float _maxMultiplier;
        private readonly double _tolerance;
        private readonly double _cooldown;

        public string Id { get; }
        public bool Owned { get; private set; }
        public double CooldownRemaining { get; private set; }
        public bool CanFire => Owned && CooldownRemaining <= 0.0;

        public Ability(string id, float baseSpike, float maxMultiplier,
            double toleranceSeconds, double cooldown, bool ownedFromStart)
        {
            Id = id;
            _baseSpike = baseSpike;
            _maxMultiplier = maxMultiplier < 1f ? 1f : maxMultiplier;
            _tolerance = toleranceSeconds;
            _cooldown = cooldown;
            Owned = ownedFromStart;
        }

        public void Unlock() => Owned = true;

        public void Tick(double dt)
        {
            if (CooldownRemaining <= 0.0) return;
            CooldownRemaining -= dt;
            if (CooldownRemaining < 0.0) CooldownRemaining = 0.0;
        }

        public FireResult Fire(double now, double nearestBeatDspTime)
        {
            if (!CanFire) return FireResult.NotFired;
            float mult = BeatWindow.Multiplier(nearestBeatDspTime, now, _tolerance, _maxMultiplier);
            float hypeAdded = _baseSpike * mult;
            float onBeat01 = (mult - 1f) / Math.Max(0.0001f, _maxMultiplier - 1f);
            HitQuality quality = onBeat01 >= 0.66f ? HitQuality.Perfect
                               : onBeat01 >= 0.25f ? HitQuality.Good
                               : HitQuality.Miss;
            CooldownRemaining = _cooldown;
            return new FireResult(true, mult, hypeAdded, quality);
        }
    }
}
