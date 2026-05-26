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
    private bool isCompressing;

    [ObservableProperty] private double compressionProgress;

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

        CompressionProgress = 0;

        await Task.Run(async () => {
            for (int i = 0; i <= 100; i++) {
                CompressionProgress = i;

                await Task.Delay(50);
            }
        });

        IsCompressing = false;
    }

    private bool CanCompress() {
        return IsAudioLoaded && !IsCompressing;
    }
}