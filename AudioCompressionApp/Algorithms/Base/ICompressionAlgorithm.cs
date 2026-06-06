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
}
