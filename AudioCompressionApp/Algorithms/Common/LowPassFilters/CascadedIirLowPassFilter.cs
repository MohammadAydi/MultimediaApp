namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

public class CascadedIirLowPassFilter : IAdmLowPassFilter
{
    private readonly FirstOrderIirLowPassFilter _stage;
    private readonly int _stages;

    public string Name => $"Cascaded IIR LPF ({_stages} stages, α = {_stage.Alpha:F3})";

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
