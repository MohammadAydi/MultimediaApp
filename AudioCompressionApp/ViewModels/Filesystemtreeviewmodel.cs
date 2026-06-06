using System;
using System.Collections.ObjectModel;
using System.IO;
using AudioCompressionApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioCompressionApp.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// FILE SYSTEM TREE VIEW MODEL
//
// Owns the root nodes displayed in the TreeView and handles:
//   • Opening a root folder (via dialog or drag-dropping a folder onto the tree)
//   • Notifying MainViewModel when the user clicks a supported file
//
// Separation of concerns:
//   • This VM knows about the file system and tree state.
//   • MainViewModel knows about audio loading and mode switching.
//   • The two communicate through the FileSelected callback (no tight coupling).
// ─────────────────────────────────────────────────────────────────────────────

public partial class FileSystemTreeViewModel : ObservableObject
{
    // ── Callback injected by MainViewModel ───────────────────────────────────

    /// <summary>
    /// Called when the user clicks a supported file in the tree.
    /// MainViewModel sets this to its own routing method.
    /// </summary>
    public Action<string>? FileSelected { get; set; }

    // ── Tree state ────────────────────────────────────────────────────────────

    /// <summary>Root-level nodes shown in the TreeView.</summary>
    public ObservableCollection<FileSystemNode> RootNodes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRootFolder))]
    [NotifyPropertyChangedFor(nameof(RootFolderName))]
    private string _rootFolderPath = string.Empty;

    /// <summary>True once a root folder has been opened.</summary>
    public bool HasRootFolder => !string.IsNullOrEmpty(RootFolderPath);

    /// <summary>Display name of the root folder (just the last segment).</summary>
    public string RootFolderName => HasRootFolder
        ? Path.GetFileName(RootFolderPath.TrimEnd(Path.DirectorySeparatorChar,
                                                    Path.AltDirectorySeparatorChar))
        : "No folder opened";

    // ── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a folder-browser dialog and loads that folder as the tree root.
    /// </summary>
    [RelayCommand]
    private void OpenFolder()
    {
        // OpenFolderDialog was added in .NET 8 / WPF on .NET 8.
        // If you're on an older stack, swap this for a WinForms FolderBrowserDialog
        // wrapped via System.Windows.Forms.FolderBrowserDialog.
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to browse",
        };

        if (dialog.ShowDialog() == true)
            LoadFolder(dialog.FolderName);
    }

    /// <summary>
    /// Loads the given folder path as the new tree root.
    /// Also called from code-behind when the user drags a folder onto the tree.
    /// </summary>
    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        RootFolderPath = folderPath;
        RootNodes.Clear();

        // Start with the root folder itself expanded one level
        var root = new FileSystemNode(folderPath, isDirectory: true);
        root.IsExpanded = true;   // triggers lazy load of first level
        RootNodes.Add(root);
    }

    /// <summary>
    /// Called by the TreeView's SelectedItemChanged (wired in code-behind).
    /// Validates the node type and fires the FileSelected callback.
    /// </summary>
    public void OnNodeSelected(FileSystemNode? node)
    {
        if (node is null || node.IsDirectory || !node.IsSupported)
            return;

        FileSelected?.Invoke(node.FullPath);
    }
}