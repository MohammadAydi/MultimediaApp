using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms;

public class PDCDecodingAlgo : DecodingAlgoBase {
    public override string Name => "Predictive Differential Coding";

    protected override long ParseInput(byte[] compressedData) {
        throw new NotImplementedException();
    }

    protected override void DecodeSample(long index) {
        throw new NotImplementedException();
    }

    protected override DecompressionResult BuildDecompressionResult() {
        throw new NotImplementedException();
    }
}