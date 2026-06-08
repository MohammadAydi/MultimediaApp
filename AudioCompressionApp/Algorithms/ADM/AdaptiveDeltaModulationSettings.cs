using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms.ADM;

public class AdaptiveDeltaModulationSettings
    : CompressionSettings
{
    public double InitialStepSize { get; set; }
        = 100;

    public double StepIncreaseFactor { get; set; }
        = 1.1;

    public double StepDecreaseFactor { get; set; }
        = 0.9;
}