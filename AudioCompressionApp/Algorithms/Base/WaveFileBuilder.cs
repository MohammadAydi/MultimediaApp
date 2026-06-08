using System.IO;
using AudioCompressionApp.Models;
using NAudio.Wave;

namespace AudioCompressionApp.Algorithms.Base;

public static class WaveFileBuilder {
    public static byte[] CreateWaveFile(DecompressionResult result) {
        using MemoryStream stream = new();

        WaveFormat format = new WaveFormat(result.SampleRate, result.BitsPerSample, result.Channels);
        using (WaveFileWriter writer = new WaveFileWriter(stream, format)) {
            if (result.BitsPerSample == 32) {
                byte[] pcmBytes = new byte[result.Samples.Length * 4];
                for (int i = 0; i < result.Samples.Length; i++) {
                    int val = result.Samples[i] << 16;
                    pcmBytes[i * 4]     = (byte)(val & 0xFF);
                    pcmBytes[i * 4 + 1] = (byte)((val >> 8) & 0xFF);
                    pcmBytes[i * 4 + 2] = (byte)((val >> 16) & 0xFF);
                    pcmBytes[i * 4 + 3] = (byte)((val >> 24) & 0xFF);
                }
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }
            else {
                // 16-bit PCM
                byte[] pcmBytes = new byte[result.Samples.Length * 2];
                Buffer.BlockCopy(result.Samples, 0, pcmBytes, 0, pcmBytes.Length);
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }
        }
        return stream.ToArray();
    }
}