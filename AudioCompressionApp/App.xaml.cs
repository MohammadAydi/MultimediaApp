using System.Windows;
using AudioCompressionApp.Services;
using AudioCompressionApp.ViewModels;
using AudioCompressionApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AudioCompressionApp;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()

            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<MainWindow>();

                services.AddSingleton<MainViewModel>();

                services.AddSingleton<AudioFileService>();

                services.AddSingleton<AudioPlaybackService>();
            })

            .Build();
    }

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        await _host.StartAsync();

        MainWindow mainWindow =
            _host.Services.GetRequiredService<MainWindow>();

        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(
        ExitEventArgs e)
    {
        await _host.StopAsync();

        _host.Dispose();

        base.OnExit(e);
    }
}