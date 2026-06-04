using System.IO;
using System.Text;
using NAudio.Wave;

namespace AudioCompressionApp.Services;

public class AudioFileService {
    public bool IsSupportedAudioFile(string path) {
        string extension = Path.GetExtension(path).ToLower();
        return extension is ".wav" or ".ogg" or ".mp3";
    }

    public async Task<string?> SaveBytesToFileAsync(string? path, byte[] compressedData) {
        if (path == null) return null;
        await File.WriteAllBytesAsync(path, compressedData);
        return path;
    }

    public long GetFileSize(string filePath) {
        return new FileInfo(filePath).Length;
    }

    public async Task<short[]> GetAudioSamples(string filePath) {
        short[] samples;
        await using (var waveReader = new WaveFileReader(filePath)) {
            var totalBytes = (int)waveReader.Length;
            var buffer = new byte[totalBytes];
            await waveReader.ReadExactlyAsync(buffer, 0, totalBytes);

            samples = new short[totalBytes / 2];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
        }

        Console.WriteLine($"\n[Compress] Loaded {samples.Length:N0} samples — {filePath}");
        return samples;
    }

    public async Task<byte[]> GetFileBytes(string filePath) {
        byte[] compressedBytes =
            await File.ReadAllBytesAsync(filePath);
        return compressedBytes;
    }

    public async Task<String> GetMagicName(String filePath) {
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        string magic = Encoding.ASCII.GetString(fileBytes, 0, 4);
        return magic;
    }
}