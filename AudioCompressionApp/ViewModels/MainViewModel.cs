using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AudioCompressionApp.Algorithms;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Algorithms.Nonlinear;
using AudioCompressionApp.Models;
using AudioCompressionApp.Models.Settings;
using AudioCompressionApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;

namespace AudioCompressionApp.ViewModels;

/// <summary>
/// Main ViewModel for the Audio Compression application.
///
/// UI → ViewModel binding map:
///   - File explorer tree  → FileSystemTree (FileSystemTreeViewModel)
///   - Mode (read-only)    → IsCompressMode / IsDecompressMode
///                           Set automatically by file extension — never by the user.
///   - Playback            → PlayCommand / PauseCommand / StopCommand
///   - Compression         → CompressCommand / CancelCompressionCommand
///   - Decompression       → DecompressCommand / CancelDecompressionCommand
///   - Reset               → ResetCommand
///   - Algorithm selection → SelectedAlgorithm (drives AlgorithmSettingsViewModel)
///   - Progress feedback   → OperationProgress / ProcessingSpeed / LiveCompressionRatio
///   - Final results       → FinalCompressionRatio / FinalDuration / SnrDb / SpaceSaved
///   - File info           → SourceFile* / CompressedFile*
///   - Activity log        → Logs
/// </summary>
public partial class MainViewModel : ObservableObject {
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly AudioFileService _audioFileService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private CancellationTokenSource? _cancellationTokenSource;

    // ── Extensions that drive automatic mode detection ────────────────────────
    private static readonly string[] AudioExtensions =
        [".wav", ".mp3"];

    private static readonly string[] CompressedExtensions =
        [".admaydi", ".nlqalaa", ".dmjomaat", ".dpcmkasem", ".pdcmanar"];

    public MainViewModel(
        AudioFileService audioFileService,
        AudioPlaybackService audioPlaybackService) {
        _audioFileService = audioFileService;
        _audioPlaybackService = audioPlaybackService;

        Algorithms = [
            new DpcmCompressionAlgorithm(),
            new AdaptiveDeltaModulationCompressionAlgorithm(),
            new DeltaModulationCompressionAlgorithm(),
            new NonlinearQuantizationCompressionAlgorithm(),
            new PredictiveDifferentialCodingCompressionAlgorithm(),
        ];

        // Wire the tree's file-selection callback to our routing method.
        // The tree knows nothing about audio — it just reports a file path.
        FileSystemTree = new FileSystemTreeViewModel {
            FileSelected = RouteSelectedFile,
        };
    }

    // =========================================================
    // FILE SYSTEM TREE
    // =========================================================

    /// <summary>
    /// Drives the left-panel explorer TreeView.
    /// Bound directly to the View — the tree's open-folder button,
    /// root nodes, and selection all live here.
    /// </summary>
    public FileSystemTreeViewModel FileSystemTree { get; }

    // =========================================================
    // MODE  (read-only — set by extension, never by user)
    // =========================================================

    /// <summary>
    /// True when a raw audio file (.wav / .mp3) is selected.
    /// Set automatically when the user picks a file in the tree.
    /// The UI displays this as a read-only badge — no click handler.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDecompressMode))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecompressCommand))]
    private bool _isCompressMode = true;

    /// <summary>True when the selected file is a compressed audio file.</summary>
    public bool IsDecompressMode => !IsCompressMode;

    // =========================================================
    // ALGORITHM SELECTION
    // =========================================================

    public ObservableCollection<ICompressionAlgorithm> Algorithms { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyPropertyChangedFor(nameof(AlgorithmSettingsViewModel))]
    private ICompressionAlgorithm? _selectedAlgorithm;

    [ObservableProperty] private double quantizationLevels = 16;

    /// <summary>
    /// Returns a ViewModel that exposes the editable settings for
    /// the currently selected algorithm.
    /// PLACEHOLDER — implement by returning a typed settings VM.
    /// </summary>
    public AlgorithmSettingsViewModel? AlgorithmSettingsViewModel =>
        SelectedAlgorithm is null ? null : CreateSettingsViewModel(SelectedAlgorithm);

    // =========================================================
    // SOURCE AUDIO FILE  (raw .wav / .mp3)
    // =========================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceFileName))]
    [NotifyPropertyChangedFor(nameof(SourceSampleRate))]
    [NotifyPropertyChangedFor(nameof(SourceChannels))]
    [NotifyPropertyChangedFor(nameof(SourceBitsPerSample))]
    [NotifyPropertyChangedFor(nameof(SourceFileSize))]
    [NotifyPropertyChangedFor(nameof(SourcePlayDuration))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private AudioFileModel? _currentAudioFile;

    public string SourceFileName => _currentAudioFile?.FileName ?? "No file selected";
    public string SourceSampleRate => _currentAudioFile is { } f ? $"{f.SampleRate} Hz" : "—";

    public string SourceChannels => _currentAudioFile is { } f
        ? f.Channels == 2 ? "Stereo" : $"{f.Channels}ch"
        : "—";

    public string SourceBitsPerSample => _currentAudioFile is { } f ? $"{f.BitsPerSample}-bit" : "—";

    public string SourceFileSize => _currentAudioFile is { } f
        ? FormatFileSize(_audioFileService.GetFileSize(f.FilePath))
        : "—";

    /// <summary>
    /// Total playback duration of the loaded audio file.
    /// Computed once on load from sample count ÷ (sample rate × channels).
    /// </summary>
    public string SourcePlayDuration => _currentAudioFile is { } f
        ? FormatDuration(f.TotalSamples, f.SampleRate, f.Channels)
        : "—";

    private static string FormatDuration(long totalSamples, int sampleRate, int channels) {
        if (sampleRate <= 0 || channels <= 0) return "—";
        double seconds = (double)totalSamples / (sampleRate * channels);
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    // =========================================================
    // COMPRESSED FILE INFO
    // =========================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompressedFileName))]
    [NotifyPropertyChangedFor(nameof(CompressedFileSize))]
    [NotifyPropertyChangedFor(nameof(CompressedAlgorithmName))]
    [NotifyPropertyChangedFor(nameof(CompressedSampleRate))]
    [NotifyPropertyChangedFor(nameof(CompressedChannels))]
    [NotifyPropertyChangedFor(nameof(CompressedBitsPerSample))]
    [NotifyCanExecuteChangedFor(nameof(DecompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayDecompressedCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopDecompressedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private CompressedFileModel? _compressedFileInfo;

    public string CompressedFileName => _compressedFileInfo?.FileName ?? "—";

    public string CompressedFileSize => _compressedFileInfo is { } c
        ? FormatFileSize(_audioFileService.GetFileSize(c.FilePath))
        : "—";

    public string CompressedAlgorithmName => _compressedFileInfo?.AlgorithmName ?? "—";

    /// <summary>
    /// Sample rate read from the compressed file header.
    /// Populate CompressedFileModel.SampleRate in LoadCompressedFile().
    /// </summary>
    public string CompressedSampleRate =>
        _compressedFileInfo is { SampleRate: > 0 } c ? $"{c.SampleRate} Hz" : "—";

    /// <summary>
    /// Channel count read from the compressed file header.
    /// </summary>
    public string CompressedChannels =>
        _compressedFileInfo is { Channels: > 0 } c
            ? c.Channels == 2 ? "Stereo" : $"{c.Channels}ch"
            : "—";

    /// <summary>
    /// Bit depth read from the compressed file header.
    /// </summary>
    public string CompressedBitsPerSample =>
        _compressedFileInfo is { BitsPerSample: > 0 } c ? $"{c.BitsPerSample}-bit" : "—";

    // =========================================================
    // STATE FLAGS — drive CanExecute + UI visibility
    // =========================================================

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _isAudioLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayDecompressedCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopDecompressedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _isCompressedFileLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCompressionCommand))]
    private bool _isCompressing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DecompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDecompressionCommand))]
    private bool _isDecompressing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isPlayingSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayDecompressedCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopDecompressedCommand))]
    private bool _isPlayingDecompressed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    private bool _isSourcePaused;

    // =========================================================
    // REAL-TIME COMPRESSION PROGRESS
    // =========================================================

    [ObservableProperty] private double _operationProgress;
    [ObservableProperty] private string _processingSpeed = "—";
    [ObservableProperty] private string _liveCompressionRatio = "—";


    // =========================================================
    // REAL-TIME DECOMPRESSION PROGRESS
    // =========================================================
    [ObservableProperty] private double _decompressionProgress;
    [ObservableProperty] private string _decompressionSpeed = "—";
    [ObservableProperty] private string _finalDecompressionTime = "—";


    // =========================================================
    // FINAL RESULTS
    // =========================================================

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasResults))]
    private string _finalCompressionRatio = "—";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasResults))]
    private string _finalDuration = "—";

    [ObservableProperty] private string _snrDb = "—";
    [ObservableProperty] private string _spaceSaved = "—";

    public bool HasResults =>
        _finalCompressionRatio != "—" || _finalDuration != "—";

    // =========================================================
    // ACTIVITY LOG
    // =========================================================

    public ObservableCollection<string> Logs { get; } = [];

    private void AddLog(string message) =>
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

    // =========================================================
    // FILE ROUTING  (replaces the old manual Open* commands)
    //
    // This is the single entry point for all file selection:
    // tree clicks, drag-drop onto the tree, and drop onto the main area.
    // The extension determines the mode — the user has no control over it.
    // =========================================================

    /// <summary>
    /// Routes a file path to the correct loader based on its extension.
    /// Called from FileSystemTreeViewModel.FileSelected and from the
    /// drag-drop handler in MainWindow.xaml.cs.
    /// </summary>
    public void RouteSelectedFile(string filePath) {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (AudioExtensions.AsSpan().Contains(ext)) {
            IsCompressMode = true;
            ResetWorkArea();
            LoadAudioFile(filePath);
        }
        else if (CompressedExtensions.AsSpan().Contains(ext)) {
            IsCompressMode = false;
            ResetWorkArea();
            LoadCompressedFile(filePath);
        }
        else {
            AddLog($"Unsupported file type: {Path.GetFileName(filePath)}");
        }
    }

    /// <summary>
    /// Loads and validates a raw audio file from a file path.
    /// </summary>
    public void LoadAudioFile(string filePath) {
        if (!_audioFileService.IsSupportedAudioFile(filePath)) {
            AddLog($"Unsupported audio file: {Path.GetFileName(filePath)}");
            return;
        }

        using var reader = new WaveFileReader(filePath);
        CurrentAudioFile = new AudioFileModel {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            SampleRate = reader.WaveFormat.SampleRate,
            Channels = reader.WaveFormat.Channels,
            BitsPerSample = reader.WaveFormat.BitsPerSample,
            // TotalSamples drives the play duration display
            TotalSamples = reader.SampleCount,
        };

        IsAudioLoaded = true;
        AddLog($"Loaded: {CurrentAudioFile.FileName}");
    }

    /// <summary>
    /// Loads a compressed audio file.
    /// </summary>
    public async Task LoadCompressedFile(string filePath) {
        string magic = await _audioFileService.GetMagicName(filePath);
        IDecodingAlgo decodingAlgo = DecodingAlgorithmFactory.Create(magic);
        CompressedFileInfo = new CompressedFileModel {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            AlgorithmName = decodingAlgo.Name,
            // TODO: read SampleRate, Channels, BitsPerSample from the file header
            // e.g. SampleRate    = header.SampleRate,
            //      Channels      = header.Channels,
            //      BitsPerSample = header.BitsPerSample,
        };

        IsCompressedFileLoaded = true;
        AddLog($"Loaded compressed: {CompressedFileInfo.FileName}");
    }

    // =========================================================
    // COMMANDS — PLAYBACK  (source audio)
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() {
        if (CurrentAudioFile == null || string.IsNullOrWhiteSpace(CurrentAudioFile.FilePath))
            return;

        _audioPlaybackService.Play(CurrentAudioFile.FilePath);
        IsPlayingSource = true;
        IsSourcePaused = false;
        AddLog("Playback started");
    }

    private bool CanPlay() => IsAudioLoaded && !IsPlayingSource;

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause() {
        IsPlayingSource = false;
        IsSourcePaused = true;
        AddLog("Playback paused");
    }

    private bool CanPause() => IsPlayingSource && !IsSourcePaused;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() {
        _audioPlaybackService.Stop();
        IsPlayingSource = false;
        IsSourcePaused = false;
        AddLog("Playback stopped");
    }

    private bool CanStop() => IsPlayingSource || IsSourcePaused;

    // =========================================================
    // COMMANDS — PLAYBACK  (decompressed audio)
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanPlayDecompressed))]
    private void PlayDecompressed() {
        IsPlayingDecompressed = true;
        AddLog("Playing decompressed audio");
    }

    private bool CanPlayDecompressed() => IsCompressedFileLoaded && !IsPlayingDecompressed;

    [RelayCommand(CanExecute = nameof(CanStopDecompressed))]
    private void StopDecompressed() {
        IsPlayingDecompressed = false;
        AddLog("Stopped decompressed playback");
    }

    private bool CanStopDecompressed() => IsPlayingDecompressed;

    // =========================================================
    // COMMANDS — COMPRESS
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAsync() {
        if (CurrentAudioFile is null || SelectedAlgorithm is null) return;

        IsCompressing = true;
        OperationProgress = 0;
        LiveCompressionRatio = "—";
        ProcessingSpeed = "—";
        FinalCompressionRatio = "—";
        FinalDuration = "—";
        _cancellationTokenSource = new CancellationTokenSource();

        AddLog($"Compression started — {SelectedAlgorithm.Name}");
        IProgress<CompressionProgressModel> progressReporter =
            new Progress<CompressionProgressModel>(progress => {
                OperationProgress = progress.Progress;
                LiveCompressionRatio = $"{progress.CompressionRatio:F2}:1";
                ProcessingSpeed = $"{progress.ProcessingSpeed:F0} samples/s";
            });

        try {
            short[] samples = await _audioFileService.GetAudioSamples(CurrentAudioFile.FilePath);
            var context = new CompressionContext {
                Samples = samples,
                Settings = CreateSettingsForAlgorithm(SelectedAlgorithm),
            };
            CompressionResult result = await SelectedAlgorithm.CompressAsync(
                context, progressReporter, _cancellationTokenSource.Token);

            string? fileName = SaveCompressedFile();
            await _audioFileService.SaveBytesToFileAsync(fileName, result.CompressedData);

            FinalCompressionRatio = $"{result.CompressionRatio:F2}:1";
            FinalDuration = $"{result.CompressionTime.TotalMilliseconds:F0} ms";

            long originalBytes = samples.Length * sizeof(short);
            long compressedBytes = result.CompressedData.Length;
            double saved = (1.0 - (double)compressedBytes / originalBytes) * 100.0;
            SpaceSaved = $"{saved:F1}%";
            OperationProgress = 100;

            AddLog($"Compression complete — {FinalCompressionRatio} in {FinalDuration}");
        }
        catch (OperationCanceledException) {
            AddLog("Compression canceled");
            MessageBox.Show("Compression canceled.");
        }
        catch (Exception ex) {
            AddLog($"Error: {ex.Message}");
            Console.WriteLine($"[ERROR] {ex}");
            MessageBox.Show($"Error: {ex.Message}");
        }
        finally {
            IsCompressing = false;
        }
    }

    private string? SaveCompressedFile() {
        string defaultFileName =
            $"{Path.GetFileNameWithoutExtension(CurrentAudioFile!.FilePath)}_compressed";
        string filter =
            $"{SelectedAlgorithm!.Name} Files (*.{SelectedAlgorithm.Extension})|*.{SelectedAlgorithm.Extension}";

        var dialog = new SaveFileDialog {
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private bool CanCompress() =>
        IsAudioLoaded && !IsCompressing && SelectedAlgorithm is not null;

    [RelayCommand(CanExecute = nameof(CanCancelCompression))]
    private void CancelCompression() {
        _cancellationTokenSource?.Cancel();
        AddLog("Cancelling compression…");
    }

    private bool CanCancelCompression() => IsCompressing;

    // =========================================================
    // COMMANDS — DECOMPRESS
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanDecompress))]
    private async Task DecompressAsync() {
        if (CompressedFileInfo is null) return;

        IsDecompressing        = true;
        DecompressionProgress  = 0;
        DecompressionSpeed     = "—";
        FinalDecompressionTime = "—";

        _cancellationTokenSource = new CancellationTokenSource();

        var progressReporter = new Progress<CompressionProgressModel>(model =>
        {
            DecompressionProgress = model.Progress;
            DecompressionSpeed    = $"{model.ProcessingSpeed:F0} samples/s";
        });

        AddLog("Decompression started");
        try
        {
            byte[] fileBytes = await _audioFileService.GetFileBytes(CompressedFileInfo.FilePath);
            string magic = await _audioFileService.GetMagicName(CompressedFileInfo.FilePath);
            IDecodingAlgo decodingAlgo = DecodingAlgorithmFactory.Create(magic);
            DecompressionResult result = await decodingAlgo.DecompressAsync(
                fileBytes,
                progressReporter,
                _cancellationTokenSource.Token);

            FinalDecompressionTime = $"{result.DecompressionTime.TotalMilliseconds:F0} ms";
            DecompressionProgress  = 100;

            byte[]  wav      = WaveFileBuilder.CreateWaveFile(result);
            string? filePath = SaveDecompressedFile();
            await _audioFileService.SaveBytesToFileAsync(filePath, wav);

            AddLog($"Decompression complete — {FinalDecompressionTime}");
        }
        catch (OperationCanceledException)
        {
            DecompressionProgress  = 0;
            DecompressionSpeed     = "—";
            FinalDecompressionTime = "—";
            AddLog("Decompression cancelled");
        }
        catch (Exception ex)
        {
            AddLog($"Error: {ex.Message}");
            MessageBox.Show($"Decompression failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine($"[ERROR] {ex}");
        }
        finally
        {
            IsDecompressing = false;
        }
    }

    private string? SaveDecompressedFile() {
        string defaultFileName =
            $"{Path.GetFileNameWithoutExtension(CompressedFileInfo!.FilePath)}_Uncompressed";

        var dialog = new SaveFileDialog {
            FileName = defaultFileName,
            Filter = "Uncompressed Files (*.wav)|*.wav",
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private bool CanDecompress() =>
        IsCompressedFileLoaded && !IsDecompressing;

    [RelayCommand(CanExecute = nameof(CanCancelDecompression))]
    private void CancelDecompression() {
        _cancellationTokenSource?.Cancel();
        AddLog("Cancelling decompression…");
    }

    private bool CanCancelDecompression() => IsDecompressing;

    // =========================================================
    // COMMANDS — RESET
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset() {
        _audioPlaybackService.Stop();
        ResetWorkArea();
        Logs.Clear();
    }

    private void ResetWorkArea() {
        CurrentAudioFile = null;
        CompressedFileInfo = null;
        IsAudioLoaded = false;
        IsCompressedFileLoaded = false;
        IsPlayingSource = false;
        IsSourcePaused = false;
        IsPlayingDecompressed = false;
        OperationProgress = 0;
        LiveCompressionRatio = "—";
        ProcessingSpeed = "—";
        FinalCompressionRatio = "—";
        FinalDuration = "—";
        SnrDb = "—";
        SpaceSaved = "—";
    }

    private bool CanReset() =>
        IsAudioLoaded || IsCompressedFileLoaded;

    // =========================================================
    // PRIVATE HELPERS
    // =========================================================

    private static string FormatFileSize(long bytes) => bytes switch {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F2} MB",
    };

    private static AlgorithmSettingsViewModel? CreateSettingsViewModel(
        ICompressionAlgorithm algorithm) {
        // TODO: return a strongly-typed settings VM per algorithm
        return new AlgorithmSettingsViewModel(algorithm.Name);
    }

    private CompressionSettings CreateSettingsForAlgorithm(ICompressionAlgorithm algorithm) {
        if (CurrentAudioFile == null)
            throw new InvalidOperationException("No audio file loaded");

        return algorithm switch {
            DpcmCompressionAlgorithm => new DpcmSettings {
                SampleRate = CurrentAudioFile.SampleRate,
                Channels = CurrentAudioFile.Channels,
                BitsPerSample = CurrentAudioFile.BitsPerSample,
                QuantizationStep = 8,
            },
            AdaptiveDeltaModulationCompressionAlgorithm =>
                new AdaptiveDeltaModulationSettings {
                    SampleRate = CurrentAudioFile.SampleRate,
                    Channels = CurrentAudioFile.Channels,
                    BitsPerSample = CurrentAudioFile.BitsPerSample,
                },
            DeltaModulationCompressionAlgorithm =>
                new DeltaModulationSettings {
                    SampleRate = CurrentAudioFile.SampleRate,
                    Channels = CurrentAudioFile.Channels,
                    BitsPerSample = CurrentAudioFile.BitsPerSample,
                },
            NonlinearQuantizationCompressionAlgorithm =>
                new NonlinearDifferentialCodingSettings {
                    SampleRate = CurrentAudioFile.SampleRate,
                    Channels = CurrentAudioFile.Channels,
                    BitsPerSample = CurrentAudioFile.BitsPerSample,
                    QuantizationBits = (int)QuantizationLevels,
                },
            PredictiveDifferentialCodingCompressionAlgorithm =>
                new PredictiveDifferentialCodingSettings {
                    SampleRate = CurrentAudioFile.SampleRate,
                    Channels = CurrentAudioFile.Channels,
                    BitsPerSample = CurrentAudioFile.BitsPerSample,
                },
            _ => throw new NotSupportedException(
                $"Algorithm {algorithm.GetType().Name} is not supported"),
        };
    }
}

// ─────────────────────────────────────────────────────────────
// PLACEHOLDER VIEW-MODELS for algorithm settings
// ─────────────────────────────────────────────────────────────

public partial class AlgorithmSettingsViewModel : ObservableObject {
    public AlgorithmSettingsViewModel(string algorithmName) {
        AlgorithmName = algorithmName;
    }

    public string AlgorithmName { get; }

    [ObservableProperty] private double _setting1 = 8;
    [ObservableProperty] private double _setting2 = 100;
}