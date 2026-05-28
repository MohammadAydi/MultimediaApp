using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows;
using AudioCompressionApp.Algorithms;
using AudioCompressionApp.Algorithms.Base;
using AudioCompressionApp.Models;
using AudioCompressionApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;

namespace AudioCompressionApp.ViewModels;

public partial class MainViewModel : ObservableObject {
    private readonly AudioFileService _audioFileService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private CancellationTokenSource? _cancellationTokenSource;

    public MainViewModel(
        AudioFileService audioFileService,
        AudioPlaybackService audioPlaybackService) {
        _audioFileService = audioFileService;
        _audioPlaybackService = audioPlaybackService;
        Algorithms = [
            new DPCMCompressionAlgorithm(),
            new AdaptiveDeltaModulationCompressionAlgorithm(),
            new DeltaModulationCompressionAlgorithm(),
            new NonlinearQuantizationCompressionAlgorithm(),
            new PredictiveDifferentialCodingCompressionAlgorithm(),
        ];
    }


    public ObservableCollection<ICompressionAlgorithm>
        Algorithms { get; }

    [ObservableProperty] private ICompressionAlgorithm? selectedAlgorithm;

    [ObservableProperty] private double selectedSampleRate = 44100;

    [ObservableProperty] private double quantizationLevels = 16;


    public ObservableCollection<string> Logs { get; } = [];

    private void AddLog(string message) {
        Logs.Insert(
            0,
            $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    // ==========================================
    // The Underlying Model
    // ==========================================

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(SampleRate))]
    [NotifyPropertyChangedFor(nameof(Channels))]
    [NotifyPropertyChangedFor(nameof(BitsPerSample))]
    private AudioFileModel? _currentAudioFile;

    // ==========================================
    // Formatted Proxy Properties for the View
    // ==========================================

    public string FileName => CurrentAudioFile?.FileName ?? "No File Loaded";

    // Example of formatting: adding "Hz"
    public string SampleRate => CurrentAudioFile != null ? $"{CurrentAudioFile.SampleRate} Hz" : "-";

    // Example of formatting: checking channel count
    public string Channels => CurrentAudioFile != null
        ? (CurrentAudioFile.Channels == 2 ? "2 (Stereo)" : $"{CurrentAudioFile.Channels} (Mono)")
        : "-";

    // Example of formatting: adding "-bit"
    public string BitsPerSample => CurrentAudioFile != null ? $"{CurrentAudioFile.BitsPerSample}-bit" : "-";

    // ==========================================
    // State Properties
    // ==========================================

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _isAudioLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCompressionCommand))]
    private bool _isCompressing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _isPlaying;

    [ObservableProperty] private double _compressionProgress;
    [ObservableProperty] private string _processingSpeed = "-";
    [ObservableProperty] private string _compressionRatio = "-";

    // ==========================================
    // Commands
    // ==========================================

    [RelayCommand]
    private void OpenAudio() {
        OpenFileDialog dialog = new() {
            Title = "Open Audio File",
            Filter = "Audio Files|*.wav;*.mp3|WAV Files|*.wav|MP3 Files|*.mp3",
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true) {
            LoadAudioFile(dialog.FileName);
        }
    }

    public void LoadAudioFile(string filePath) {
        if (!_audioFileService.IsSupportedAudioFile(filePath))
            return;

        using AudioFileReader reader = new(filePath);

        // Assigning to this property automatically notifies the UI 
        // to update FileName, SampleRate, Channels, and BitsPerSample!
        CurrentAudioFile = new AudioFileModel {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            SampleRate = reader.WaveFormat.SampleRate,
            Channels = reader.WaveFormat.Channels,
            BitsPerSample = reader.WaveFormat.BitsPerSample
        };
        AddLog($"Loaded file: {CurrentAudioFile.FileName}");
        IsAudioLoaded = true;
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() {
        if (CurrentAudioFile == null || string.IsNullOrWhiteSpace(CurrentAudioFile.FilePath))
            return;

        _audioPlaybackService.Play(CurrentAudioFile.FilePath);
        AddLog("Playback started");
        IsPlaying = true;
    }

    private bool CanPlay() => IsAudioLoaded && !IsPlaying;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() {
        _audioPlaybackService.Stop();
        AddLog("Playback stopped");
        IsPlaying = false;
    }

    private bool CanStop() => IsPlaying;

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAsync() {
        IsCompressing = true;
        _cancellationTokenSource = new CancellationTokenSource();

        CompressionProgress = 0;
        CompressionRatio = "-";
        ProcessingSpeed = "-";
        AddLog("Compression started");

        IProgress<CompressionProgressModel> progressReporter = new Progress<CompressionProgressModel>(progress => {
            CompressionProgress = progress.Progress;
            CompressionRatio = $"{progress.CompressionRatio:F2}%";
            ProcessingSpeed = $"{progress.ProcessingSpeed:F2} MB/s";
        });

        try {
            await Task.Run(async () => {
                Random random = new Random();
                for (int i = 0; i < 100; i++) {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    progressReporter.Report(new CompressionProgressModel {
                        Progress = i,
                        CompressionRatio = 40 + random.NextDouble() * 40,
                        ProcessingSpeed = 1 + random.NextDouble() * 8
                    });

                    await Task.Delay(60);
                }
            }, _cancellationTokenSource.Token);
            AddLog("Compression completed");
        }
        catch (OperationCanceledException) {
            AddLog("Compression canceled");
            MessageBox.Show("Compression canceled.");
        }
        finally {
            IsCompressing = false;
        }
    }

    private bool CanCompress() => IsAudioLoaded && !IsCompressing;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelCompression() {
        _cancellationTokenSource?.Cancel();
    }

    private bool CanCancel() => IsCompressing;

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset() {
        _audioPlaybackService.Stop();

        // Setting the model to null clears the proxy properties automatically
        CurrentAudioFile = null;

        IsAudioLoaded = false;
        IsPlaying = false;
        CompressionProgress = 0;
        CompressionRatio = "-";
        ProcessingSpeed = "-";
    }

    private bool CanReset() => IsAudioLoaded;
}