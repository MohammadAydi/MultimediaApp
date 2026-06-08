
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Algorithms.Common;
using AudioCompressionApp.Algorithms.Common.LowPassFilters;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.DM;

public class DmDecodingAlgo : DecodingAlgoBase {
    private DmHeader _header;
    private double _reconstructed;
    private List<bool> _encodedBits = [];
    private double _stepSize;

    public override string Name => "Adaptive Delta Modulation";

   

    protected override long ParseInput(byte[] compressedData) {
        var (header, payload) = DmFileReader.Read(compressedData);
        _header = header;
        _encodedBits = BitPacker.UnpackBits(payload, header.SampleCount);
        _stepSize = header.InitialStepSize;
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
}