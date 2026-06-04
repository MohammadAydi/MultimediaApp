namespace AudioCompressionApp.Algorithms.Base;

public abstract class AlgoHeader {
    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }

    public int OriginalSampleCount { get; set; }
}