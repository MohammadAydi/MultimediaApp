namespace AudioCompressionApp.Models.Settings;

 
public class DpcmSettings : CompressionSettings
{
    // ─── معلومات صيغة الصوت (مصدرها WaveFormat قبل تحميل العينات) ───────

    public int SampleRate    { get; set; }
    public int Channels      { get; set; }
    public int BitsPerSample { get; set; }

    // ─── معاملات خوارزمية DPCM ───────────────────────────────────────────

    /// <summary>خطوة التكميم، بين 1 و 32767. كلما زادت قلّت الجودة وزاد الضغط.</summary>
    public int QuantizationStep { get; set; } = 1;

    /// <summary>عدد البتات لكل delta، بين 2 و 16.</summary>
    public int DeltaBits { get; set; } = 8;
}