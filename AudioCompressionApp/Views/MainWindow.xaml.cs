using System.Windows;
using System.Windows.Input;
using AudioCompressionApp.ViewModels;

namespace AudioCompressionApp.Views;

/// <summary>
/// Minimal code-behind: only handles drag-and-drop surface events
/// because drag-drop cannot be fully expressed in XAML alone.
/// All logic is delegated to the ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    // ── Drag-and-Drop Handlers ─────────────────────────────

    private void FileDropBorder_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void FileDropBorder_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string file = files[0];

        // Route to the right loader based on the current mode
        if (_viewModel.IsCompressMode)
            _viewModel.LoadAudioFile(file);
        else
            _viewModel.LoadCompressedFile(file);

        e.Handled = true;
    }
}