namespace AudioCompressionApp.Models.Settings;

 
public class DpcmSettings : CompressionSettings
{ 
    public int QuantizationStep { get; init; } = 1;
    public int PredictorOrder { get; set; } = 1;
}