namespace AudioCompressionApp.Models.Settings;

public abstract class CompressionSettings {
    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }
}