using System.IO;

namespace AudioCompressionApp.Algorithms;

public sealed class NonlinearQuantizationHeader
{
    private static readonly byte[] Magic = "NLQ1"u8.ToArray();

    public int    SampleRate       { get; init; }
    public int    Channels         { get; init; }
    public int    BitsPerSample    { get; init; }
    public long   SampleCount      { get; init; }
    public int    QuantizationBits  { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(SampleRate);
        writer.Write(Channels);
        writer.Write(BitsPerSample);
        writer.Write(SampleCount);
        writer.Write(QuantizationBits);
    }

    public static NonlinearQuantizationHeader Read(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] ||
            magic[2] != Magic[2] || magic[3] != Magic[3])
            throw new InvalidDataException("Not a valid NLQ file: magic bytes mismatch.");

        return new NonlinearQuantizationHeader
        {
            SampleRate = reader.ReadInt32(),
            Channels = reader.ReadInt32(),
            BitsPerSample = reader.ReadInt32(),
            SampleCount = reader.ReadInt64(),
            QuantizationBits = reader.ReadInt32(),
        };
    }
}


