using System.IO;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class DPCMDecodingAlgo : DecodingAlgoBase{
    
    private int[] _prev = Array.Empty<int>();
    private int[] _prevPrev = Array.Empty<int>();
    private DpcmHeader _header;
    private BitPackReader _bitReader = null!;


    public override string Name => "DPCM";

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