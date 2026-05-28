using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class PredictiveDifferentialCodingCompressionAlgorithm : CompressionAlgorithmBase{
    public override string Name => "Predictive Differential Coding";
    protected override void ProcessSample(int index, CompressionContext context) {
        throw new NotImplementedException();
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }
}