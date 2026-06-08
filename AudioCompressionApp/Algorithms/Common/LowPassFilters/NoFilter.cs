namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Version 1 – No filtering.
/// Returns the raw decoded ADM samples unchanged so you can hear
/// the granular noise before any smoothing is applied.
/// </summary>
public class NoFilter : IAdmLowPassFilter
{
    public string Name => "No Filter (Raw ADM)";

    public short[] Apply(short[] samples)
    {
        // Just hand back the original array – nothing to do.
        short[] result = new short[samples.Length];
        Array.Copy(samples, result, samples.Length);
        return result;
    }
}
