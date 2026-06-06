namespace AudioCompressionApp.Models;

public record DecompressionResult(short[] Samples, int SampleRate, int Channels, int BitsPerSample) {
    public short[] Samples { get; set; } = Samples;

    public int SampleRate { get; set; } = SampleRate;

    public int Channels { get; set; } = Channels;

    public int BitsPerSample { get; set; } = BitsPerSample;
    public TimeSpan DecompressionTime { get; init; }
}