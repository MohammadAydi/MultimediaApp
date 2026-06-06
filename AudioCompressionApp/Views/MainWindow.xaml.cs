using System.IO;
using System.Windows;
using System.Windows.Controls;
using AudioCompressionApp.Models;
using AudioCompressionApp.ViewModels;

namespace AudioCompressionApp.Views;

/// <summary>
/// Minimal code-behind.
///
/// The two things that can't be done in pure XAML and live here:
///   1. TreeView.SelectedItemChanged — WPF's TreeView selection event
///      must be wired in code because it exposes the old/new values via
///      RoutedPropertyChangedEventArgs, not a simple Command parameter.
///   2. Drag-and-drop onto the tree panel — routing a dropped file/folder
///      requires inspecting the dropped data and deciding whether it's a
///      folder (open in tree) or a file (route to ViewModel).
///
/// All actual logic is delegated to FileSystemTreeViewModel or MainViewModel.
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

    // ── TreeView: selection ───────────────────────────────────────────────────

    /// <summary>
    /// Fires when the user clicks a node in the tree.
    /// Delegates to FileSystemTreeViewModel.OnNodeSelected(), which in turn
    /// calls MainViewModel.RouteSelectedFile() for supported file types.
    /// </summary>
    private void FileTree_OnSelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileSystemNode node)
            _viewModel.FileSystemTree.OnNodeSelected(node);
    }

    // ── TreeView: drag-and-drop ───────────────────────────────────────────────

    /// <summary>
    /// Allow drops of folders or audio/compressed files onto the tree panel.
    /// </summary>
    private void FileTree_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    /// <summary>
    /// If the user drops a folder → open it in the tree.
    /// If the user drops a file  → route it directly (same as a tree click).
    /// Multiple items: uses the first one only.
    /// </summary>
    private void FileTree_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] items || items.Length == 0)
            return;

        string path = items[0];

        if (Directory.Exists(path))
        {
            // Dropped a folder → load it as the new tree root
            _viewModel.FileSystemTree.LoadFolder(path);
        }
        else if (File.Exists(path))
        {
            // Dropped a file → route it by extension (same path as a tree click)
            _viewModel.RouteSelectedFile(path);
        }

        e.Handled = true;
    }
}