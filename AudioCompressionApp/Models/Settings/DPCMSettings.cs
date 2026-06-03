namespace AudioCompressionApp.Models.Settings;

 
public class DpcmSettings : CompressionSettings
{ 

    public int SampleRate    { get; init; }
    public int Channels      { get; init; }
    public int BitsPerSample { get; init; }
    public int QuantizationStep { get; init; } = 1;
}