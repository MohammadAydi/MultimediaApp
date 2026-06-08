namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;


public class NoFilter : IAdmLowPassFilter
{
    public string Name => "No Filter (Raw ADM)";

    public short[] Apply(short[] samples)
    {
        short[] result = new short[samples.Length];
        Array.Copy(samples, result, samples.Length);
        return result;
    }
}
