namespace AudioCompressionApp.Models.Settings;

public class NonlinearDifferentialCodingSettings : CompressionSettings
{
    // Number of bits to use for quantization per sample (default 8)
    public int QuantizationBits { get; set; } = 8;
}