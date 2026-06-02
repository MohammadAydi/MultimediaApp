using System.IO;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms;


public sealed class DpcmCompressionAlgorithm : CompressionAlgorithmBase
{ 
    private int _quantizationStep;
    private int _deltaBits;
 
    private int   _deltaMin;
    private int   _deltaMax;
    private int   _channels;
    private int[] _predicted = Array.Empty<int>();

    private MemoryStream?  _outputMemoryStream;
    private BinaryWriter?  _binaryWriter;
    private BitPackWriter? _bitWriter;

    private long _inputBitsTotal;
    private long _outputBitsWritten;
 

    public override string Name => "DPCM";
 
    public DpcmCompressionAlgorithm(int quantizationStep = 1, int deltaBits = 8)
    {
        _quantizationStep = quantizationStep;
        _deltaBits        = deltaBits;
    } 

    protected override void Validate(CompressionContext context)
    {
        if (context.Samples is null || context.Samples.Length == 0)
            throw new InvalidDataException(
                "No audio samples found in the compression context.");

        if (context.Settings is not DpcmSettings)
            throw new InvalidOperationException(
                $"DPCMCompressionAlgorithm requires DpcmSettings, " +
                $"but got: {context.Settings?.GetType().Name ?? "null"}");
    }

    protected override void Initialize(CompressionContext context)
    {
        var settings = (DpcmSettings)context.Settings;
 
        _quantizationStep = settings.QuantizationStep;
        _deltaBits        = settings.DeltaBits;

        _deltaMin = -(1 << (_deltaBits - 1));
        _deltaMax =  (1 << (_deltaBits - 1)) - 1;
        _channels = settings.Channels;

        _outputMemoryStream = new MemoryStream();
        _binaryWriter       = new BinaryWriter(_outputMemoryStream);

        var header = new DpcmHeader
        {
            SampleRate        = settings.SampleRate,
            Channels          = _channels,
            BitsPerSample     = settings.BitsPerSample,
            TotalSampleFrames = context.Samples.Length,
            QuantizationStep  = _quantizationStep,
            DeltaBits         = _deltaBits
        };
        header.Write(_binaryWriter);

        _predicted = new int[_channels];
        for (int ch = 0; ch < _channels; ch++)
        {
            short seed     = context.Samples[ch];
            _binaryWriter.Write(seed);
            _predicted[ch] = seed;
        }

        _bitWriter         = new BitPackWriter(_binaryWriter);
        _inputBitsTotal    = (long)context.Samples.Length * 16;
        _outputBitsWritten = 0;

        Console.WriteLine($"[DPCM] quantStep={_quantizationStep}  deltaBits={_deltaBits}" +
                          $"  deltaRange=[{_deltaMin * _quantizationStep}" +
                          $"..{_deltaMax * _quantizationStep}]");
    }

    protected override void ProcessSample(int index, CompressionContext context)
    {
        if (index < _channels)
            return;

        int   channel       = index % _channels;
        short currentSample = context.Samples[index];

        int delta          = currentSample - _predicted[channel];
        int quantisedDelta = (int)Math.Round((double)delta / _quantizationStep);
        quantisedDelta     = Math.Clamp(quantisedDelta, _deltaMin, _deltaMax);

        _bitWriter!.WriteBits(quantisedDelta, _deltaBits);
        _outputBitsWritten += _deltaBits;

        int reconstructed   = _predicted[channel] + quantisedDelta * _quantizationStep;
        _predicted[channel] = Math.Clamp(reconstructed, short.MinValue, short.MaxValue);
    }

    protected override void FinalizeEncoding()
    {
        _bitWriter?.Dispose();
        _bitWriter = null;
        _binaryWriter?.Flush();

        if (_outputMemoryStream is not null)
            CompressedData = _outputMemoryStream.ToArray();
    }

    protected override double CalculateCurrentRatio()
    {
        if (_outputBitsWritten == 0)
            return 0.0;
        return (double)_inputBitsTotal / _outputBitsWritten;
    }
 

    public override byte[] Decompress(byte[] compressedData)
    {
        using var inputStream  = new MemoryStream(compressedData);
        using var binaryReader = new BinaryReader(inputStream);

        var  header       = DpcmHeader.Read(binaryReader);
        int  quantStep    = header.QuantizationStep;
        int  deltaBits    = header.DeltaBits;
        int  channels     = header.Channels;
        long totalSamples = header.TotalSampleFrames;

        using var outputStream = new MemoryStream();
        using var pcmWriter    = new BinaryWriter(outputStream);

        var predicted = new int[channels];

        short seedSample = binaryReader.ReadInt16();
        for (int ch = 0; ch < channels; ch++)
        {
            pcmWriter.Write(BitConverter.GetBytes(seedSample));
            predicted[ch] = seedSample;
        }

        long processed = channels;
        var  bitReader  = new BitPackReader(binaryReader);

        while (processed < totalSamples)
        {
            int channel        = (int)(processed % channels);
            int quantisedDelta = bitReader.ReadBits(deltaBits);
            int reconstructed  = predicted[channel] + quantisedDelta * quantStep;
            reconstructed      = Math.Clamp(reconstructed, short.MinValue, short.MaxValue);
            predicted[channel] = reconstructed;

            pcmWriter.Write(BitConverter.GetBytes((short)reconstructed));
            processed++;
        }

        pcmWriter.Flush();
        return outputStream.ToArray();
    }
}