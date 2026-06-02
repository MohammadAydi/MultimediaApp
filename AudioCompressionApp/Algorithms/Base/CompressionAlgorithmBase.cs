using System.Diagnostics;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

public abstract class CompressionAlgorithmBase
    : ICompressionAlgorithm {
    public abstract string Name { get; }

    protected byte[] CompressedData = [];

    public Task<CompressionResult> CompressAsync(
        CompressionContext context,
        IProgress<CompressionProgressModel> progress,
        CancellationToken cancellationToken) {
        Validate(context);

        Initialize(context);

        Stopwatch stopwatch = Stopwatch.StartNew();

        int totalSamples = context.Samples.Length;

        for (int i = 0; i < totalSamples; i++) {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessSample(i, context);

            if (i % 1000 == 0) {
                ReportProgress(
                    i,
                    totalSamples,
                    stopwatch,
                    progress);
            }
        }

        FinalizeEncoding();
        stopwatch.Stop();

        CompressionResult result = new CompressionResult
        {
            CompressionRatio = CalculateCurrentRatio(),
            CompressionTime  = stopwatch.Elapsed,
            CompressedData   = CompressedData,
        };
        return Task.FromResult(result);
    }

    public byte[] Decompress(byte[] compressedData) {
        throw new NotImplementedException();
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
        IProgress<CompressionProgressModel> progress)
    {
        double percentage = (double)current / total * 100;
        double speed      = current / stopwatch.Elapsed.TotalSeconds;

        progress?.Report(new CompressionProgressModel
        {
            Progress         = percentage,
            ProcessingSpeed  = speed,
            CompressionRatio = CalculateCurrentRatio()
        });
    }
}