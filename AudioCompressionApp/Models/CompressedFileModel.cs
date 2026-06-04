namespace AudioCompressionApp.Models;

/// <summary>Represents a loaded compressed audio file.</summary>
public class CompressedFileModel {
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string AlgorithmName { get; set; } = string.Empty;
}