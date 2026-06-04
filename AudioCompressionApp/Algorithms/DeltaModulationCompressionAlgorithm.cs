using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class DeltaModulationCompressionAlgorithm : CompressionAlgorithmBase{
    public override string Name => "Delta Modulation";
    public override string Extension => "dmJomaat";
    protected override void ProcessSample(int index, CompressionContext context) {
        throw new NotImplementedException();
    }

    protected override double CalculateCurrentRatio() {
        throw new NotImplementedException();
    }
}