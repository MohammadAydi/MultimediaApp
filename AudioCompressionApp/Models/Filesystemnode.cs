using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioCompressionApp.Models;

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
// Extension classification (drives mode detection in FileSystemTreeViewModel):
//   • AudioExtensions   → Compress mode
//   • CompressedExtensions → Decompress mode
//   • Anything else     → unsupported (node is shown dimmed, not selectable)
// ─────────────────────────────────────────────────────────────────────────────

public partial class FileSystemNode : ObservableObject {
    // ── Known extensions ──────────────────────────────────────────────────────

    private static readonly string[] AudioExtensions =
        [".wav", ".mp3"];

    private static readonly string[] CompressedExtensions =
        [".admaydi", ".nlqalaa", ".dmjomaat", ".dpcmkasem", ".pdcmanar"];

    // Dummy sentinel used as the placeholder child while the node is collapsed
    private static readonly FileSystemNode DummyChild = new();

    // Private constructor for the dummy sentinel only
    private FileSystemNode() {
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public FileSystemNode(string fullPath, bool isDirectory) {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
        IsDirectory = isDirectory;

        if (isDirectory) {
            // Add a dummy child so WPF shows the expand arrow
            Children.Add(DummyChild);
        }
        else {
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            IsAudioFile = AudioExtensions.Contains(ext);
            IsCompressedFile = CompressedExtensions.Contains(ext);
            IsSupported = IsAudioFile || IsCompressedFile;
        }
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string FullPath { get; } = string.Empty;
    public string Name { get; } = string.Empty;
    public bool IsDirectory { get; }
    public bool IsAudioFile { get; }
    public bool IsCompressedFile { get; }
    public bool IsSupported { get; }

    /// <summary>
    /// True when this is an unsupported file type (dimmed in the tree).
    /// </summary>
    public bool IsUnsupported => !IsDirectory && !IsSupported;

    /// <summary>Child nodes — populated lazily on first expand.</summary>
    public ObservableCollection<FileSystemNode> Children { get; } = [];

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    // ── Lazy loading ──────────────────────────────────────────────────────────

    partial void OnIsExpandedChanged(bool value) {
        if (!IsDirectory || !value) return;

        // Only load if we still have the dummy placeholder
        bool hasDummy = Children.Count == 1 && Children[0] == DummyChild;
        if (!hasDummy) return;

        Children.Clear();
        LoadChildren();
    }

    private void LoadChildren() {
        try {
            // Directories first, then files — both sorted alphabetically
            foreach (string dir in Directory.EnumerateDirectories(FullPath)
                         .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)) {
                Children.Add(new FileSystemNode(dir, isDirectory: true));
            }

            foreach (string file in Directory.EnumerateFiles(FullPath)
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)) {
                Children.Add(new FileSystemNode(file, isDirectory: false));
            }

            // If the directory is empty, add nothing — WPF hides the arrow automatically
        }
        catch (UnauthorizedAccessException) {
            // Silently skip folders we can't read (system/protected dirs)
        }
        catch (IOException) {
            // Silently skip IO errors (e.g. network drives that disappeared)
        }
    }
}