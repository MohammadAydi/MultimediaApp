using System.Diagnostics;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.Base;

public abstract class DecodingAlgoBase : IDecodingAlgo{
     protected short[] DecompressedSamples = [];
     public abstract string Name { get; }

    public Task<DecompressionResult> DecompressAsync(
        byte[] compressedData,
        IProgress<CompressionProgressModel>? progress = null,
        CancellationToken cancellationToken = default) {
        return Task.Run(() => {
            long totalSamples = ParseInput(compressedData);
            DecompressedSamples = new short[totalSamples];

            long startIndex = InitializeSamples();

            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastReportTime = 0;

            for (long i = startIndex; i < totalSamples; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                DecodeSample(i);

                if (stopwatch.ElapsedMilliseconds - lastReportTime >= 100) {
                    ReportDecompressionProgress(i, totalSamples, stopwatch, progress);
                    lastReportTime = stopwatch.ElapsedMilliseconds;
                }
            }

            // Final progress update
            ReportDecompressionProgress(totalSamples, totalSamples, stopwatch, progress);

            stopwatch.Stop();

            try {
                return BuildDecompressionResult() with { DecompressionTime = stopwatch.Elapsed };
            }
            finally {
                CleanupDecompression(); // new virtual no-op hook
            }
        }, cancellationToken);
    }

    public virtual DecompressionResult Decompress(byte[] compressedData)
        => DecompressAsync(compressedData).GetAwaiter().GetResult();
    
    protected abstract long ParseInput(byte[] compressedData);
    protected virtual long InitializeSamples() => 0;

    protected abstract void DecodeSample(long index);

    protected virtual void ReportDecompressionProgress(
        long current,
        long total,
        Stopwatch stopwatch,
        IProgress<CompressionProgressModel>? progress) {
        double percentage = (double)current / total * 100;
        double speed = current / stopwatch.Elapsed.TotalSeconds;

        progress?.Report(new CompressionProgressModel {
            Progress = percentage,
            ProcessingSpeed = speed,
            CompressionRatio = 0 // not meaningful during decompression; callers can ignore
        });
    }

    protected abstract DecompressionResult BuildDecompressionResult();

    protected virtual void CleanupDecompression() {
    }
}