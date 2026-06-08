namespace AudioCompressionApp.Algorithms.Common.LowPassFilters;

public static class AdmLowPassFilters
{
    
    public static IAdmLowPassFilter None()
        => new NoFilter();

    
    public static IAdmLowPassFilter MovingAverage(int radius = 2)
        => new MovingAverageLowPassFilter(radius);

 
    public static IAdmLowPassFilter IirFirstOrder(double alpha = 0.1)
        => new FirstOrderIirLowPassFilter(alpha);

 
    public static IAdmLowPassFilter IirFirstOrderFromCutoff(
        double cutoffHz,
        int    sampleRate)
        => FirstOrderIirLowPassFilter.FromCutoffFrequency(cutoffHz, sampleRate);



    public static IAdmLowPassFilter IirCascaded(double alpha = 0.2, int stages = 2)
        => new CascadedIirLowPassFilter(alpha, stages);

    public static IAdmLowPassFilter Butterworth(double cutoffHz, int sampleRate)
        => new ButterworthBiquadLowPassFilter(cutoffHz, sampleRate);
}
