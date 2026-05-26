using System.Windows.Forms;
using AudioCompressionApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioCompressionApp.ViewModels;

public partial class MainViewModel : ObservableObject {
    [ObservableProperty] private string fileName = "No File Loaded";

    [ObservableProperty] private string sampleRate = "-";

    [ObservableProperty] private string channels = "-";

    [ObservableProperty] private string bitsPerSample = "-";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    private bool isAudioLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompressCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCompressionCommand))]
    private bool isCompressing;

    [ObservableProperty] private double compressionProgress;

    [ObservableProperty] private string processingSpeed = "-";

    [ObservableProperty] private string compressionRatio = "-";

    private CancellationTokenSource? _cancellationTokenSource;

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset() {
        FileName = "No File Loaded";

        SampleRate = "-";

        Channels = "-";

        BitsPerSample = "-";

        IsAudioLoaded = false;
    }

    private bool CanReset() {
        return IsAudioLoaded;
    }

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAsync() {
        IsCompressing = true;

        _cancellationTokenSource = new CancellationTokenSource();

        CompressionProgress = 0;

        CompressionProgress = 0;

        CompressionRatio = "-";

        ProcessingSpeed = "-";

        IProgress<CompressionProgressModel> progressReporter = new Progress<CompressionProgressModel>(progress => {
                CompressionProgress = progress.Progress;
                CompressionRatio =
                    $"{progress.CompressionRatio:F2}%";
                ProcessingSpeed =
                    $"{progress.ProcessingSpeed:F2} MB/s";
            }
        );

        try {
            await Task.Run(async () => {
                Random random = new Random();
                for (int i = 0; i < 100; i++) {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    progressReporter.Report(
                        new CompressionProgressModel {
                            Progress = i,

                            CompressionRatio =
                                40 + random.NextDouble() * 40,

                            ProcessingSpeed =
                                1 + random.NextDouble() * 8
                        });
                    await Task.Delay(60);
                }
            }, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) {
            MessageBox.Show("Compression canceled.");
        }
        finally {
            IsCompressing = false;
        }
    }

    private bool CanCompress() {
        return IsAudioLoaded && !IsCompressing;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelCompression() {
        _cancellationTokenSource?.Cancel();
    }

    private bool CanCancel() {
        return IsCompressing;
    }
}