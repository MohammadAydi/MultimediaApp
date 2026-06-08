namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

public class ButterworthBiquadLowPassFilter : IAdmLowPassFilter
{
    private readonly double _a0;
    private readonly double _a1;
    private readonly double _a2;

    private readonly double _b1;
    private readonly double _b2;

    public double CutoffHz    { get; }
    public int    SampleRate  { get; }

    public string Name =>
        $"Butterworth Biquad LPF (fc = {CutoffHz:F0} Hz, fs = {SampleRate} Hz)";

 
    public ButterworthBiquadLowPassFilter(double cutoffHz, int sampleRateHz)
    {
        double nyquist = sampleRateHz / 2.0;
        if (cutoffHz <= 0 || cutoffHz >= nyquist)
            throw new ArgumentOutOfRangeException(
                nameof(cutoffHz),
                $"Cutoff must be between 0 and Nyquist ({nyquist:F0} Hz).");

        CutoffHz   = cutoffHz;
        SampleRate = sampleRateHz;

        double wc = 2.0 * Math.PI * cutoffHz;          
        double wd = 2.0 * sampleRateHz
                    * Math.Tan(wc / (2.0 * sampleRateHz));   

   
        const double Q = 0.70710678118; 

        double K  = wd / (2.0 * sampleRateHz);   
        double K2 = K * K;
        double norm = 1.0 / (1.0 + K / Q + K2);

        _a0 = K2 * norm;
        _a1 = 2.0 * _a0;
        _a2 = _a0;
        _b1 = 2.0 * (K2 - 1.0) * norm;
        _b2 = (1.0 - K / Q + K2) * norm;
        // ───────────────────────────────────────────────────────────────────
    }

    public short[] Apply(short[] samples)
    {
        if (samples.Length == 0)
            return [];

        short[] result = new short[samples.Length];

       
        double x1 = 0, x2 = 0;   // previous inputs
        double y1 = 0, y2 = 0;   // previous outputs

        double seed = samples[0];
        x1 = x2 = seed;
        y1 = y2 = seed;

        for (int i = 0; i < samples.Length; i++)
        {
            double x = samples[i];

            double y = _a0 * x
                     + _a1 * x1
                     + _a2 * x2
                     - _b1 * y1
                     - _b2 * y2;

            x2 = x1;  x1 = x;
            y2 = y1;  y1 = y;

            result[i] = (short)Math.Clamp(
                Math.Round(y),
                short.MinValue,
                short.MaxValue);
        }

        return result;
    }
}
