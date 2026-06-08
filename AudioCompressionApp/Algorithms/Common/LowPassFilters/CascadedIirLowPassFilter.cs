namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Version 3b – Cascaded (Multi-Stage) First-Order IIR Low-Pass Filter.
/// 
/// Analogue equivalent : multiple cascaded active RC stages.
/// 
/// Each additional stage adds 6 dB/octave of roll-off attenuation:
///   1 stage  →  6  dB/octave  (20 dB/decade)
///   2 stages → 12 dB/octave   (40 dB/decade)
///   3 stages → 18 dB/octave   (60 dB/decade)
/// 
/// The article shows this as:
///     filtered1 = ApplyLPF(decoded);
///     filtered2 = ApplyLPF(filtered1);
/// 
/// Note: cascading stages also shifts the effective -3 dB point downward.
/// For N identical stages with coefficient α, the combined -3 dB frequency
/// is lower than a single stage's cutoff.  Use a slightly larger α when
/// stacking, or use <see cref="FirstOrderIirLowPassFilter.FromCutoffFrequency"/>
/// to pre-calculate the right α for your desired cutoff.
/// </summary>
public class CascadedIirLowPassFilter : IAdmLowPassFilter
{
    private readonly FirstOrderIirLowPassFilter _stage;
    private readonly int _stages;

    public string Name => $"Cascaded IIR LPF ({_stages} stages, α = {_stage.Alpha:F3})";

    /// <param name="alpha">Smoothing coefficient per stage (0 &lt; α &lt; 1).</param>
    /// <param name="stages">Number of filter passes.  2 = 2nd-order, 3 = 3rd-order, …</param>
    public CascadedIirLowPassFilter(double alpha = 0.2, int stages = 2)
    {
        if (stages < 2)
            throw new ArgumentOutOfRangeException(nameof(stages), "Use at least 2 stages; for 1 stage use FirstOrderIirLowPassFilter.");

        _stage  = new FirstOrderIirLowPassFilter(alpha);
        _stages = stages;
    }

    public short[] Apply(short[] samples)
    {
        short[] current = samples;

        for (int s = 0; s < _stages; s++)
            current = _stage.Apply(current);

        return current;
    }
}
