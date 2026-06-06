using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class DeltaModulationCompressionAlgorithm : CompressionAlgorithmBase{
    public override string Name => "Delta Modulation";
    public override string Extension => "dmJomaat";
    protected override long ParseInput(byte[] compressedData) {
        throw new NotImplementedException();
    }

    protected override void DecodeSample(long index) {
        throw new NotImplementedException();
    }

    protected override DecompressionResult BuildDecompressionResult() {
        throw new NotImplementedException();
    }

    protected override void ProcessSample(int index, CompressionContext context) {
        throw new NotImplementedException();
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }
}