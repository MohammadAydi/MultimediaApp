using System.IO;
using AudioCompressionApp.Models;
using NAudio.Wave;

namespace AudioCompressionApp.Algorithms;

public static class WaveFileBuilder {
    public static byte[] CreateWaveFile(DecompressionResult result) {
        using MemoryStream stream = new();

        WaveFormat format = new WaveFormat(result.SampleRate, result.BitsPerSample, result.Channels);
        using (WaveFileWriter writer = new WaveFileWriter(stream, format)) {
            byte[] pcmBytes = new byte[result.Samples.Length * 2];

            Buffer.BlockCopy(
                result.Samples,
                0,
                pcmBytes,
                0,
                pcmBytes.Length);

            writer.Write(
                pcmBytes,
                0,
                pcmBytes.Length);
        }
        return stream.ToArray();
    }
}