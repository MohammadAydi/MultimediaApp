using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Models;

public class CompressionContext {
    public short[] Samples { get; set; }

    public CompressionSettings Settings { get; set; }
}