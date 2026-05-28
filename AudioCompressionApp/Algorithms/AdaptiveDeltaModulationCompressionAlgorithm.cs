using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class AdaptiveDeltaModulationCompressionAlgorithm : CompressionAlgorithmBase {
    public override string Name => "Adaptive Delta Modulation";
    protected override void ProcessSample(int index, CompressionContext context) {
        throw new NotImplementedException();
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }
}