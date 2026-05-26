using System.Windows;
using AudioCompressionApp.ViewModels;

namespace AudioCompressionApp.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }
    
    private void FileDropBorder_OnDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
    }

    private void FileDropBorder_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

        if (files.Length == 0)
            return;

        string filePath = files[0];

        MessageBox.Show(filePath);
    }
}