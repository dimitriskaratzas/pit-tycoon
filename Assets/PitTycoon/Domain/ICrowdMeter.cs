namespace PitTycoon.Domain
{
    /// <summary>
    /// Read-only pit fill, decoupling the hype-rate multiplier from the concrete CrowdController.
    /// </summary>
    public interface ICrowdMeter
    {
        /// <summary>How full the pit is, 0..1 (Active / Capacity).</summary>
        float FillFraction { get; }
    }
}
