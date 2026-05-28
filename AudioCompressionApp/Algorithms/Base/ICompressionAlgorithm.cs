using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.Base;

public interface ICompressionAlgorithm {
    string Name { get; }
    

    Task<CompressionResult> CompressAsync(
        CompressionContext context,
        IProgress<CompressionProgressModel> progress,
        CancellationToken cancellationToken);

    byte[] Decompress(
        byte[] compressedData);
}