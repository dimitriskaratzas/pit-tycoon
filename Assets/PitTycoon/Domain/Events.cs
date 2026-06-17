namespace PitTycoon.Domain
{
    /// <summary>Raised when the analyzer detects a beat.</summary>
    public readonly struct BeatDetected
    {
        public BeatInfo Beat { get; }
        public BeatDetected(BeatInfo beat) { Beat = beat; }
    }

    /// <summary>Raised when an ability fires, with its resolved on-beat multiplier.</summary>
    public readonly struct AbilityFired
    {
        public string AbilityId { get; }
        public float Multiplier { get; }
        public float HypeAdded { get; }
        public AbilityFired(string abilityId, float multiplier, float hypeAdded)
        {
            AbilityId = abilityId;
            Multiplier = multiplier;
            HypeAdded = hypeAdded;
        }
    }

    /// <summary>Raised when a set ends and hype banks to cash.</summary>
    public readonly struct SetEnded
    {
        public float PeakHype { get; }
        public float AvgHype { get; }
        public int CashEarned { get; }
        public SetEnded(float peakHype, float avgHype, int cashEarned)
        {
            PeakHype = peakHype;
            AvgHype = avgHype;
            CashEarned = cashEarned;
        }
    }

    /// <summary>Raised when a passive upgrade is bought during intermission.</summary>
    public readonly struct UpgradePurchased
    {
        public string UpgradeId { get; }
        public UpgradePurchased(string upgradeId) { UpgradeId = upgradeId; }
    }
}
