using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.ADM;

public class AdmDecodingAlgo : DecodingAlgoBase {
    private AdmHeader _header;
    private double _reconstructed = 0;
    private List<bool> _encodedBits = [];
    public override string Name => "Adaptive Delta Modulation";

    public override DecompressionResult Decompress(
        byte[] compressedData) {
        var (header, payload) = AdmFileReader.Read(compressedData);
        List<bool> bits = AdmBitPacker.UnpackBits(payload, header.SampleCount);
        short[] samples = new short[header.SampleCount];
        double reconstructed = 0;

        double stepSize =
            header.InitialStepSize;

        for (int i = 0; i < bits.Count; i++) {
            double previousReconstructed = reconstructed;

            if (bits[i]) {
                reconstructed += stepSize;
            }
            else {
                reconstructed -= stepSize;
            }

            // Console.WriteLine(
            //     $"[{i}] " +
            //     $"Bit={(bits[i] ? 1 : 0)}, " +
            //     $"PreviousEstimate={previousReconstructed}, " +
            //     $"UpdatedEstimate={reconstructed}");

            samples[i] = (short)Math.Clamp(
                reconstructed,
                short.MinValue,
                short.MaxValue);
        }

        return new DecompressionResult(samples, header.SampleRate, header.Channels, header.BitsPerSample);
    }

    protected override long ParseInput(byte[] compressedData) {
        var (header, payload) = AdmFileReader.Read(compressedData);
        _header = header;
        _encodedBits = AdmBitPacker.UnpackBits(payload, header.SampleCount);
        return header.SampleCount;
    }

    protected override void DecodeSample(long index) {
        if (_encodedBits[(int)index])
            _reconstructed += _header.InitialStepSize;
        else
            _reconstructed -= _header.InitialStepSize;

        DecompressedSamples[index] = (short)Math.Clamp(
            _reconstructed,
            short.MinValue,
            short.MaxValue);
    }

    protected override DecompressionResult BuildDecompressionResult()
        => new(_header.SampleCount > 0 ? DecompressedSamples : [],
            _header.SampleRate,
            _header.Channels,
            _header.BitsPerSample);
}