using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class NonlinearQuantizationCompressionAlgorithm : CompressionAlgorithmBase{
    public override string Name => "Nonlinear Quantization";
    protected override void ProcessSample(int index, CompressionContext context) {
        throw new NotImplementedException();
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }
}