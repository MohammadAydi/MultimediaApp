using System;

namespace AudioCompressionApp.Algorithms.Base;

public static class SignalQualityAnalyzer
{
    public static double ComputeSnrDb(short[] original, short[] reconstructed)
    {
        double signalPower = 0, noisePower = 0;
        int count = Math.Min(original.Length, reconstructed.Length);
        
        for (int i = 0; i < count; i++)
        {
            double s = original[i];
            double e = original[i] - reconstructed[i];
            signalPower += s * s;
            noisePower  += e * e;
        }

        return noisePower < double.Epsilon 
            ? double.PositiveInfinity 
            : 10.0 * Math.Log10(signalPower / noisePower);
    }
}