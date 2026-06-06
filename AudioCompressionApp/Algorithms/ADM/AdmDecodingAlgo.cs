using AudioCompressionApp.Algorithms.ADM.Filters;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.ADM;

public class AdmDecodingAlgo : DecodingAlgoBase {
    private AdmHeader _header;
    private double _reconstructed;
    private List<bool> _encodedBits = [];
    private double _stepSize;
    private bool? _previousBit;

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
        _stepSize = header.InitialStepSize;
        _previousBit = null;
        _reconstructed = _header.InitialPredictor;
        return header.SampleCount;
    }

    protected override void DecodeSample(long index) {
        if (_encodedBits[(int)index])
            _reconstructed += _stepSize;
        else
            _reconstructed -= _stepSize;

        DecompressedSamples[index] = (short)Math.Clamp(
            _reconstructed,
            short.MinValue,
            short.MaxValue);
        AdaptStepSize(_encodedBits[(int)index]);
    }

    protected override DecompressionResult BuildDecompressionResult() {
        
        // ── SNR comparison across all low-pass filter variants ────────────────
        // IAdmLowPassFilter[] filters = [
        //     AdmLowPassFilters.None(),
        //
        //     AdmLowPassFilters.MovingAverage(radius: 2),
        //     AdmLowPassFilters.MovingAverage(radius: 4),
        //
        //     AdmLowPassFilters.IirFirstOrder(alpha: 0.05),
        //     AdmLowPassFilters.IirFirstOrder(alpha: 0.1),
        //     AdmLowPassFilters.IirFirstOrder(alpha: 0.3),
        //     AdmLowPassFilters.IirFirstOrderFromCutoff(3400, sampleRate),
        //
        //     AdmLowPassFilters.IirCascaded(alpha: 0.2, stages: 2),
        //     AdmLowPassFilters.IirCascaded(alpha: 0.25, stages: 3),
        //
        //     AdmLowPassFilters.Butterworth(cutoffHz: 3000, sampleRate: sampleRate),
        //     AdmLowPassFilters.Butterworth(cutoffHz: 4000, sampleRate: sampleRate),
        //     AdmLowPassFilters.Butterworth(cutoffHz: 6000, sampleRate: sampleRate),
        // ];
        IAdmLowPassFilter filter = AdmLowPassFilters.IirCascaded(alpha: 0.25, stages: 3);
        short[] filtered = filter.Apply(DecompressedSamples);
        return new(_header.SampleCount > 0 ? filtered : [],
            _header.SampleRate,
            _header.Channels,
            _header.BitsPerSample);

    } 
    private void AdaptStepSize(
        bool currentBit) {
        if (_previousBit == null) {
            _previousBit = currentBit;
            return;
        }

        if (_previousBit == currentBit) {
            _stepSize = _stepSize * _header.StepIncreaseFactor;
        }
        else if (_previousBit != currentBit) {
            // _stepSize = _stepSize * _header.StepDecreaseFactor - _header.ConstFactor;
            _stepSize = _header.InitialStepSize;
        }

        _previousBit = currentBit;
    }
}