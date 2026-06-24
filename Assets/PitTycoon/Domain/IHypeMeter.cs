namespace PitTycoon.Domain
{
    /// <summary>
    /// Read-only hype progress, decoupling crowd fill from the concrete HypeSystem.
    /// </summary>
    public interface IHypeMeter
    {
        /// <summary>Current hype as a 0..1 fraction of the ceiling.</summary>
        float HypeFraction { get; }
    }
}
