namespace AudioCompressionApp.Algorithms.ADM;

public class AdmHeader {
    public int SampleRate { get; set; }

    public short Channels { get; set; }

    public short BitsPerSample { get; set; }

    public int SampleCount { get; set; }

    public double InitialStepSize { get; set; }
}
//     +-------------------+
//     | SampleRate        | 4 bytes
//     +-------------------+
//     | Channels          | 2 bytes
//     +-------------------+
//     | BitsPerSample     | 2 bytes
//     +-------------------+
//     | SampleCount       | 4 bytes
//     +-------------------+
//     | InitialStepSize   | 8 bytes
//     +-------------------+
//     | Payload Length    | 4 bytes
//     +-------------------+
//     | Payload Bytes     |
//     +-------------------+