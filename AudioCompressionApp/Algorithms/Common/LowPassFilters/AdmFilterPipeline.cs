namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;


public class AdmFilterPipeline
{
    private readonly IAdmLowPassFilter _filter;

    public string FilterName => _filter.Name;

    public AdmFilterPipeline(IAdmLowPassFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

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
