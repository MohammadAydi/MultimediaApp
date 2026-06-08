
using AudioCompressionApp.Algorithms.Common;
using AudioCompressionApp.Models;
namespace AudioCompressionApp.Algorithms.DM;

public class DeltaModulationCompressionAlgorithm : CompressionAlgorithmBase {
    private List<bool> _encodedBits = [];
    private double _reconstructed;
    private double _stepSize;

    private DeltaModulationSettings? _settings;

    private CompressionContext? _context;


    public long BitsWritten { get; private set; }
    private int _processedSamples;


    public override string Name => "Delta Modulation";
    public override string Extension => "dmJomaat";

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


        // Current estimate before decision
        double estimate = _reconstructed;

        // Error
        double error = sample - estimate;

        // Quantizer decision
        bool bit = error >= 0;

        // Transmitted value (+Δ or -Δ)
        double transmitted = bit ? _stepSize : -_stepSize;

        // Update accumulator
        _reconstructed += transmitted;

        _encodedBits.Add(bit);
        BitsWritten++;

        // Console.WriteLine(
        //     $"[{index}] " +
        //     $"Input={sample}, " +
        //     $"Estimate={estimate}, " +
        //     $"Error={error}, " +
        //     $"Bit={(bit ? 1 : 0)}, " +
        //     $"Transmit={transmitted}, " +
        //     $"UpdatedEstimate={_reconstructed}");
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
        _settings = (DeltaModulationSettings)context.Settings;

        _encodedBits.Clear();
        CompressedData = [];
        _reconstructed = _context.Samples.First();
        _stepSize = _settings.InitialStepSize;
    }

    protected override void FinalizeEncoding() {
        byte[] payload = BitPacker.PackBits(_encodedBits);
        DmHeader header =
            new() {
                SampleRate = _context.Settings.SampleRate,
                Channels = _context.Settings.Channels,
                BitsPerSample = _context.Settings.BitsPerSample,
                SampleCount =
                    _context!.Samples.Length,

                InitialStepSize = _settings.InitialStepSize,
                InitialPredictor = _context.Samples[0],
                
            };

        CompressedData = DmFileWriter.Write(header, payload);
    }
}