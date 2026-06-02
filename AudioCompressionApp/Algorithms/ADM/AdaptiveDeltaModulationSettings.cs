namespace AudioCompressionApp.Models.Settings;

public class AdaptiveDeltaModulationSettings
    : CompressionSettings
{
    public double InitialStepSize { get; set; }
        = 100;
}