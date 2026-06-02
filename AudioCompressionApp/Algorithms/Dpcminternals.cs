using System.IO;

namespace AudioCompressionApp.Algorithms;

internal sealed class DpcmHeader
{
    private static readonly byte[] Magic = "DPCM"u8.ToArray();

    public int  SampleRate        { get; init; }
    public int  Channels          { get; init; }
    public int  BitsPerSample     { get; init; }
    public long TotalSampleFrames { get; init; }
    public int  QuantizationStep  { get; init; }
    public int  DeltaBits         { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(SampleRate);
        writer.Write(Channels);
        writer.Write(BitsPerSample);
        writer.Write(TotalSampleFrames);
        writer.Write(QuantizationStep);
        writer.Write(DeltaBits);
    }

    public static DpcmHeader Read(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(4);
        if (magic[0] != Magic[0] || magic[1] != Magic[1] ||
            magic[2] != Magic[2] || magic[3] != Magic[3])
            throw new InvalidDataException("Not a valid DPCM file: magic bytes mismatch.");

        return new DpcmHeader
        {
            SampleRate        = reader.ReadInt32(),
            Channels          = reader.ReadInt32(),
            BitsPerSample     = reader.ReadInt32(),
            TotalSampleFrames = reader.ReadInt64(),
            QuantizationStep  = reader.ReadInt32(),
            DeltaBits         = reader.ReadInt32()
        };
    }
}
 
internal sealed class BitPackWriter(BinaryWriter writer) : IDisposable
{
    private ulong _buffer;
    private int   _bitsInBuffer;

    public void WriteBits(int value, int bits)
    {
        ulong masked = (ulong)value & ((1UL << bits) - 1);

        _buffer       |= masked << _bitsInBuffer;
        _bitsInBuffer += bits;

        while (_bitsInBuffer >= 8)
        {
            writer.Write((byte)(_buffer & 0xFF));
            _buffer       >>= 8;
            _bitsInBuffer  -= 8;
        }
    }

    private void Flush()
    {
        if (_bitsInBuffer <= 0) return;
        writer.Write((byte)(_buffer & 0xFF));
        _buffer       = 0;
        _bitsInBuffer = 0;
    }

    public void Dispose() => Flush();
}
 
internal sealed class BitPackReader(BinaryReader reader)
{
    private ulong _buffer;
    private int   _bitsInBuffer;

    public int ReadBits(int bits)
    {
        while (_bitsInBuffer < bits)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
                break;

            _buffer       |= (ulong)reader.ReadByte() << _bitsInBuffer;
            _bitsInBuffer += 8;
        }

        var raw   = _buffer & ((1UL << bits) - 1);
        _buffer     >>= bits;
        _bitsInBuffer -= bits;
 
        var signBit = 1 << (bits - 1);
        var value   = (int)raw;
        if ((value & signBit) != 0)
            value -= (1 << bits);

        return value;
    }
}