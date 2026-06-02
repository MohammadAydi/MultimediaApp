namespace AudioCompressionApp.Models;

public class DecompressionResult {
    public DecompressionResult(short[] samples, int sampleRate, short channels, short bitsPerSample) {
        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
        BitsPerSample = bitsPerSample;
    }

    public short[] Samples { get; set; }

    public int SampleRate { get; set; }

    public short Channels { get; set; }

    public short BitsPerSample { get; set; }
}