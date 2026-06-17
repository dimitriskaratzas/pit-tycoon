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
        [Tooltip("Hype added off-beat (the floor). On-beat multiplies this up to MaxMultiplier, " +
                 "so keep it small for 'off-beat does little'.")]
        public float BaseSpike = 4f;
        [Tooltip("Multiplier when fired exactly on a beat (falls to 1.0 at the window edge). " +
                 "A wide spread (e.g. 6) makes timing clearly worth it.")]
        [Min(1f)] public float MaxMultiplier = 6f;
        [Tooltip("Seconds either side of a beat that still count as on-beat. Smaller = stricter timing.")]
        public float ToleranceSeconds = 0.1f;
        [Tooltip("Seconds between fires. ~one beat (0.5s at 120 BPM) lets you tap on the rhythm.")]
        public float Cooldown = 0.5f;
    }
}
