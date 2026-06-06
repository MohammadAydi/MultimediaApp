namespace AudioCompressionApp.Models.Settings;

public class AdaptiveDeltaModulationSettings
    : CompressionSettings
{
    public double InitialStepSize { get; set; }
        = 100;

    public double StepIncreaseFactor { get; set; }
        = 1.1;

    public double StepDecreaseFactor { get; set; }
        = 0.9;

    public double MinimumStepSize { get; set; }
        = 1;

    public double MaximumStepSize { get; set; }
        = 10000;

    public double constFactor { get; set; } = 100;
}