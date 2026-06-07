using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Models;

public class CompressionContext {
    public short[] Samples { get; set; }

    public CompressionSettings Settings { get; set; }

    /// <summary>
    /// Bit depth of the original WAV (16 or 32).
    /// Used by algorithms that need correct normalization (e.g. Nonlinear μ-law).
    /// </summary>
    public int BitsPerSample { get; set; } = 16;
}