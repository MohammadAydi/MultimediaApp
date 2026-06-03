using System.IO;

namespace AudioCompressionApp.Algorithms.Nonlinear;

internal static class NonlinearQuantizationFileWriter
{
    public static byte[] Write(NonlinearQuantizationHeader header, byte[] payload)
    {
        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        header.Write(writer);
        writer.Write(payload.Length);
        writer.Write(payload);

        return ms.ToArray();
    }
}

internal static class NonlinearQuantizationFileReader
{
    public static (NonlinearQuantizationHeader Header, byte[] Payload) Read(byte[] fileData)
    {
        using MemoryStream ms = new(fileData);
        using BinaryReader reader = new(ms);

        NonlinearQuantizationHeader header = NonlinearQuantizationHeader.Read(reader);
        int payloadLength = reader.ReadInt32();
        byte[] payload = reader.ReadBytes(payloadLength);

        return (header, payload);
    }
}


