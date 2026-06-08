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