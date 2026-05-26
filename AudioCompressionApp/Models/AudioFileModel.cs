namespace AudioCompressionApp.Models;

public class AudioFileModel {
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    
    public int SampleRate { get; set; }

    public int Channels { get; set; }

    public int BitsPerSample { get; set; }
}