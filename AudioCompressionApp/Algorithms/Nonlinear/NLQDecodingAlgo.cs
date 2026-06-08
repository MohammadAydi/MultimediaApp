using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;

namespace AudioCompressionApp.Algorithms.Nonlinear;

public class NLQDecodingAlgo : DecodingAlgoBase {
    private List<int> _quantized;
    private NonlinearQuantizationHeader _header;
    private double _lnMuPlus1;
    private int _levels;
    private const int Mu = 255;
    public override string Name => "Nonlinear Quantization (μ-law)";
    

    protected override long ParseInput(byte[] compressedData) {
        var (header, payload) = NonlinearQuantizationFileReader.Read(compressedData);
        _header = header;

        int qbits = Math.Max(1, header.QuantizationBits);
        long sampleCount = header.SampleCount;
        _levels = 1 << qbits;
        _lnMuPlus1 = Math.Log(1 + Mu);
        _quantized = new List<int>((int)sampleCount);

        if (qbits == 8) {
            for (int i = 0; i < sampleCount; i++)
                _quantized.Add(payload[i]);
        }
        else {
            int bitIndex = 0;
            for (int i = 0; i < sampleCount; i++) {
                int bitsLeft = qbits, v = 0, shift = 0;
                while (bitsLeft > 0) {
                    int bytePos = bitIndex / 8;
                    int offset = bitIndex % 8;
                    int space = 8 - offset;
                    int take = Math.Min(space, bitsLeft);
                    int chunk = (payload[bytePos] >> offset) & ((1 << take) - 1);
                    v |= (chunk << shift);
                    shift += take;
                    bitsLeft -= take;
                    bitIndex += take;
                }

                _quantized.Add(v);
            }
        }

        return sampleCount;
    }

    protected override void DecodeSample(long index) {
        double y = (double)_quantized[(int)index] / (_levels - 1) * 2.0 - 1.0;
        double sign = Math.Sign(y);
        double absy = Math.Abs(y);
        double expanded = absy > 0
            ? sign * (Math.Exp(absy * _lnMuPlus1) - 1.0) / Mu
            : 0.0;

        int sval = (int)Math.Round(expanded * 32767.0);
        DecompressedSamples[index] = (short)Math.Clamp(sval, short.MinValue, short.MaxValue);
    }

    protected override DecompressionResult BuildDecompressionResult()
        => new(DecompressedSamples,
            _header.SampleRate,
            (short)_header.Channels,
            (short)_header.BitsPerSample);
}