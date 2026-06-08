using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioCompressionApp.ViewModels;

public partial class FileSystemNode : ObservableObject
{
    private static readonly string[] AudioExtensions =
        [".wav", ".mp3"];

    private static readonly string[] CompressedExtensions =
        [".admaydi", ".nlqalaa", ".dmjomaat", ".dpcmkasem", ".pdcmanar"];

    private static readonly FileSystemNode DummyChild = new();

    private FileSystemWatcher? _watcher;

    private FileSystemNode() { }


    public FileSystemNode(string fullPath, bool isDirectory)
    {
        FullPath    = fullPath;
        Name        = Path.GetFileName(fullPath);
        IsDirectory = isDirectory;

        if (isDirectory)
        {
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

    public bool IsUnsupported => !IsDirectory && !IsSupported;

    public ObservableCollection<FileSystemNode> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    // ── Expand / collapse ─────────────────────────────────────────────────────
    partial void OnIsExpandedChanged(bool value)
    {
        if (!IsDirectory) return;

        if (value)
        {
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
            StopWatcher();
        }
    }


    private void LoadChildren()
    {
        try
        {
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


    private void StartWatcher()
    {
        if (_watcher is not null) return;  
        if (!Directory.Exists(FullPath))    return;

        try
        {
            _watcher = new FileSystemWatcher(FullPath)
            {
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

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        => RefreshOnUiThread();

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        => RefreshOnUiThread();

    private void RefreshOnUiThread()
    {
        Application.Current?.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            (Action)RefreshChildren);
    }


    private void RefreshChildren()
    {
        if (!IsExpanded) return;   

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
        catch { return; }   

        var desired = dirs .Select(d => d)
                           .Concat(files)
                           .ToList();

     
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(Children[i].FullPath,
                                  StringComparer.OrdinalIgnoreCase))
            {
                Children[i].StopWatcher();  
                Children.RemoveAt(i);
            }
        }

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