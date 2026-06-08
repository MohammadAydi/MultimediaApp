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
    
    public static string GetOutputFolder(string sourceFilePath)
    {
        string outputFolder = Path.Combine(
            Path.GetDirectoryName(sourceFilePath)!,
            "ADM_Output");

        Directory.CreateDirectory(outputFolder);

        return outputFolder;
    }
    public async Task SaveSamplesAsWaveAsync(
        short[] samples,
        int sampleRate,
        string filePath)
    {
        await Task.Run(() =>
        {
            var format = new WaveFormat(
                sampleRate,
                16, // bits per sample
                1); // mono

            using var writer = new WaveFileWriter(filePath, format);

            byte[] buffer = new byte[samples.Length * sizeof(short)];

            Buffer.BlockCopy(
                samples,
                0,
                buffer,
                0,
                buffer.Length);

            writer.Write(buffer, 0, buffer.Length);
            writer.Flush();
        });
    }

    public long GetFileSize(string filePath) {
        return new FileInfo(filePath).Length;
    }

    public async Task<short[]> GetAudioSamples(string filePath) {
    short[] samples;
    await using (var waveReader = new WaveFileReader(filePath)) {
        int bitsPerSample = waveReader.WaveFormat.BitsPerSample;
        var totalBytes = (int)waveReader.Length;
        var buffer = new byte[totalBytes];
        await waveReader.ReadExactlyAsync(buffer, 0, totalBytes);

        int bytesPerSample = bitsPerSample / 8;
        int sampleCount = totalBytes / bytesPerSample;
        samples = new short[sampleCount];

        switch (bitsPerSample) {
            case 8:
                for (int i = 0; i < sampleCount; i++)
                    samples[i] = (short)((buffer[i] - 128) << 8);
                break;

            case 16:
                for (int i = 0; i < sampleCount; i++)
                    samples[i] = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                break;

            case 24:
                for (int i = 0; i < sampleCount; i++) {
                    int raw = buffer[i * 3]
                            | (buffer[i * 3 + 1] << 8)
                            | (buffer[i * 3 + 2] << 16);
                    if ((raw & 0x800000) != 0)
                        raw |= unchecked((int)0xFF000000);
                    samples[i] = (short)(raw >> 8);
                }
                break;

            case 32:
                for (int i = 0; i < sampleCount; i++) {
                    int raw = buffer[i * 4]
                            | (buffer[i * 4 + 1] << 8)
                            | (buffer[i * 4 + 2] << 16)
                            | (buffer[i * 4 + 3] << 24);
                    samples[i] = (short)(raw >> 16);
                }
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported bit depth: {bitsPerSample}. Supported: 8, 16, 24, 32-bit PCM.");
        }
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