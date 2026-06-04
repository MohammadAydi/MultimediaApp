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
///   - Mode selection       → IsCompressMode / IsDecompressMode
///   - File drop / browse   → OpenAudioCommand / OpenCompressedCommand / LoadAudioFile()
///   - Playback             → PlayCommand / PauseCommand / StopCommand
///   - Compression          → CompressCommand / CancelCompressionCommand
///   - Decompression        → DecompressCommand / CancelDecompressionCommand
///   - Reset                → ResetCommand
///   - Algorithm selection  → SelectedAlgorithm (drives AlgorithmSettingsViewModel)
///   - Progress feedback    → CompressionProgress / ProcessingSpeed / CompressionRatio
///   - Final results        → FinalCompressionRatio / FinalDuration / SnrDb
///   - File info            → SourceFileInfo / CompressedFileInfo
///   - Activity log         → Logs
/// </summary>
public partial class MainViewModel : ObservableObject {
    // ── Dependencies ─────────────────────────────────────────
    private readonly AudioFileService _audioFileService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private CancellationTokenSource? _cancellationTokenSource;

    public MainViewModel(
        AudioFileService audioFileService,
        AudioPlaybackService audioPlaybackService) {
        _audioFileService = audioFileService;
        _audioPlaybackService = audioPlaybackService;

        // Populate algorithm list — displayed in the algorithm selector ComboBox
        Algorithms = [
            new DpcmCompressionAlgorithm(),
            new AdaptiveDeltaModulationCompressionAlgorithm(),
            new DeltaModulationCompressionAlgorithm(),
            new NonlinearQuantizationCompressionAlgorithm(),
            new PredictiveDifferentialCodingCompressionAlgorithm(),
        ];
    }

    // =========================================================
    // MODE SWITCHING
    // The UI uses two mutually exclusive modes:
    //   Compress   — load a raw audio file → compress it
    //   Decompress — load a compressed file → decompress + play
    // =========================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDecompressMode))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(DecompressCommand))]
    private bool _isCompressMode = true;

    /// <summary>True when the user is in Decompress mode (inverse of IsCompressMode).</summary>
    public bool IsDecompressMode => !IsCompressMode;

    partial void OnIsCompressModeChanged(bool value) {
        // Clear state when switching modes so stale data never shows
        Reset();
    }

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
    /// the currently selected algorithm. The UI binds the dynamic
    /// settings panel to this property.
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
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private AudioFileModel? _currentAudioFile;

    // ── Formatted proxy properties bound by the View ──────────
    public string SourceFileName => _currentAudioFile?.FileName ?? "No file loaded";
    public string SourceSampleRate => _currentAudioFile is { } f ? $"{f.SampleRate} Hz" : "—";

    public string SourceChannels => _currentAudioFile is { } f
        ? f.Channels == 2 ? "Stereo" : $"{f.Channels}ch"
        : "—";

    public string SourceBitsPerSample => _currentAudioFile is { } f ? $"{f.BitsPerSample}-bit" : "—";

    public string SourceFileSize => _currentAudioFile is { } f
        ? FormatFileSize(_audioFileService.GetFileSize(f.FilePath))
        : "—";

    // =========================================================
    // COMPRESSED FILE INFO
    // =========================================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompressedFileName))]
    [NotifyPropertyChangedFor(nameof(CompressedFileSize))]
    [NotifyPropertyChangedFor(nameof(CompressedAlgorithmName))]
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
    // REAL-TIME PROGRESS
    // =========================================================

    /// <summary>0 – 100 progress value bound to the ProgressBar.</summary>
    [ObservableProperty] private double _operationProgress;

    /// <summary>Live processing throughput, e.g. "128 000 samples/s".</summary>
    [ObservableProperty] private string _processingSpeed = "—";

    /// <summary>Live compression ratio during compression, e.g. "2.34:1".</summary>
    [ObservableProperty] private string _liveCompressionRatio = "—";

    // =========================================================
    // FINAL RESULTS  (shown after operation completes)
    // =========================================================

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasResults))]
    private string _finalCompressionRatio = "—";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasResults))]
    private string _finalDuration = "—";

    [ObservableProperty] private string _snrDb = "—";

    [ObservableProperty] private string _spaceSaved = "—";

    /// <summary>True once a compression or decompression run has finished.</summary>
    public bool HasResults =>
        _finalCompressionRatio != "—" || _finalDuration != "—";

    // =========================================================
    // ACTIVITY LOG
    // =========================================================

    public ObservableCollection<string> Logs { get; } = [];

    private void AddLog(string message) =>
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

    // =========================================================
    // COMMANDS — MODE SWITCHING
    // =========================================================

    /// <summary>Switches the UI to Compress mode.</summary>
    [RelayCommand]
    private void SetCompressMode() => IsCompressMode = true;

    /// <summary>Switches the UI to Decompress mode.</summary>
    [RelayCommand]
    private void SetDecompressMode() => IsCompressMode = false;

    // =========================================================
    // COMMANDS — OPEN FILE
    // =========================================================

    /// <summary>Opens a native file dialog for raw audio files.</summary>
    [RelayCommand]
    private void OpenAudio() {
        var dialog = new OpenFileDialog {
            Title = "Open Audio File",
            Filter = "Audio Files|*.wav;*.mp3|WAV Files|*.wav|MP3 Files|*.mp3",
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            LoadAudioFile(dialog.FileName);
    }

    /// <summary>Opens a native file dialog for already-compressed files.</summary>
    [RelayCommand]
    private void OpenCompressed() {
        var dialog = new OpenFileDialog {
            Title = "Open Compressed Audio File",
            Filter = $"Compressed Audio|*.admAydi;*.nlqAlaa;*.dmJomaat;*.dpcmKasem;*.pdcManar|All Files|*.*",
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            LoadCompressedFile(dialog.FileName);
    }

    // ── Public entry points used by drag-and-drop in the code-behind ──

    /// <summary>
    /// Loads and validates a raw audio file from a file path.
    /// Called from drag-drop code-behind and OpenAudio command.
    /// PLACEHOLDER — add validation + metadata reading.
    /// </summary>
    public void LoadAudioFile(string filePath) {
        if (!_audioFileService.IsSupportedAudioFile(filePath)) {
            AddLog($"Unsupported file: {System.IO.Path.GetFileName(filePath)}");
            return;
        }

        using var reader = new WaveFileReader(filePath);
        CurrentAudioFile = new AudioFileModel {
            FilePath = filePath,
            FileName = System.IO.Path.GetFileName(filePath),
            SampleRate = reader.WaveFormat.SampleRate,
            Channels = reader.WaveFormat.Channels,
            BitsPerSample = reader.WaveFormat.BitsPerSample,
        };

        IsAudioLoaded = true;
        AddLog($"Loaded: {CurrentAudioFile.FileName}");
    }

    /// <summary>
    /// Loads a compressed audio file.
    /// PLACEHOLDER — add header/metadata parsing per format.
    /// </summary>
    public async Task LoadCompressedFile(string filePath) {
        string magic = await _audioFileService.GetMagicName(filePath);
        SelectedAlgorithm = CompressionAlgorithmFactory.Create(magic);
        CompressedFileInfo = new CompressedFileModel {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            AlgorithmName = SelectedAlgorithm.Name,
        };

        IsCompressedFileLoaded = true;
        AddLog($"Loaded compressed: {CompressedFileInfo.FileName}");
    }

    // =========================================================
    // COMMANDS — PLAYBACK  (source audio)
    // =========================================================

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() {
        // PLACEHOLDER — call _audioPlaybackService.Play(...)
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
        // PLACEHOLDER — call _audioPlaybackService.Pause(...)
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
        // PLACEHOLDER — play the reconstructed WAV
        IsPlayingDecompressed = true;
        AddLog("Playing decompressed audio");
    }

    private bool CanPlayDecompressed() => IsCompressedFileLoaded && !IsPlayingDecompressed;

    [RelayCommand(CanExecute = nameof(CanStopDecompressed))]
    private void StopDecompressed() {
        // PLACEHOLDER — stop decompressed playback
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
                context,
                progressReporter,
                _cancellationTokenSource.Token);
            string? fileName = SaveCompressedFile();
            await _audioFileService.SaveBytesToFileAsync(fileName, result.CompressedData);

            FinalCompressionRatio =
                $"{result.CompressionRatio:F2}:1";
            FinalDuration =
                $"{result.CompressionTime.TotalMilliseconds:F0} ms";
            long originalBytes =
                samples.Length * sizeof(short);
            long compressedBytes =
                result.CompressedData.Length;
            double saved =
                (1.0 - (double)compressedBytes / originalBytes) * 100.0;
            SpaceSaved = $"{saved:F1}%";
            OperationProgress = 100;

            AddLog("Compression complete (placeholder)");
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

    private String? SaveCompressedFile() {
        string defaultFileName =
            $"{Path.GetFileNameWithoutExtension(CurrentAudioFile.FilePath)}" +
            "_compressed";
        string filter =
            $"{SelectedAlgorithm.Name} Files (*.{SelectedAlgorithm.Extension})| *.{SelectedAlgorithm.Extension}";
        var dialog = new SaveFileDialog {
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    private String? SaveDecompressedFile() {
        string defaultFileName =
            $"{Path.GetFileNameWithoutExtension(CompressedFileInfo.FilePath)}" +
            "_Uncompressed";
        string filter =
            "Uncompressed Files (*.wav)| *.wav";
        var dialog = new SaveFileDialog {
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
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

        IsDecompressing = true;
        OperationProgress = 0;
        ProcessingSpeed = "—";
        FinalDuration = "—";
        _cancellationTokenSource = new CancellationTokenSource();

        AddLog("Decompression started");
        try {
            byte[] fileBytes = await _audioFileService.GetFileBytes(CompressedFileInfo.FilePath);
            DecompressionResult result = SelectedAlgorithm.Decompress(fileBytes);
            byte[] wav = WaveFileBuilder.CreateWaveFile(result);
            string? filePath = SaveDecompressedFile();
            await _audioFileService.SaveBytesToFileAsync(filePath, wav);
            AddLog("Decompression complete (placeholder)");
        }
        // try {
        //     var startTime = DateTime.Now;
        //     var random = new Random();
        //
        //     for (int i = 0; i <= 100; i++) {
        //         _cancellationTokenSource.Token.ThrowIfCancellationRequested();
        //
        //         OperationProgress = i;
        //
        //         ProcessingSpeed =
        //             $"{random.Next(80000, 400000):N0} samples/s";
        //
        //         await Task.Delay(30, _cancellationTokenSource.Token);
        //     }
        //
        //     var elapsed = DateTime.Now - startTime;
        //
        //     FinalDuration = $"{elapsed.TotalMilliseconds:F0} ms";
        //
        //     AddLog("Decompression complete");
        // }
        catch (OperationCanceledException) {
            AddLog("Decompression cancelled");
        }
        catch (Exception ex) {
            AddLog($"Error: {ex.Message}");
            MessageBox.Show($"Decompression failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine($"[ERROR] {ex}");
        }
        finally {
            IsDecompressing = false;
        }
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
        Logs.Clear();
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

    /// <summary>
    /// Factory that returns a typed settings ViewModel for the given algorithm.
    /// PLACEHOLDER — create one SettingsViewModel per algorithm type.
    /// </summary>
    private static AlgorithmSettingsViewModel? CreateSettingsViewModel(
        ICompressionAlgorithm algorithm) {
        // TODO: return a strongly-typed settings VM per algorithm, e.g.:
        //   DpcmCompressionAlgorithm    → DpcmSettingsViewModel
        //   AdaptiveDeltaModulation...  → AdaptiveDeltaModulationSettingsViewModel
        // For now return a generic placeholder so the UI has something to bind to.
        return new AlgorithmSettingsViewModel(algorithm.Name);
    }

    private CompressionSettings CreateSettingsForAlgorithm(ICompressionAlgorithm algorithm) {
        if (CurrentAudioFile == null)
            throw new InvalidOperationException(
                "No audio file loaded");
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
                    // InitialStepSize = 2
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
                    QuantizationBits = (int)QuantizationLevels
                },

            PredictiveDifferentialCodingCompressionAlgorithm =>
                new PredictiveDifferentialCodingSettings {
                    SampleRate = CurrentAudioFile.SampleRate,
                    Channels = CurrentAudioFile.Channels,
                    BitsPerSample = CurrentAudioFile.BitsPerSample,
                },

            _ => throw new NotSupportedException(
                $"Algorithm {algorithm.GetType().Name} is not supported")
        };
    }
}

// ─────────────────────────────────────────────────────────────
// PLACEHOLDER VIEW-MODELS for algorithm settings
// Replace with one strongly-typed VM per algorithm.
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Generic placeholder settings ViewModel shown in the algorithm settings panel.
/// Replace with one SettingsViewModel per algorithm.
/// </summary>
public partial class AlgorithmSettingsViewModel : ObservableObject {
    public AlgorithmSettingsViewModel(string algorithmName) {
        AlgorithmName = algorithmName;
    }

    public string AlgorithmName { get; }

    // PLACEHOLDER properties — replace with algorithm-specific settings.
    // Example for DPCM:
    //   [ObservableProperty] private int _quantizationStep = 8;
    //   [ObservableProperty] private int _sampleRate = 44100;

    [ObservableProperty] private double _setting1 = 8;
    [ObservableProperty] private double _setting2 = 100;
}

// ─────────────────────────────────────────────────────────────
// PLACEHOLDER DATA MODELS (add to Models/ folder in your project)
// ─────────────────────────────────────────────────────────────