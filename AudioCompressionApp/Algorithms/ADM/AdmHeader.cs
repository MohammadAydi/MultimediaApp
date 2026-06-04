namespace AudioCompressionApp.Algorithms.ADM;

public class AdmHeader {
    public static readonly byte[] MagicNumber = "ADM1"u8.ToArray();
    public const string StringMagicNumber = "ADM1";

    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }

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