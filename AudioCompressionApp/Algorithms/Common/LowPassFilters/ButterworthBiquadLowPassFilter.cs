namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Version 4 – Second-Order Butterworth Low-Pass Biquad Filter.
/// 
/// Analogue equivalent : 2nd-order Butterworth active filter (Sallen-Key topology
/// is the classic hardware circuit, but in software we implement the equivalent
/// transfer function directly as a biquad section).
/// 
/// Difference equation:
///     y[n] = a0·x[n] + a1·x[n-1] + a2·x[n-2]
///           - b1·y[n-1] - b2·y[n-2]
/// 
/// Characteristics:
///   • Maximally flat passband (no ripple) – the defining Butterworth property.
///   • Roll-off: 12 dB/octave (40 dB/decade) – twice as steep as a 1st-order filter.
///   • Clean, smooth sound preferred by most audio applications.
///   • Coefficients are calculated from the desired cutoff frequency and sample rate.
/// 
/// This is how real-world audio software (DAWs, media players) typically implements
/// Butterworth filters and it would look very professional in a university project.
/// 
/// Usage:
///     // Hard cutoff at 4 kHz on 44.1 kHz audio
///     var filter = new ButterworthBiquadLowPassFilter(cutoffHz: 4000, sampleRateHz: 44100);
///     short[] clean = filter.Apply(rawSamples);
/// </summary>
public class ButterworthBiquadLowPassFilter : IAdmLowPassFilter
{
    // Feed-forward coefficients (numerator).
    private readonly double _a0;
    private readonly double _a1;
    private readonly double _a2;

    // Feed-back coefficients (denominator, sign already applied).
    private readonly double _b1;
    private readonly double _b2;

    public double CutoffHz    { get; }
    public int    SampleRate  { get; }

    public string Name =>
        $"Butterworth Biquad LPF (fc = {CutoffHz:F0} Hz, fs = {SampleRate} Hz)";

    /// <param name="cutoffHz">
    /// -3 dB cutoff frequency in Hz.
    /// Must be &gt; 0 and &lt; sampleRateHz / 2 (Nyquist limit).
    /// Good starting points for ADM audio: 3 000 – 6 000 Hz.
    /// </param>
    /// <param name="sampleRateHz">
    /// Audio sample rate in Hz (read from the ADM header: header.SampleRate).
    /// </param>
    public ButterworthBiquadLowPassFilter(double cutoffHz, int sampleRateHz)
    {
        double nyquist = sampleRateHz / 2.0;
        if (cutoffHz <= 0 || cutoffHz >= nyquist)
            throw new ArgumentOutOfRangeException(
                nameof(cutoffHz),
                $"Cutoff must be between 0 and Nyquist ({nyquist:F0} Hz).");

        CutoffHz   = cutoffHz;
        SampleRate = sampleRateHz;

        // ── Bilinear-transform coefficient calculation ──────────────────────
        // Pre-warp the analogue cutoff frequency to compensate for the
        // frequency warping introduced by the bilinear transform.
        double wc = 2.0 * Math.PI * cutoffHz;          // analogue cutoff (rad/s)
        double wd = 2.0 * sampleRateHz
                    * Math.Tan(wc / (2.0 * sampleRateHz));   // pre-warped

        // For a 2nd-order Butterworth, the quality factor Q = 1/√2 ≈ 0.7071.
        const double Q = 0.70710678118; // 1 / sqrt(2)

        double K  = wd / (2.0 * sampleRateHz);   // normalised pre-warped frequency
        double K2 = K * K;
        double norm = 1.0 / (1.0 + K / Q + K2);

        _a0 = K2 * norm;
        _a1 = 2.0 * _a0;
        _a2 = _a0;
        _b1 = 2.0 * (K2 - 1.0) * norm;
        _b2 = (1.0 - K / Q + K2) * norm;
        // ───────────────────────────────────────────────────────────────────
    }

    public short[] Apply(short[] samples)
    {
        if (samples.Length == 0)
            return [];

        short[] result = new short[samples.Length];

        // Delay-line state variables.
        double x1 = 0, x2 = 0;   // previous inputs
        double y1 = 0, y2 = 0;   // previous outputs

        // Seed the state with the first sample to avoid a cold-start DC step.
        // (One warm-up pass isn't strictly necessary for short ADM files but
        //  it eliminates the brief low-frequency click at the very start.)
        double seed = samples[0];
        x1 = x2 = seed;
        y1 = y2 = seed;

        for (int i = 0; i < samples.Length; i++)
        {
            double x = samples[i];

            // Core biquad difference equation.
            double y = _a0 * x
                     + _a1 * x1
                     + _a2 * x2
                     - _b1 * y1
                     - _b2 * y2;

            // Shift the delay line.
            x2 = x1;  x1 = x;
            y2 = y1;  y1 = y;

            result[i] = (short)Math.Clamp(
                Math.Round(y),
                short.MinValue,
                short.MaxValue);
        }

        return result;
    }
}
