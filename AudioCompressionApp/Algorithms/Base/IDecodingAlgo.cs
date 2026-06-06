using System.IO;
using AudioCompressionApp.Algorithms.ADM;
using AudioCompressionApp.Algorithms.Nonlinear;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.Base;

public interface IDecodingAlgo {
    public string Name { get; }
    public DecompressionResult Decompress(byte[] compressedData);

    public Task<DecompressionResult> DecompressAsync(
        byte[] compressedData,
        IProgress<CompressionProgressModel>? progress = null,
        CancellationToken cancellationToken = default);
}

public static class DecodingAlgorithmFactory {
    public static IDecodingAlgo Create(string magic) {
        return magic switch {
            AdmHeader.StringMagicNumber => new AdmDecodingAlgo(),
            DpcmHeader.StringMagicNumber => new DPCMDecodingAlgo(),
            NonlinearQuantizationHeader.StringMagicNumber => new NLQDecodingAlgo(),
            // "MUL1" => new N(),
            // "ALAW" => new ALawAlgorithm(),

            _ => throw new InvalidDataException(
                $"Unknown compression format '{magic}'.")
        };
    }
}