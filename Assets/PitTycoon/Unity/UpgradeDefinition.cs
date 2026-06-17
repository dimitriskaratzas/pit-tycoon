using UnityEngine;

namespace PitTycoon.Unity
{
    /// <summary>
    /// Data-driven passive upgrade (M1: the capacity / "grounds" upgrade only — the
    /// densest visible change). Buying it grows the crowd grid and raises the hype
    /// ceiling, so the next set is visibly bigger and can bank more.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Pit Tycoon/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id = "capacity";
        public string DisplayName = "Grounds Expansion";
        [Min(0)] public int Cost = 60;
        [Tooltip("Crowd columns added per purchase.")]
        public int AddColumns = 4;
        [Tooltip("Crowd rows added per purchase.")]
        public int AddRows = 2;
        [Tooltip("Hype ceiling added per purchase (bigger venue holds more hype).")]
        public float CeilingDelta = 25f;
    }
}
