using System;
using System.IO;
using System.Windows;
using FFmpeg.AutoGen;
using Microsoft.Extensions.DependencyInjection;
using SimpleSRT.App.Services;
using SimpleSRT.App.Services.Interfaces;
using SimpleSRT.App.ViewModels;
using SimpleSRT.App.Views;

namespace SimpleSRT.App;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Registro das dependências da aplicação
        services.AddSingleton<IMediaPlayerService, VLCPlayerService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configuração do caminho das DLLs nativas do FFmpeg
        ConfigureFFmpeg();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    private static void ConfigureFFmpeg()
    {
        // Define o diretório atual onde o executável foi instalado
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Se as DLLs nativas do FFmpeg estiverem soltas na pasta do executável:
        ffmpeg.RootPath = baseDirectory;

        // Caso as DLLs fiquem em uma subpasta específica (ex: 'runtimes/win-x64/native' ou 'ffmpeg'):
        // string ffmpegPath = Path.Combine(baseDirectory, "ffmpeg");
        // if (Directory.Exists(ffmpegPath))
        // {
        //     ffmpeg.RootPath = ffmpegPath;
        // }
    }
}