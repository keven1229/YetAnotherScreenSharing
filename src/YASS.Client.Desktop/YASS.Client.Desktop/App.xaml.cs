using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using YASS.Client.Desktop.Services;
using YASS.Client.Desktop.ViewModels;

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

                // 应用服务
                services.AddSingleton<IApiService, ApiService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<StreamingViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        // 启动主窗口
        var mainWindow = Services.GetRequiredService<MainWindow>();
        
        // 初始化 WPF UI 主题 - 监视系统主题变化
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(mainWindow);
        
        mainWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _host?.Dispose();
    }
}

