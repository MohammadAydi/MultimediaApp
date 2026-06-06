namespace AudioCompressionApp.Algorithms.ADM.Filters;

/// <summary>
/// Version 2 – Moving Average Low-Pass Filter.
/// 
/// Analogue equivalent : passive RC filter (no amplification).
/// 
/// Each output sample is the arithmetic mean of a symmetric window of
/// neighbouring input samples.  A 5-sample window (radius = 2) is the
/// default, which mirrors the example from the article, but you can pass
/// any odd window size you like.
/// 
/// Pros : trivial to understand and implement; removes ADM granular noise.
/// Cons : blurs transients; frequency roll-off is relatively weak and has
///        side-lobes ("spectral leakage"), so it is not a clean LPF.
/// </summary>
public class MovingAverageLowPassFilter : IAdmLowPassFilter
{
    /// <summary>
    /// Half-width of the averaging window (full window = 2*radius + 1).
    /// Default radius = 2  →  5-sample window, matching the article.
    /// </summary>
    public int Radius { get; }

    public string Name => $"Moving Average LPF (window = {2 * Radius + 1})";

    /// <param name="radius">
    /// Half-width of the symmetric averaging window.
    /// Must be ≥ 1.  Larger values = more smoothing but more transient blur.
    /// </param>
    public MovingAverageLowPassFilter(int radius = 2)
    {
        if (radius < 1)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be at least 1.");

        Radius = radius;
    }

    public short[] Apply(short[] samples)
    {
        int n = samples.Length;
        short[] filtered = new short[n];

        for (int i = 0; i < n; i++)
        {
            // Clamp the window to valid indices (handles edges gracefully).
            int start = Math.Max(0, i - Radius);
            int end   = Math.Min(n - 1, i + Radius);
            int count = end - start + 1;

            double sum = 0;
            for (int j = start; j <= end; j++)
                sum += samples[j];

            filtered[i] = (short)Math.Clamp(
                Math.Round(sum / count),
                short.MinValue,
                short.MaxValue);
        }

        return filtered;
    }
}
