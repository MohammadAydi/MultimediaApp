using System.Windows;
using AudioCompressionApp.ViewModels;

namespace AudioCompressionApp.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        DataContext = viewModel;
    }

    private void FileDropBorder_OnDragEnter(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
    }

    private void FileDropBorder_OnDrop(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files =
            (string[])e.Data.GetData(DataFormats.FileDrop);

        if (files.Length == 0)
            return;

        _viewModel.LoadAudioFile(files[0]);
    }
}