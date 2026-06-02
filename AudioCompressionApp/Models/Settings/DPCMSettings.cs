namespace AudioCompressionApp.Models.Settings;

 
public class DpcmSettings : CompressionSettings
{ 

    public int SampleRate    { get; set; }
    public int Channels      { get; set; }
    public int BitsPerSample { get; set; }
 
 
    public int QuantizationStep { get; set; } = 1;
 
    public int DeltaBits { get; set; } = 8;
}