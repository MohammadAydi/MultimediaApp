using System.IO;
using System.Windows;
using System.Windows.Controls;
using AudioCompressionApp.Models;
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

    // ── TreeView: selection ───────────────────────────────────────────────────

    private void FileTree_OnSelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileSystemNode node)
            _viewModel.FileSystemTree.OnNodeSelected(node);
    }

    // ── TreeView: drag-and-drop ───────────────────────────────────────────────
    private void FileTree_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }
    
    private void FileTree_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] items || items.Length == 0)
            return;

        string path = items[0];

        if (Directory.Exists(path))
        {
            _viewModel.FileSystemTree.LoadFolder(path);
        }
        else if (File.Exists(path))
        {
            _viewModel.RouteSelectedFile(path);
        }

        e.Handled = true;
    }
}