using System.IO;
using AudioCompressionApp.Algorithms.ADM;
using AudioCompressionApp.Algorithms.Nonlinear;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.Base;

public interface ICompressionAlgorithm {
    string Name { get; }
    string Extension { get; }

    Task<CompressionResult> CompressAsync(
        CompressionContext context,
        IProgress<CompressionProgressModel> progress,
        CancellationToken cancellationToken);

    DecompressionResult Decompress(
        byte[] compressedData);
}

public static class CompressionAlgorithmFactory {
    public static ICompressionAlgorithm Create(string magic) {
        return magic switch {
            AdmHeader.StringMagicNumber => new AdaptiveDeltaModulationCompressionAlgorithm(),
            DpcmHeader.StringMagicNumber => new DpcmCompressionAlgorithm(),
            NonlinearQuantizationHeader.StringMagicNumber => new NonlinearQuantizationCompressionAlgorithm(),
            // "MUL1" => new N(),
            // "ALAW" => new ALawAlgorithm(),

            _ => throw new InvalidDataException(
                $"Unknown compression format '{magic}'.")
        };
    }
}