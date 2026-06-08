namespace AudioCompressionApp.Algorithms.DM;

public class DmHeader {
    public static readonly byte[] MagicNumber = "DM11"u8.ToArray();
    public const string StringMagicNumber = "DM11";

    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }

    public int SampleCount { get; set; }

    public double InitialStepSize { get; set; }
    public short InitialPredictor { get; set; }
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