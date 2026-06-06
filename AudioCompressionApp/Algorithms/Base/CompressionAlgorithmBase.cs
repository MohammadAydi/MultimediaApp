using System.Diagnostics;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Services;

public abstract class CompressionAlgorithmBase
    : ICompressionAlgorithm {
    public abstract string Name { get; }
    public abstract string Extension { get; }

    protected byte[] CompressedData = [];


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