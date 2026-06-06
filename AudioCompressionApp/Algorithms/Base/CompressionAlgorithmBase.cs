using System.Diagnostics;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Services;

public abstract class CompressionAlgorithmBase
    : ICompressionAlgorithm {
    public abstract string Name { get; }
    public abstract string Extension { get; }

    protected byte[] CompressedData = [];

    protected short[] DecompressedSamples = [];
    protected abstract long ParseInput(byte[] compressedData);
    protected virtual long InitializeSamples() => 0;

    protected abstract void DecodeSample(long index);
    protected abstract DecompressionResult BuildDecompressionResult();

    protected virtual void CleanupDecompression() {
    }


    public Task<CompressionResult> CompressAsync(
        CompressionContext context,
        IProgress<CompressionProgressModel> progress,
        CancellationToken cancellationToken) {
        return Task.Run(() => {
            Validate(context);

            Initialize(context);

            Stopwatch stopwatch = Stopwatch.StartNew();

            int totalSamples = context.Samples.Length;
            long lastReportTime = 0;

            for (int i = 0; i < totalSamples; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                ProcessSample(i, context);

                // Report progress at most every 100 ms
                if (stopwatch.ElapsedMilliseconds - lastReportTime >= 100) {
                    ReportProgress(
                        i,
                        totalSamples,
                        stopwatch,
                        progress);

                    lastReportTime = stopwatch.ElapsedMilliseconds;
                }
            }

            // Final progress update
            ReportProgress(
                totalSamples,
                totalSamples,
                stopwatch,
                progress);

            FinalizeEncoding();
            stopwatch.Stop();
            return new CompressionResult {
                CompressionRatio = CalculateCurrentRatio(),
                CompressionTime = stopwatch.Elapsed,
                CompressedData = CompressedData
            };
        }, cancellationToken);
    }

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

    protected virtual void Validate(CompressionContext context) {
    }

    protected virtual void Initialize(CompressionContext context) {
    }

    protected abstract void ProcessSample(int index, CompressionContext context);
    protected abstract double CalculateCurrentRatio();

    protected virtual void FinalizeEncoding() {
    }

    protected virtual void ReportProgress(
        int current,
        int total,
        Stopwatch stopwatch,
        IProgress<CompressionProgressModel> progress) {
        double percentage = (double)current / total * 100;
        double speed = current / stopwatch.Elapsed.TotalSeconds;

        progress?.Report(new CompressionProgressModel {
            Progress = percentage,
            ProcessingSpeed = speed,
            CompressionRatio = CalculateCurrentRatio()
        });
    }
}