namespace AudioCompressionApp.Models;

public record DecompressionResult {
    public DecompressionResult(short[] samples, int sampleRate, int channels, int bitsPerSample ) {
        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
        
    }

    public short[] Samples { get; set; }

    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }
    public TimeSpan DecompressionTime { get; init; }
}