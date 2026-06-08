namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;


public class MovingAverageLowPassFilter : IAdmLowPassFilter
{

    public int Radius { get; }

    public string Name => $"Moving Average LPF (window = {2 * Radius + 1})";


    public MovingAverageLowPassFilter(int radius = 2)
    {
        if (radius < 1)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be at least 1.");

        Radius = radius;
    }

    public short[] Apply(short[] samples)
    {
        int n = samples.Length;
        short[] filtered = new short[n];

        for (int i = 0; i < n; i++)
        {
            // Clamp the window to valid indices
            int start = Math.Max(0, i - Radius);
            int end   = Math.Min(n - 1, i + Radius);
            int count = end - start + 1;

            double sum = 0;
            for (int j = start; j <= end; j++)
                sum += samples[j];

            filtered[i] = (short)Math.Clamp(
                Math.Round(sum / count),
                short.MinValue,
                short.MaxValue);
        }

        return filtered;
    }
}
