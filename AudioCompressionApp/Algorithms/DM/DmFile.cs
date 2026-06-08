using System.IO;
using AudioCompressionApp.Algorithms.DM;

public static class DmFileWriter {
    public static byte[] Write(
        DmHeader header,
        byte[] payload) {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);


        writer.Write(DmHeader.MagicNumber);
        writer.Write(header.SampleRate);
        writer.Write(header.Channels);
        writer.Write(header.BitsPerSample);
        writer.Write(header.SampleCount);
        writer.Write(header.InitialStepSize);
        writer.Write(header.InitialPredictor);
        writer.Write(payload.Length);
        writer.Write(payload);

        return ms.ToArray();
    }
}

public static class DmFileReader {
    public static (
        DmHeader Header,
        byte[] Payload)
        Read(byte[] fileData) {
        using MemoryStream ms =
            new(fileData);

        using BinaryReader reader =
            new(ms);

        byte[] magicNumber = reader.ReadBytes(4);
        if (!magicNumber.AsSpan().SequenceEqual(DmHeader.MagicNumber)) {
            throw new InvalidDataException("Invalid ADM file.");
        }

        DmHeader header = new() {
            SampleRate = reader.ReadInt32(),
            Channels = reader.ReadInt32(),
            BitsPerSample = reader.ReadInt32(),
            SampleCount = reader.ReadInt32(),
            InitialStepSize = reader.ReadDouble(),
            InitialPredictor = reader.ReadInt16(),
        };

        Console.WriteLine("=== ADM Header ===");
        Console.WriteLine($"SampleRate         : {header.SampleRate}");
        Console.WriteLine($"Channels           : {header.Channels}");
        Console.WriteLine($"BitsPerSample      : {header.BitsPerSample}");
        Console.WriteLine($"SampleCount        : {header.SampleCount}");
        Console.WriteLine($"InitialStepSize    : {header.InitialStepSize}");
        Console.WriteLine($"InitialPredictor   : {header.InitialPredictor}");
        Console.WriteLine("==================");

        int payloadLength =
            reader.ReadInt32();

        byte[] payload =
            reader.ReadBytes(payloadLength);

        return (header, payload);
    }
}
