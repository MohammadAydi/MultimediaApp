namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

public interface IAdmLowPassFilter
{
    string Name { get; }

    short[] Apply(short[] samples);
}
