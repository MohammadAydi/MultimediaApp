namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Static factory that creates every supported low-pass filter variant.
/// 
/// Import this one class to access all filters without remembering individual
/// constructor signatures.
/// 
/// <code>
/// // All available filters, from simplest to most professional:
/// IAdmLowPassFilter f1 = AdmLowPassFilters.None();
/// IAdmLowPassFilter f2 = AdmLowPassFilters.MovingAverage();
/// IAdmLowPassFilter f3 = AdmLowPassFilters.IirFirstOrder();
/// IAdmLowPassFilter f3b = AdmLowPassFilters.IirCascaded();
/// IAdmLowPassFilter f4 = AdmLowPassFilters.Butterworth(cutoffHz: 4000, sampleRate: 44100);
/// </code>
/// </summary>
public static class AdmLowPassFilters
{
    // ─── Version 1: No filtering ────────────────────────────────────────────

    /// <summary>
    /// Raw ADM output with no filtering applied.
    /// Use this first to hear the unprocessed granular noise baseline.
    /// </summary>
    public static IAdmLowPassFilter None()
        => new NoFilter();

    // ─── Version 2: Moving Average ──────────────────────────────────────────

    /// <summary>
    /// Simple moving-average filter.
    /// Analogue equivalent: passive RC (no amplification).
    /// </summary>
    /// <param name="radius">
    /// Half-window size.  Total window = 2·radius + 1 samples.
    /// Default 2 → 5-sample window (as in the article).
    /// Try 3 or 4 for more smoothing.
    /// </param>
    public static IAdmLowPassFilter MovingAverage(int radius = 2)
        => new MovingAverageLowPassFilter(radius);

    // ─── Version 3: First-Order IIR ─────────────────────────────────────────

    /// <summary>
    /// First-order IIR exponential smoothing filter.
    /// Analogue equivalent: first-order active RC filter (op-amp).
    /// Recommended for most ADM use-cases.
    /// </summary>
    /// <param name="alpha">
    /// Smoothing factor (0 &lt; α &lt; 1).
    /// Smaller = smoother but more muffled.  Start with 0.1.
    /// </param>
    public static IAdmLowPassFilter IirFirstOrder(double alpha = 0.1)
        => new FirstOrderIirLowPassFilter(alpha);

    /// <summary>
    /// First-order IIR filter with the alpha value calculated from a desired
    /// -3 dB cutoff frequency.  Convenience wrapper when you think in Hz.
    /// </summary>
    /// <param name="cutoffHz">Desired -3 dB cutoff frequency in Hz.</param>
    /// <param name="sampleRate">Audio sample rate from the ADM header.</param>
    public static IAdmLowPassFilter IirFirstOrderFromCutoff(
        double cutoffHz,
        int    sampleRate)
        => FirstOrderIirLowPassFilter.FromCutoffFrequency(cutoffHz, sampleRate);

    // ─── Version 3b: Cascaded IIR ───────────────────────────────────────────

    /// <summary>
    /// Two or more first-order IIR stages in series.
    /// Each additional stage adds 6 dB/octave of roll-off.
    /// A 2-stage cascade approximates a 2nd-order filter.
    /// </summary>
    /// <param name="alpha">Smoothing factor per stage.</param>
    /// <param name="stages">Number of cascaded passes (≥ 2).</param>
    public static IAdmLowPassFilter IirCascaded(double alpha = 0.2, int stages = 2)
        => new CascadedIirLowPassFilter(alpha, stages);

    // ─── Version 4: Butterworth Biquad ──────────────────────────────────────

    /// <summary>
    /// Second-order Butterworth biquad low-pass filter.
    /// Analogue equivalent: Sallen-Key 2nd-order Butterworth active filter.
    /// Professional quality; maximally flat passband; 12 dB/octave roll-off.
    /// </summary>
    /// <param name="cutoffHz">
    /// -3 dB cutoff frequency in Hz.
    /// Good starting points for ADM speech: 3 000 – 4 000 Hz.
    /// For music: 6 000 – 8 000 Hz.
    /// Must be &lt; sampleRate / 2 (Nyquist limit).
    /// </param>
    /// <param name="sampleRate">
    /// Audio sample rate in Hz (from your ADM header: header.SampleRate).
    /// </param>
    public static IAdmLowPassFilter Butterworth(double cutoffHz, int sampleRate)
        => new ButterworthBiquadLowPassFilter(cutoffHz, sampleRate);
}
