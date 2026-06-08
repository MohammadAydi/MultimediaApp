namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

/// <summary>
/// Wraps a chosen <see cref="IAdmLowPassFilter"/> and exposes a single
/// Apply() method that operates on the raw samples produced by
/// <see cref="AdmDecodingAlgo"/>.
/// 
/// Typical use:
/// <code>
///     var decoded = decodingAlgo.Decompress(compressedBytes);
///     var pipeline = new AdmFilterPipeline(new ButterworthBiquadLowPassFilter(4000, decoded.SampleRate));
///     short[] cleaned = pipeline.Apply(decoded.Samples);
/// </code>
/// </summary>
public class AdmFilterPipeline
{
    private readonly IAdmLowPassFilter _filter;

    public string FilterName => _filter.Name;

    public AdmFilterPipeline(IAdmLowPassFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    /// <summary>
    /// Run the filter and return the smoothed samples.
    /// Input and output arrays are always the same length.
    /// </summary>
    public short[] Apply(short[] rawSamples)
    {
        if (rawSamples is null || rawSamples.Length == 0)
            return [];

        Console.WriteLine($"[AdmFilterPipeline] Applying: {_filter.Name}");
        short[] result = _filter.Apply(rawSamples);
        Console.WriteLine($"[AdmFilterPipeline] Done. {result.Length} samples processed.");
        return result;
    }
}
