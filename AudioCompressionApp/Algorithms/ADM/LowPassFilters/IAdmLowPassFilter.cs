namespace AudioCompressionApp.Algorithms.ADM.Filters;

/// <summary>
/// Common interface for every ADM low-pass post-filter.
/// Call Apply() on the raw decoded samples; it returns the smoothed array.
/// </summary>
public interface IAdmLowPassFilter
{
    /// <summary>Human-readable name shown in logs / UI.</summary>
    string Name { get; }

    /// <summary>
    /// Filter the decoded ADM samples and return the smoothed result.
    /// The returned array is always the same length as <paramref name="samples"/>.
    /// </summary>
    short[] Apply(short[] samples);
}
