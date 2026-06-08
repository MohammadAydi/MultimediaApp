using AudioCompressionApp.Algorithms.Common;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;

namespace AudioCompressionApp.Algorithms.ADM;

public class AdaptiveDeltaModulationCompressionAlgorithm : CompressionAlgorithmBase {
    private List<bool> _encodedBits = [];
    private double _reconstructed;

    private double _stepSize;
    private bool? _previousBit;

    private AdaptiveDeltaModulationSettings? _settings;

    private CompressionContext? _context;


    public long BitsWritten { get; private set; }
    private int _processedSamples;


    public override string Name => "Adaptive Delta Modulation";
    public override string Extension => "admAydi";

    private readonly Dictionary<int, int> _diffHistogram = new();

    protected override void ProcessSample(int index, CompressionContext context) {
        _processedSamples++;
        short sample = context.Samples[index];
        if (index > 0) {
            int previous = context.Samples[index - 1];

            int diff = Math.Abs(sample - previous);

            // Bucket size = 100
            int bucket = (diff / 100) * 100;

            _diffHistogram.TryAdd(bucket, 0);
            _diffHistogram[bucket]++;
        }


        // Current estimate
        double estimate = _reconstructed;

        // Error
        double error = sample - estimate;

        // Quantizer decision
        bool bit = error >= 0;

        // Transmitted value
        double transmitted = bit ? _stepSize : -_stepSize;

        // Update accumulator
        _reconstructed += transmitted;

        _encodedBits.Add(bit);
        BitsWritten++;
        AdaptStepSize(bit);

        // Console.WriteLine(
        //     $"[{index}] " +
        //     $"Input={sample}, " +
        //     $"Estimate={estimate}, " +
        //     $"Error={error}, " +
        //     $"Bit={(bit ? 1 : 0)}, " +
        //     $"Transmit={transmitted}, " +
        //     $"UpdatedEstimate={_reconstructed}");
    }

    private void AdaptStepSize(
        bool currentBit) {
        if (_previousBit == null) {
            _previousBit = currentBit;
            return;
        }

        if (_previousBit == currentBit) {
            _stepSize = _stepSize * _settings.StepIncreaseFactor;
        }
       
        else if (_previousBit != currentBit) {
            _stepSize = _settings.InitialStepSize;
        }

        _previousBit = currentBit;
    }

    protected override double CalculateCurrentRatio() {
        if (BitsWritten == 0)
            return 0;

        long originalBits =
            (long)_processedSamples *
            _context!.Settings.BitsPerSample;

        return (double)originalBits /
               BitsWritten;
    }

    protected override void Initialize(CompressionContext context) {
        _context = context;
        _settings = (AdaptiveDeltaModulationSettings)context.Settings;

        _encodedBits.Clear();
        CompressedData = [];
        _reconstructed = _context.Samples.First();
        _stepSize = _settings.InitialStepSize;
        _previousBit = null;
    }

    protected override void FinalizeEncoding() {
        byte[] payload = BitPacker.PackBits(_encodedBits);
        AdmHeader header =
            new() {
                SampleRate = _context.Settings.SampleRate,
                Channels = _context.Settings.Channels,
                BitsPerSample = _context.Settings.BitsPerSample,

                SampleCount =
                    _context!.Samples.Length,

                InitialStepSize = _settings.InitialStepSize,
                InitialPredictor = _context.Samples[0],
                StepIncreaseFactor = _settings.StepIncreaseFactor,
                
            };

        CompressedData = AdmFileWriter.Write(header, payload);

        // Console.WriteLine("=== Top 20 Most Common Differences ===");
        //
        // foreach (var kv in _diffHistogram
        //              .OrderByDescending(x => x.Value)
        //              .Take(20)) {
        //     Console.WriteLine(
        //         $"{kv.Key,6} - {kv.Key + 99,6} : {kv.Value}");
        // }
        //
        // int maxDiff = _diffHistogram.Keys.Max();
        //
        // Console.WriteLine($"Max Diff = {maxDiff}");
        //
        // long total =
        //     _diffHistogram.Sum(x => (long)x.Key * x.Value);
        //
        // long count =
        //     _diffHistogram.Sum(x => x.Value);
        //
        // Console.WriteLine(
        //     $"Average Diff ≈ {(double)total / count:F2}");
    }
}