namespace AudioCompressionApp.Models;

public class CompressionResult {
    public byte[] CompressedData { get; set; }
        = [];

    public double CompressionRatio { get; set; }

    public TimeSpan CompressionTime { get; set; }

    public string AlgorithmName { get; set; }
        = string.Empty;
}