using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioCompressionApp.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// FILE-SYSTEM NODE
//
// Represents one item (file or directory) in the explorer TreeView.
//
// Lazy loading strategy:
//   • Directories get a single dummy child on construction so WPF renders
//     the expand arrow. When the user expands the node, OnIsExpanded fires,
//     the dummy is removed, and real children are loaded from disk.
//   • Files are leaf nodes (no children).
//
// Live update strategy:
//   • When a directory node is expanded, a FileSystemWatcher is created for
//     that directory (not sub-directories — each expanded child manages its own).
//   • When the watcher fires (Created / Deleted / Renamed), the children
//     collection is refreshed on the UI thread via the WPF Dispatcher.
//   • When the node collapses, the watcher is disposed to free OS handles.
//
// Extension classification (drives mode detection in FileSystemTreeViewModel):
//   • AudioExtensions      → Compress mode
//   • CompressedExtensions → Decompress mode
//   • Anything else        → unsupported (node is shown dimmed, not selectable)
// ─────────────────────────────────────────────────────────────────────────────

public partial class FileSystemNode : ObservableObject
{
    // ── Known extensions ──────────────────────────────────────────────────────

    private static readonly string[] AudioExtensions =
        [".wav", ".mp3"];

    private static readonly string[] CompressedExtensions =
        [".admaydi", ".nlqalaa", ".dmjomaat", ".dpcmkasem", ".pdcmanar"];

    // Dummy sentinel used as the placeholder child while the node is collapsed
    private static readonly FileSystemNode DummyChild = new();

    // FileSystemWatcher — only alive while this directory node is expanded
    private FileSystemWatcher? _watcher;

    // Private constructor for the dummy sentinel only
    private FileSystemNode() { }

    // ── Constructor ───────────────────────────────────────────────────────────

    public FileSystemNode(string fullPath, bool isDirectory)
    {
        FullPath    = fullPath;
        Name        = Path.GetFileName(fullPath);
        IsDirectory = isDirectory;

        if (isDirectory)
        {
            // Add a dummy child so WPF shows the expand arrow
            Children.Add(DummyChild);
        }
        else
        {
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            IsAudioFile      = AudioExtensions.Contains(ext);
            IsCompressedFile = CompressedExtensions.Contains(ext);
            IsSupported      = IsAudioFile || IsCompressedFile;
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string FullPath    { get; } = string.Empty;
    public string Name        { get; } = string.Empty;
    public bool   IsDirectory { get; }
    public bool   IsAudioFile      { get; }
    public bool   IsCompressedFile { get; }
    public bool   IsSupported      { get; }

    /// <summary>True when this is an unsupported file type (dimmed in the tree).</summary>
    public bool IsUnsupported => !IsDirectory && !IsSupported;

    /// <summary>Child nodes — populated lazily on first expand.</summary>
    public ObservableCollection<FileSystemNode> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    // ── Expand / collapse ─────────────────────────────────────────────────────

    partial void OnIsExpandedChanged(bool value)
    {
        if (!IsDirectory) return;

        if (value)
        {
            // First expand: swap dummy for real children then start watching
            bool hasDummy = Children.Count == 1 && Children[0] == DummyChild;
            if (hasDummy)
            {
                Children.Clear();
                LoadChildren();
            }

            StartWatcher();
        }
        else
        {
            // Collapsed: stop watching to release OS handles
            StopWatcher();
        }
    }

    // ── Child loading ─────────────────────────────────────────────────────────

    private void LoadChildren()
    {
        try
        {
            // Directories first, then files — both sorted alphabetically
            foreach (string dir in Directory.EnumerateDirectories(FullPath)
                                            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                Children.Add(new FileSystemNode(dir, isDirectory: true));
            }

            foreach (string file in Directory.EnumerateFiles(FullPath)
                                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                Children.Add(new FileSystemNode(file, isDirectory: false));
            }
        }
        catch (UnauthorizedAccessException) { /* skip protected folders */ }
        catch (IOException)                 { /* skip vanished drives   */ }
    }

    // ── FileSystemWatcher ─────────────────────────────────────────────────────

    private void StartWatcher()
    {
        if (_watcher is not null) return;   // already watching
        if (!Directory.Exists(FullPath))    return;

        try
        {
            _watcher = new FileSystemWatcher(FullPath)
            {
                // Watch only the immediate children of this directory,
                // not recursively — each expanded sub-node manages its own watcher.
                IncludeSubdirectories = false,
                NotifyFilter =
                    NotifyFilters.FileName  |
                    NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };

            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemRenamed;
        }
        catch (Exception)
        {
            // Watcher creation can fail on network paths or restricted dirs.
            // Silently fall back to static (non-updating) mode.
            _watcher = null;
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileSystemChanged;
        _watcher.Deleted -= OnFileSystemChanged;
        _watcher.Renamed -= OnFileSystemRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    // FSW events fire on a background thread — marshal to the UI thread.
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        => RefreshOnUiThread();

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        => RefreshOnUiThread();

    private void RefreshOnUiThread()
    {
        // Use the application Dispatcher to update ObservableCollection on the UI thread.
        Application.Current?.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            (Action)RefreshChildren);
    }

    /// <summary>
    /// Rebuilds the Children collection to match the current disk state.
    /// Runs on the UI thread (called via Dispatcher).
    /// </summary>
    private void RefreshChildren()
    {
        if (!IsExpanded) return;    // collapsed while event was queued — skip

        // Snapshot the current disk contents
        string[] dirs, files;
        try
        {
            dirs  = Directory.GetDirectories(FullPath)
                             .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                             .ToArray();
            files = Directory.GetFiles(FullPath)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();
        }
        catch { return; }   // directory vanished or became inaccessible

        // Build the desired set (dirs first, then files)
        var desired = dirs .Select(d => d)
                           .Concat(files)
                           .ToList();

        // ── Remove nodes that no longer exist on disk ──
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(Children[i].FullPath,
                                  StringComparer.OrdinalIgnoreCase))
            {
                Children[i].StopWatcher();   // clean up any watcher on removed nodes
                Children.RemoveAt(i);
            }
        }

        // ── Add nodes that appeared on disk ──
        var existing = Children.Select(c => c.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int insertIndex = 0;
        foreach (string path in desired)
        {
            if (!existing.Contains(path))
            {
                bool isDir = Directory.Exists(path);
                Children.Insert(insertIndex, new FileSystemNode(path, isDir));
            }
            insertIndex++;
        }
    }
}