using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>Which venue system a passive upgrade scales.</summary>
    public enum UpgradeKind { Grounds, Stage, Lighting, PA }

    /// <summary>
    /// Data-driven passive upgrade. Bought repeatedly at an escalating cost
    /// (BaseCost * CostGrowth^level); each purchase applies its per-level deltas and
    /// a visible step on the venue (via VenueController, by Kind).
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Pit Tycoon/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id = "grounds";
        public string DisplayName = "Grounds Expansion";
        public UpgradeKind Kind = UpgradeKind.Grounds;
        [Min(0)] public int BaseCost = 60;
        [Min(1f)] public float CostGrowth = 1.6f;

        [Header("Per-level effects (set the ones relevant to this kind)")]
        [Tooltip("Crowd capacity (member slots) added per purchase (Grounds).")]
        [Min(0)] public int AddCapacity = 0;
        [Tooltip("Hype ceiling added per purchase (Stage).")]
        public float CeilingDelta = 0f;
        [Tooltip("Passive hype/sec added per purchase (Lighting, PA).")]
        public float RateDelta = 0f;
    }
}
