using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms.Nonlinear;

public sealed class NonlinearQuantizationCompressionAlgorithm : CompressionAlgorithmBase {
    private const int Mu = 255;

    private NonlinearDifferentialCodingSettings? _settings;
    private CompressionContext? _context;

    private readonly List<int> _quantized = new();

    public override string Name => "Nonlinear Quantization (μ-law)";
    public override string Extension => "nlqAlaa";

    
    private NonlinearQuantizationHeader _header;
    private double _lnMuPlus1;
    private int _levels;

    protected override void Validate(CompressionContext context) {
        if (context.Samples is null || context.Samples.Length == 0)
            throw new InvalidDataException("No audio samples found in the compression context.");

        if (context.Settings is not NonlinearDifferentialCodingSettings)
            throw new InvalidOperationException(
                $"NonlinearQuantizationCompressionAlgorithm requires NonlinearDifferentialCodingSettings, " +
                $"but got: {context.Settings?.GetType().Name ?? "null"}");
    }

    protected override void Initialize(CompressionContext context) {
        _context = context;
        _settings = (NonlinearDifferentialCodingSettings)context.Settings;
        _quantized.Clear();
    }

    protected override void ProcessSample(int index, CompressionContext context) {
        short sample = context.Samples[index];

        // Normalize to [-1, +1]
        double normalized = sample / 32768.0;

        // μ-law compression
        double sign = Math.Sign(normalized);
        double absx = Math.Abs(normalized);
        double compressed = 0.0;
        if (absx > 0) {
            compressed = sign * Math.Log(1 + Mu * absx) / Math.Log(1 + Mu);
        }

        int qbits = Math.Max(1, _settings!.QuantizationBits);
        int levels = 1 << qbits;

        // Map [-1,1] to [0, levels-1]
        int quantized = (int)Math.Round((compressed + 1.0) / 2.0 * (levels - 1));
        quantized = Math.Clamp(quantized, 0, levels - 1);

        _quantized.Add(quantized);
    }

    protected override void FinalizeEncoding() {
        if (_context is null || _settings is null)
            return;

        int qbits = Math.Max(1, _settings.QuantizationBits);

        // Pack quantized values into bytes
        byte[] payload;
        if (qbits == 8) {
            payload = _quantized.Select(i => (byte)i).ToArray();
        }
        else {
            int totalBits = _quantized.Count * qbits;
            int byteCount = (totalBits + 7) / 8;
            payload = new byte[byteCount];

            int bitIndex = 0;
            foreach (int value in _quantized) {
                int bitsLeft = qbits;
                int v = value;
                while (bitsLeft > 0) {
                    int bytePos = bitIndex / 8;
                    int offset = bitIndex % 8;
                    int space = 8 - offset;
                    int take = Math.Min(space, bitsLeft);

                    int mask = (1 << take) - 1;
                    int chunk = v & mask;

                    payload[bytePos] |= (byte)(chunk << offset);

                    v >>= take;
                    bitsLeft -= take;
                    bitIndex += take;
                }
            }
        }

        var header = new NonlinearQuantizationHeader {
            SampleRate = _context.Settings.SampleRate,
            Channels = _context.Settings.Channels,
            BitsPerSample = _context.Settings.BitsPerSample,
            SampleCount = _context.Samples.Length,
            QuantizationBits = _settings.QuantizationBits
        };

        CompressedData = NonlinearQuantizationFileWriter.Write(header, payload);
    }

    protected override double CalculateCurrentRatio() {
        if (_context is null || _settings is null) return 0.0;

        int srcBits = _context.BitsPerSample;
        long originalBits = (long)_context.Samples.Length * srcBits;
        long compressedBits = (long)_context.Samples.Length * Math.Max(1, _settings.QuantizationBits);
        if (compressedBits == 0) return 0.0;
        return (double)originalBits / compressedBits;
    }

}