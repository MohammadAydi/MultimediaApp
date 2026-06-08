namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

public class FirstOrderIirLowPassFilter : IAdmLowPassFilter
{
   
    public double Alpha { get; }

    public string Name => $"First-Order IIR LPF (α = {Alpha:F3})";

   
    public FirstOrderIirLowPassFilter(double alpha = 0.1)
    {
        if (alpha is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be strictly between 0 and 1.");

        Alpha = alpha;
    }

    public short[] Apply(short[] samples)
    {
        if (samples.Length == 0)
            return [];

        double[] filtered = new double[samples.Length];

        filtered[0] = samples[0];

        for (int i = 1; i < samples.Length; i++)
        {
            // y[n] = y[n-1] + α · (x[n] - y[n-1])
            filtered[i] = filtered[i - 1] + Alpha * (samples[i] - filtered[i - 1]);
        }

        // Convert back to PCM shorts.
        short[] result = new short[samples.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (short)Math.Clamp(
                Math.Round(filtered[i]),
                short.MinValue,
                short.MaxValue);
        }

        return result;
    }

   
    public static FirstOrderIirLowPassFilter FromCutoffFrequency(
        double cutoffHz,
        int sampleRateHz)
    {
        if (cutoffHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(cutoffHz));
        if (sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));

        // Bilinear transform approximation of α.
        double rc    = 1.0 / (2.0 * Math.PI * cutoffHz);
        double dt    = 1.0 / sampleRateHz;
        double alpha = dt / (rc + dt);

       
        alpha = Math.Clamp(alpha, 1e-6, 1.0 - 1e-6);

        return new FirstOrderIirLowPassFilter(alpha);
    }
}
