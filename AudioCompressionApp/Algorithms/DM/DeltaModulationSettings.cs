using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms.DM;

public class DeltaModulationSettings
    : CompressionSettings
{
    public double InitialStepSize { get; set; }
        = 100;
}