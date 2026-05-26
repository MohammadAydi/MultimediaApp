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
    
}