using System.IO;
using System.Windows;
using AudioCompressionApp.ViewModels;
using Microsoft.Win32;
using AudioCompressionApp.Services;
using NAudio.Wave;

namespace AudioCompressionApp.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    private readonly AudioFileService _audioFileService = new();
    private readonly MainViewModel _viewModel = new();

    public MainWindow() {
        InitializeComponent();

        DataContext = _viewModel;
    }

    private void FileDropBorder_OnDragEnter(object sender, DragEventArgs e) {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
    }

    private void FileDropBorder_OnDrop(object sender, DragEventArgs e) {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

        if (files.Length == 0)
            return;

        string filePath = files[0];

        LoadAudioFile(filePath);
    }

    private void OpenAudioButton_OnClick(object sender, RoutedEventArgs e) {
        OpenFileDialog dialog = new() {
            Title = "Open Audio File",
            Filter =
                "Audio Files|*.wav;*.mp3|" +
                "WAV Files|*.wav|" +
                "MP3 Files|*.mp3",
            Multiselect = false,
        };
        bool? result = dialog.ShowDialog();
        if (result != true)
            return;
        string filePath = dialog.FileName;
        LoadAudioFile(filePath);
    }

    private void LoadAudioFile(string filePath)
    {
        if (!_audioFileService.IsSupportedAudioFile(filePath))
        {
            MessageBox.Show("Unsupported audio format.");
            return;
        }

        using AudioFileReader reader = new(filePath);

        _viewModel.FileName = Path.GetFileName(filePath);

        _viewModel.SampleRate =
            $"{reader.WaveFormat.SampleRate} Hz";

        _viewModel.Channels =
            reader.WaveFormat.Channels.ToString();

        _viewModel.BitsPerSample =
            $"{reader.WaveFormat.BitsPerSample} Bit";
        _viewModel.IsAudioLoaded = true;
    }
}