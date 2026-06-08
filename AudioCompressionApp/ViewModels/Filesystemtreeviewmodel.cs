using System;
using System.Collections.ObjectModel;
using System.IO;
using AudioCompressionApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AudioCompressionApp.ViewModels;


public partial class FileSystemTreeViewModel : ObservableObject
{

    public Action<string>? FileSelected { get; set; }


    public ObservableCollection<FileSystemNode> RootNodes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRootFolder))]
    [NotifyPropertyChangedFor(nameof(RootFolderName))]
    private string _rootFolderPath = string.Empty;

    public bool HasRootFolder => !string.IsNullOrEmpty(RootFolderPath);

    public string RootFolderName => HasRootFolder
        ? Path.GetFileName(RootFolderPath.TrimEnd(Path.DirectorySeparatorChar,
                                                    Path.AltDirectorySeparatorChar))
        : "No folder opened";

  
    [RelayCommand]
    private void OpenFolder()
    {
     
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to browse",
        };

        if (dialog.ShowDialog() == true)
            LoadFolder(dialog.FolderName);
    }

   
    public void LoadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        RootFolderPath = folderPath;
        RootNodes.Clear();

        
        var root = new FileSystemNode(folderPath, isDirectory: true);
        root.IsExpanded = true;   // triggers lazy load of first level
        RootNodes.Add(root);
    }


    public void OnNodeSelected(FileSystemNode? node)
    {
        if (node is null || node.IsDirectory || !node.IsSupported)
            return;

        FileSelected?.Invoke(node.FullPath);
    }
}