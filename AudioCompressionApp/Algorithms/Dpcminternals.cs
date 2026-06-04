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
    public int  RiceParameter     { get; init; }
    public int  PredictorOrder    { get; init; }   

    public void Write(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(SampleRate);
        writer.Write(Channels);
        writer.Write(BitsPerSample);
        writer.Write(TotalSampleFrames);
        writer.Write(QuantizationStep);
        writer.Write(RiceParameter);
        writer.Write(PredictorOrder);               
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
            RiceParameter     = reader.ReadInt32(),
            PredictorOrder    = reader.ReadInt32(), 
        };
    }
}

internal sealed class BitPackWriter(BinaryWriter writer) : IDisposable
{
    private ulong _buffer;
    private int   _bitsInBuffer;

    public void WriteBits(int value, int bits)
    {
        ulong masked   = (ulong)value & ((1UL << bits) - 1);
        _buffer       |= masked << _bitsInBuffer;
        _bitsInBuffer += bits;

        while (_bitsInBuffer >= 8)
        {
            writer.Write((byte)(_buffer & 0xFF));
            _buffer      >>= 8;
            _bitsInBuffer -= 8;
        }
    }

    public void WriteBit(int bit) => WriteBits(bit & 1, 1);

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

        int value     = (int)(_buffer & ((1UL << bits) - 1));
        _buffer      >>= bits;
        _bitsInBuffer -= bits;
        return value;
    }

    public int ReadBit() => ReadBits(1);
}

internal static class RiceCoder
{
    private const int EscapeThreshold = 64;

    public static void Encode(BitPackWriter bpw, int signedValue, int k)
    {
        int n = signedValue >= 0
            ? signedValue * 2
            : -signedValue * 2 - 1;

        int quotient  = n >> k;
        int remainder = n & ((1 << k) - 1);

        if (quotient >= EscapeThreshold)
        {
            for (int i = 0; i < EscapeThreshold; i++)
                bpw.WriteBit(0);
            bpw.WriteBit(1);
            bpw.WriteBits(n, 17);
        }
        else
        {
            for (int i = 0; i < quotient; i++)
                bpw.WriteBit(0);
            bpw.WriteBit(1);

            if (k > 0)
                bpw.WriteBits(remainder, k);
        }
    }

    public static int Decode(BitPackReader bpr, int k)
    {
        int quotient = 0;
        while (bpr.ReadBit() == 0)
        {
            quotient++;
            if (quotient >= EscapeThreshold)
            {
                int rawN = bpr.ReadBits(17);
                return ZigZagDecode(rawN);
            }
        }

        int remainder = k > 0 ? bpr.ReadBits(k) : 0;
        int n         = (quotient << k) | remainder;
        return ZigZagDecode(n);
    }

    private static int ZigZagDecode(int n) =>
        (n & 1) == 0 ? n >> 1 : -(n >> 1) - 1;
}

internal static class RiceParameterEstimator
{
    public static int EstimateFirstOrder(short[] samples, int channels, int quantStep)
    {
        const int maxProbe = 8_000;
        int       step     = Math.Max(1, samples.Length / maxProbe);

        var    pred   = new int[channels];
        double sumAbs = 0;
        int    count  = 0;

        for (int ch = 0; ch < channels && ch < samples.Length; ch++)
            pred[ch] = samples[ch];

        for (int i = channels; i < samples.Length; i += step)
        {
            int ch    = i % channels;
            int delta = samples[i] - pred[ch];
            int q     = (int)Math.Round((double)delta / quantStep);
            pred[ch]  = Math.Clamp(pred[ch] + q * quantStep,
                                   short.MinValue, short.MaxValue);
            sumAbs   += Math.Abs(q);
            count++;
        }

        if (count == 0 || sumAbs == 0) return 2;

        double mean = sumAbs / count;
        int    k    = (int)Math.Round(Math.Log2(mean * 1.4142));
        return Math.Clamp(k, 0, 8);
    }

    public static int EstimateSecondOrder(short[] samples, int channels, int quantStep)
    {
        if (samples.Length < channels * 2) return 2;

        const int maxProbe = 8_000;
        int       step     = Math.Max(1, samples.Length / maxProbe);

        var    prevPrev = new int[channels];
        var    prev     = new int[channels];
        double sumAbs   = 0;
        int    count    = 0;

        for (int ch = 0; ch < channels; ch++)
        {
            prevPrev[ch] = samples[ch];
            prev[ch]     = samples[ch + channels];
        }

        for (int i = channels * 2; i < samples.Length; i += step)
        {
            int ch        = i % channels;
            int predicted = Math.Clamp(
                2 * prev[ch] - prevPrev[ch],
                short.MinValue, short.MaxValue);

            int q = (int)Math.Round((double)(samples[i] - predicted) / quantStep);

            int recon = Math.Clamp(
                predicted + q * quantStep,
                short.MinValue, short.MaxValue);

            prevPrev[ch] = prev[ch];
            prev[ch]     = recon;

            sumAbs += Math.Abs(q);
            count++;
        }

        if (count == 0 || sumAbs == 0) return 0;

        double mean = sumAbs / count;
        int    k    = (int)Math.Round(Math.Log2(mean * 1.4142));
        return Math.Clamp(k, 0, 8);
    }
}