using AudioCompressionApp.Algorithms.ADM;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms;

public class AdaptiveDeltaModulationCompressionAlgorithm : CompressionAlgorithmBase {
    private readonly List<bool> _encodedBits = [];
    private double _reconstructed;

    private double _stepSize;

    private AdaptiveDeltaModulationSettings? _settings;

    private CompressionContext? _context;


    public override string Name => "Adaptive Delta Modulation";

    protected override void ProcessSample(int index, CompressionContext context) {
        short sample = context.Samples[index];
        bool bit = sample >= _reconstructed;
        _encodedBits.Add(bit);
        if (bit)
            _reconstructed += _stepSize;
        else
            _reconstructed -= _stepSize;
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }

    protected override void Initialize(CompressionContext context) {
        _context = context;
        _settings = (AdaptiveDeltaModulationSettings)context.Settings;
        _encodedBits.Clear();
        CompressedData = [];
        _reconstructed = 0;
        _stepSize = _settings.InitialStepSize;
    }

    protected override void FinalizeEncoding() {
        byte[] payload = AdmBitPacker.PackBits(_encodedBits);
        AdmHeader header =
            new() {
                SampleRate = _context.SampleRate,
                Channels = _context.Channels,
                BitsPerSample = _context.BitsPerSample,

                SampleCount =
                    _context!.Samples.Length,

                InitialStepSize =
                    _stepSize
            };

        CompressedData = AdmFileWriter.Write(header, payload);
    }

    public override DecompressionResult Decompress(
        byte[] compressedData) {
        var (header, payload) = AdmFileReader.Read(compressedData);
        List<bool> bits = AdmBitPacker.UnpackBits(payload, header.SampleCount);
        short[] samples = new short[header.SampleCount];
        double reconstructed = 0;

        double stepSize =
            header.InitialStepSize;

        for (int i = 0; i < bits.Count; i++) {
            if (bits[i]) {
                reconstructed += stepSize;
            }
            else {
                reconstructed -= stepSize;
            }

            samples[i] =
                (short)Math.Clamp(
                    reconstructed,
                    short.MinValue,
                    short.MaxValue);
        }

        return new DecompressionResult(samples, header.SampleRate, header.Channels, header.BitsPerSample);
    }
}