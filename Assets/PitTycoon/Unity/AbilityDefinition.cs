using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven ability (M1: whirlpool only). Adding more abilities = new assets,
    /// no code. On-beat firing multiplies BaseSpike up to MaxMultiplier within tolerance.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Pit Tycoon/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string Id = "whirlpool";
        public string DisplayName = "Whirlpool";
        [Tooltip("Hype added on fire, before the on-beat multiplier.")]
        public float BaseSpike = 12f;
        [Tooltip("Multiplier when fired exactly on a beat (falls to 1.0 at the window edge).")]
        [Min(1f)] public float MaxMultiplier = 3f;
        [Tooltip("Seconds either side of a beat that still count as on-beat.")]
        public float ToleranceSeconds = 0.12f;
        [Tooltip("Seconds between fires.")]
        public float Cooldown = 1.2f;
    }
}
