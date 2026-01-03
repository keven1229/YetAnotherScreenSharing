using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YASS.Client.Core.Capture;
using YASS.Client.Core.Encoding;
using YASS.Client.Core.Interfaces;
using YASS.Client.Core.Services;
using YASS.Client.Core.Streaming;
using YASS.Client.Desktop.Services;
using YASS.Client.Desktop.ViewModels;
using YASS.Client.Desktop.Views;

namespace YASS.Client.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 日志
                services.AddLogging(builder =>
                {
                    builder.AddDebug();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });

                // HTTP 客户端
                services.AddHttpClient("YassApi", client =>
                {
                    client.BaseAddress = new Uri("http://localhost:5000");
                });

                // 核心服务
                services.AddTransient<IScreenCapture, WindowsGraphicsCapture>();
                services.AddTransient<IEncoder, FFmpegEncoder>();
                services.AddTransient<IStreamer, SafeRtmpStreamer>(); // 使用线程安全的推流器
                services.AddTransient<ScreenSharingService>();

                // 应用服务
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IApiService, ApiService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<StreamingViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        // 启动主窗口
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _host?.Dispose();
    }
}

