namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Version 3 – First-Order IIR (Infinite Impulse Response) Low-Pass Filter.
/// 
/// Analogue equivalent : first-order active RC filter (op-amp based).
/// 
/// Recurrence relation:
///     y[n] = y[n-1] + α · (x[n] - y[n-1])
///
/// α (alpha) controls the cutoff frequency:
///     • α close to 0  →  heavy smoothing, very low cutoff (can muffle audio)
///     • α close to 1  →  almost no smoothing (output ≈ input)
///     • α = 0.1       →  good starting point for ADM granular noise removal
///
/// Why it works for ADM:
///   ADM granular noise manifests as rapid oscillations (e.g. 100, 120, 80 …).
///   The IIR filter exponentially decays those high-frequency swings while
///   letting slower signal components (the actual audio content) through.
///
/// Pros : 10 lines of code; causal (no look-ahead); best effort-to-quality ratio.
/// Cons : introduces a slight phase lag (group delay); steepness of roll-off is
///        limited to 6 dB/octave (same as a single passive RC stage).
///
/// Cutoff frequency relationship:
///     α ≈ 2π·fc / (2π·fc + fs)     (continuous-time approximation)
///     Rearranged: fc ≈ α·fs / (2π·(1 - α))
/// where fc is the -3 dB cutoff in Hz and fs is the sample rate in Hz.
/// </summary>
public class FirstOrderIirLowPassFilter : IAdmLowPassFilter
{
    /// <summary>
    /// Smoothing coefficient in the range (0, 1).
    /// Default 0.1 is recommended for ADM noise removal.
    /// </summary>
    public double Alpha { get; }

    public string Name => $"First-Order IIR LPF (α = {Alpha:F3})";

    /// <param name="alpha">
    /// Smoothing coefficient.  Must be strictly between 0 and 1.
    /// Smaller = more smoothing.  Recommended starting value: 0.1.
    /// </param>
    public FirstOrderIirLowPassFilter(double alpha = 0.1)
    {
        if (alpha is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be strictly between 0 and 1.");

        Alpha = alpha;
    }

    public short[] Apply(short[] samples)
    {
        if (samples.Length == 0)
            return [];

        double[] filtered = new double[samples.Length];

        // Seed: use first sample so there is no cold-start DC offset.
        filtered[0] = samples[0];

        for (int i = 1; i < samples.Length; i++)
        {
            // y[n] = y[n-1] + α · (x[n] - y[n-1])
            filtered[i] = filtered[i - 1] + Alpha * (samples[i] - filtered[i - 1]);
        }

        // Convert back to PCM shorts.
        short[] result = new short[samples.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (short)Math.Clamp(
                Math.Round(filtered[i]),
                short.MinValue,
                short.MaxValue);
        }

        return result;
    }

    /// <summary>
    /// Convenience factory: compute the alpha value that gives a desired
    /// -3 dB cutoff frequency, given the audio sample rate.
    /// </summary>
    /// <param name="cutoffHz">Desired cutoff frequency in Hz (e.g. 4000).</param>
    /// <param name="sampleRateHz">Audio sample rate in Hz (e.g. 8000, 44100).</param>
    public static FirstOrderIirLowPassFilter FromCutoffFrequency(
        double cutoffHz,
        int sampleRateHz)
    {
        if (cutoffHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(cutoffHz));
        if (sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));

        // Bilinear transform approximation of α.
        double rc    = 1.0 / (2.0 * Math.PI * cutoffHz);
        double dt    = 1.0 / sampleRateHz;
        double alpha = dt / (rc + dt);

        // Guard: clamp to a sensible range in case the user passes
        // an extremely high cutoff close to Nyquist.
        alpha = Math.Clamp(alpha, 1e-6, 1.0 - 1e-6);

        return new FirstOrderIirLowPassFilter(alpha);
    }
}
