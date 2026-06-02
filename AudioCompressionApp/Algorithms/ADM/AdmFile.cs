using System.IO;

namespace AudioCompressionApp.Algorithms.ADM;

public static class AdmFileWriter {
    public static byte[] Write(
        AdmHeader header,
        byte[] payload) {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        writer.Write(header.SampleRate);
        writer.Write(header.Channels);
        writer.Write(header.BitsPerSample);
        writer.Write(header.SampleCount);
        writer.Write(header.InitialStepSize);
        writer.Write(payload.Length);
        writer.Write(payload);

        return ms.ToArray();
    }
}

public static class AdmFileReader {
    public static (
        AdmHeader Header,
        byte[] Payload)
        Read(byte[] fileData) {
        using MemoryStream ms =
            new(fileData);

        using BinaryReader reader =
            new(ms);

        AdmHeader header = new() {
            SampleRate = reader.ReadInt32(),
            Channels = reader.ReadInt16(),
            BitsPerSample = reader.ReadInt16(),
            SampleCount = reader.ReadInt32(),
            InitialStepSize = reader.ReadDouble()
        };

        int payloadLength =
            reader.ReadInt32();

        byte[] payload =
            reader.ReadBytes(payloadLength);

        return (header, payload);
    }
}

public static class AdmBitPacker {
    public static byte[] PackBits(IReadOnlyList<bool> bits) {
        int byteCount = (bits.Count + 7) / 8;
        byte[] result = new byte[byteCount];
        
        for (int i = 0; i < bits.Count; i++) {
            result[i / 8] |= (byte)(1 << (i % 8));
        }

        return result;
    }

    public static List<bool> UnpackBits(
        byte[] bytes,
        int bitCount) {
        List<bool> result =
            new(bitCount);

        for (int i = 0; i < bitCount; i++) {
            bool bit =
                (bytes[i / 8]
                 & (1 << (i % 8))) != 0;

            result.Add(bit);
        }

        return result;
    }
}