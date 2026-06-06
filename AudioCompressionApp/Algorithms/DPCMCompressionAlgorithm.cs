using System.IO;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms;

public sealed class DpcmCompressionAlgorithm : CompressionAlgorithmBase {
    private int _quantizationStep;
    private int _riceK;
    private int _order;

    private int _channels;
    private int[] _prev = Array.Empty<int>();
    private int[] _prevPrev = Array.Empty<int>();

    private MemoryStream? _outputMemoryStream;
    private BinaryWriter? _binaryWriter;
    private BitPackWriter? _bitWriter;

    private long _inputBitsTotal;

    private DpcmHeader _header;
    private BitPackReader _bitReader = null!;

    public override string Name => "DPCM";
    public override string Extension => "dpcmKasem";

    public DpcmCompressionAlgorithm(int quantizationStep = 1) {
        _quantizationStep = quantizationStep;
        _riceK = -1;
    }

    protected override void Validate(CompressionContext context) {
        if (context.Samples is null || context.Samples.Length == 0)
            throw new InvalidDataException(
                "No audio samples found in the compression context.");

        if (context.Settings is not DpcmSettings settings)
            throw new InvalidOperationException(
                $"DpcmCompressionAlgorithm requires DpcmSettings, " +
                $"but got: {context.Settings?.GetType().Name ?? "null"}");

        if (settings.PredictorOrder == 2 && context.Samples.Length < settings.Channels * 2)
            throw new InvalidDataException(
                "Audio too short for second-order DPCM (need ≥ 2 sample frames).");
    }

    protected override void Initialize(CompressionContext context) {
        var settings = (DpcmSettings)context.Settings;

        _quantizationStep = settings.QuantizationStep;
        _channels = settings.Channels;
        _order = settings.PredictorOrder == 2 ? 2 : 1;

        _riceK = _order == 2
            ? RiceParameterEstimator.EstimateSecondOrder(context.Samples, _channels, _quantizationStep)
            : RiceParameterEstimator.EstimateFirstOrder(context.Samples, _channels, _quantizationStep);

        _outputMemoryStream = new MemoryStream();
        _binaryWriter = new BinaryWriter(_outputMemoryStream);

        new DpcmHeader {
            SampleRate = settings.SampleRate,
            Channels = _channels,
            BitsPerSample = settings.BitsPerSample,
            TotalSampleFrames = context.Samples.Length,
            QuantizationStep = _quantizationStep,
            RiceParameter = _riceK,
            PredictorOrder = _order,
        }.Write(_binaryWriter);

        _prev = new int[_channels];
        _prevPrev = new int[_channels];

        if (_order == 2) {
            for (int ch = 0; ch < _channels; ch++) {
                short s0 = context.Samples[ch];
                short s1 = context.Samples[ch + _channels];
                _binaryWriter.Write(s0);
                _binaryWriter.Write(s1);
                _prevPrev[ch] = s0;
                _prev[ch] = s1;
            }
        }
        else {
            for (int ch = 0; ch < _channels; ch++) {
                short seed = context.Samples[ch];
                _binaryWriter.Write(seed);
                _prev[ch] = seed;
            }
        }

        _bitWriter = new BitPackWriter(_binaryWriter);
        _inputBitsTotal = (long)context.Samples.Length * 16;

        Console.WriteLine($"[DPCM-{_order}] quantStep={_quantizationStep}  riceK={_riceK}  channels={_channels}");
    }

    protected override void ProcessSample(int index, CompressionContext context) {
        if (index < _channels * _order)
            return;

        int ch = index % _channels;
        short currentSample = context.Samples[index];

        int predicted = _order == 2
            ? Math.Clamp(2 * _prev[ch] - _prevPrev[ch], short.MinValue, short.MaxValue)
            : _prev[ch];

        int quantisedDelta = (int)Math.Round((double)(currentSample - predicted) / _quantizationStep);

        RiceCoder.Encode(_bitWriter!, quantisedDelta, _riceK);

        int reconstructed = Math.Clamp(
            predicted + quantisedDelta * _quantizationStep,
            short.MinValue, short.MaxValue);

        _prevPrev[ch] = _prev[ch];
        _prev[ch] = reconstructed;
    }

    protected override void FinalizeEncoding() {
        _bitWriter?.Dispose();
        _bitWriter = null;
        _binaryWriter?.Flush();

        if (_outputMemoryStream is not null)
            CompressedData = _outputMemoryStream.ToArray();
    }

    protected override double CalculateCurrentRatio() {
        long outputBytes = _outputMemoryStream?.Position ?? 0;
        if (outputBytes == 0) return 0.0;
        return (double)_inputBitsTotal / (outputBytes * 8);
    }

    public override DecompressionResult Decompress(byte[] compressedData) {
        using var inputStream = new MemoryStream(compressedData);
        using var binaryReader = new BinaryReader(inputStream);

        var header = DpcmHeader.Read(binaryReader);
        int quantStep = header.QuantizationStep;
        int riceK = header.RiceParameter;
        int channels = header.Channels;
        long totalSamples = header.TotalSampleFrames;
        int order = header.PredictorOrder;

        short[] samples = new short[totalSamples];
        var prev = new int[channels];
        var prevPrev = new int[channels];
        long processed;

        if (order == 2) {
            for (int ch = 0; ch < channels; ch++) {
                short s0 = binaryReader.ReadInt16();
                short s1 = binaryReader.ReadInt16();
                samples[ch] = s0;
                samples[ch + channels] = s1;
                prevPrev[ch] = s0;
                prev[ch] = s1;
            }

            processed = channels * 2;
        }
        else {
            for (int ch = 0; ch < channels; ch++) {
                short seed = binaryReader.ReadInt16();
                samples[ch] = seed;
                prev[ch] = seed;
            }

            processed = channels;
        }

        var bitReader = new BitPackReader(binaryReader);

        while (processed < totalSamples) {
            int channel = (int)(processed % channels);

            int predicted = order == 2
                ? Math.Clamp(2 * prev[channel] - prevPrev[channel], short.MinValue, short.MaxValue)
                : prev[channel];

            int quantisedDelta = RiceCoder.Decode(bitReader, riceK);

            int reconstructed = Math.Clamp(
                predicted + quantisedDelta * quantStep,
                short.MinValue, short.MaxValue);

            prevPrev[channel] = prev[channel];
            prev[channel] = reconstructed;
            samples[processed] = (short)reconstructed;
            processed++;
        }

        return new DecompressionResult(
            samples,
            header.SampleRate,
            (short)header.Channels,
            (short)header.BitsPerSample);
    }

    protected override long ParseInput(byte[] compressedData) {
        _inputStream = new MemoryStream(compressedData);
        _binaryReader = new BinaryReader(_inputStream);

        _header = DpcmHeader.Read(_binaryReader);
        _prev = new int[_header.Channels];
        _prevPrev = new int[_header.Channels];

        return _header.TotalSampleFrames;
    }

    private MemoryStream _inputStream = null!;
    private BinaryReader _binaryReader = null!;

    protected override long InitializeSamples() {
        int channels = _header.Channels;

        if (_header.PredictorOrder == 2) {
            for (int ch = 0; ch < channels; ch++) {
                short s0 = _binaryReader.ReadInt16();
                short s1 = _binaryReader.ReadInt16();
                DecompressedSamples[ch] = s0;
                DecompressedSamples[ch + channels] = s1;
                _prevPrev[ch] = s0;
                _prev[ch] = s1;
            }

            _bitReader = new BitPackReader(_binaryReader);
            return channels * 2L; // main loop starts here
        }

        for (int ch = 0; ch < channels; ch++) {
            short seed = _binaryReader.ReadInt16();
            DecompressedSamples[ch] = seed;
            _prev[ch] = seed;
        }

        _bitReader = new BitPackReader(_binaryReader);
        return channels;
    }

    protected override void DecodeSample(long index) {
        int channel = (int)(index % _header.Channels);
        int predicted = _header.PredictorOrder == 2
            ? Math.Clamp(2 * _prev[channel] - _prevPrev[channel],
                short.MinValue, short.MaxValue)
            : _prev[channel];

        int quantisedDelta = RiceCoder.Decode(_bitReader, _header.RiceParameter);
        int reconstructed = Math.Clamp(
            predicted + quantisedDelta * _header.QuantizationStep,
            short.MinValue, short.MaxValue);

        _prevPrev[channel] = _prev[channel];
        _prev[channel] = reconstructed;
        DecompressedSamples[index] = (short)reconstructed;
    }

    protected override DecompressionResult BuildDecompressionResult() {
        _binaryReader.Dispose();
        _inputStream.Dispose();

        return new DecompressionResult(
            DecompressedSamples,
            _header.SampleRate,
            (short)_header.Channels,
            (short)_header.BitsPerSample);
    }
    
    protected override void CleanupDecompression()
    {
        _binaryReader?.Dispose();
        _inputStream?.Dispose();
    }
}