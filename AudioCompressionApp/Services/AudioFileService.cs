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
        string extension = Path.GetExtension(filePath).ToLower();

        if (extension == ".wav") {
            samples = await GetWavAudioSamples(filePath);
        } else if (extension == ".mp3") {
            samples = await GetMp3AudioSamples(filePath);
        } else {
            throw new NotSupportedException($"Unsupported audio format: {extension}");
        }

        Console.WriteLine($"\n[Compress] Loaded {samples.Length:N0} samples — {filePath}");
        return samples;
    }

    private async Task<short[]> GetWavAudioSamples(string filePath) {
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

        return samples;
    }

    private async Task<short[]> GetMp3AudioSamples(string filePath) {
        return await Task.Run(() => {
            using var audioFileReader = new AudioFileReader(filePath);
            int sampleRate = audioFileReader.WaveFormat.SampleRate;
            int channels = audioFileReader.WaveFormat.Channels;
            
            // Read all samples into memory
            List<float> floatSamples = new();
            float[] buffer = new float[sampleRate * channels];
            int samplesRead;
            
            while ((samplesRead = audioFileReader.Read(buffer, 0, buffer.Length)) > 0) {
                for (int i = 0; i < samplesRead; i++) {
                    floatSamples.Add(buffer[i]);
                }
            }

            // Convert float samples to short
            short[] samples = new short[floatSamples.Count];
            for (int i = 0; i < floatSamples.Count; i++) {
                // Clamp to [-1, 1] range and convert to short
                float clamped = floatSamples[i];
                if (clamped > 1f) clamped = 1f;
                if (clamped < -1f) clamped = -1f;
                samples[i] = (short)(clamped * 32767f);
            }

            return samples;
        });
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