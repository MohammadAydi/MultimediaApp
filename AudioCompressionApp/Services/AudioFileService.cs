using System.IO; 
using NAudio.Wave; 

namespace AudioCompressionApp.Services;

public class AudioFileService 
{ 
    public bool IsSupportedAudioFile(string path) 
    {
        string extension = Path.GetExtension(path).ToLower();
        return extension is ".wav" or ".ogg" or ".mp3";
    }   
 
    public async Task<string> SaveCompressedFileAsync(string originalPath, byte[] compressedData,
        string compressedExtension)
    {
        string outputDpcm = Path.ChangeExtension(originalPath, compressedExtension);
        await File.WriteAllBytesAsync(outputDpcm, compressedData);
        return outputDpcm;
    }
 
    public async Task<string> SaveReconstructedWavAsync(string originalPath, short[] samples, int sampleRate, int bitsPerSample, int channels)
    {
        string reconstructedWav = Path.ChangeExtension(originalPath, ".reconstructed.wav");
        var waveFormat = new WaveFormat(sampleRate, bitsPerSample, channels);
        
        await using var writer = new WaveFileWriter(reconstructedWav, waveFormat);
        foreach (var sample in samples)
        {
            writer.Write(BitConverter.GetBytes(sample), 0, 2);
        }
        
        return reconstructedWav;
    }
 
    public long GetFileSize(string filePath)
    {
        return new FileInfo(filePath).Length;
    }
}